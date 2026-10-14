using System.Globalization;
using Freeway.Application.Features.Chat.Commands;
using Freeway.Domain.Entities;
using Freeway.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Freeway.Application.Features.Chat;

/// <summary>
/// Writes a usage_logs row for a chat request, off the request path.
/// </summary>
public sealed class ChatUsageLogger
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ChatUsageLogger> _logger;

    public ChatUsageLogger(IServiceScopeFactory scopeFactory, ILogger<ChatUsageLogger> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Logs in the background (fire-and-forget with its own scope), so a slow database
    /// never holds up a reply.
    /// </summary>
    /// <param name="timeToFirstTokenMs">For a stream, how long the caller waited for its first piece.</param>
    public void LogInBackground(
        CreateChatCompletionCommand request,
        ChatRoute route,
        ChatCompletionResult result,
        int? timeToFirstTokenMs = null)
    {
        _ = Task.Run(() => LogAsync(request, route, result, timeToFirstTokenMs));
    }

    private async Task LogAsync(
        CreateChatCompletionCommand request,
        ChatRoute route,
        ChatCompletionResult result,
        int? timeToFirstTokenMs)
    {
        try
        {
            // Create a new scope so we have our own DbContext instance
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            var dateTimeService = scope.ServiceProvider.GetRequiredService<IDateTimeService>();

            var model = route.Model;
            var modelId = route.ModelId
                          ?? (route.ServedLocally && string.IsNullOrEmpty(result.Model) ? "claude-code" : result.Model);

            if (request.Stream)
            {
                EstimateMissingUsage(request, result);
            }

            // What was avoided is what the model this request would otherwise have used
            // costs -- not what Claude would have billed. For moderate those differ by
            // a factor of tens, and reporting Claude's list price there would overstate
            // the saving badly in the weekly figures.
            if (route.ServedLocally && result.Success)
            {
                result.AvoidedCostUsd = ChatRouter.EstimateAvoidedCost(route.Candidates, result) ?? result.AvoidedCostUsd;
            }

            decimal promptCost = 0;
            decimal completionCost = 0;
            decimal totalCost = 0;
            string costSource;

            if (model != null)
            {
                // Kept for the per-token columns and as the fallback figure. Parsed with
                // the invariant culture because these are API strings like "0.0000001",
                // not values formatted for the host's locale.
                decimal.TryParse(model.PromptPrice, NumberStyles.Float, CultureInfo.InvariantCulture, out promptCost);
                decimal.TryParse(model.CompletionPrice, NumberStyles.Float, CultureInfo.InvariantCulture, out completionCost);
                totalCost = (result.Usage.PromptTokens * promptCost) + (result.Usage.CompletionTokens * completionCost);
            }

            if (result.CostUsd.HasValue)
            {
                // The upstream told us what it actually charged. Always prefer that:
                // provider routing means the endpoint that served the request may not
                // charge the model's headline rate, so the estimate above can be wrong
                // in either direction.
                totalCost = result.CostUsd.Value;
                costSource = result.CostSource ?? "provider";
            }
            else if (model != null)
            {
                costSource = "estimated";
            }
            else
            {
                // No price and no billed amount. Record the gap rather than writing a
                // confident zero: that conflation is what hid real spend before.
                costSource = "unknown";
                _logger.LogWarning(
                    "No cost information for {ModelId} via {Provider}; logging usage with cost unknown",
                    modelId, result.ProviderName ?? "unknown");
            }

            var usageLog = new UsageLog
            {
                Id = Guid.NewGuid(),
                ProjectId = request.ProjectId,
                ModelId = modelId,
                ModelType = route.ModelType,
                ModelTier = route.ModelTier,
                InputTokens = result.Usage.PromptTokens,
                OutputTokens = result.Usage.CompletionTokens,
                ResponseTimeMs = result.ResponseTimeMs,
                CostUsd = totalCost,
                PromptCostPerToken = promptCost,
                CompletionCostPerToken = completionCost,
                Success = result.Success,
                ErrorMessage = result.ErrorMessage,
                RequestId = result.Id,
                CreatedAt = dateTimeService.UtcNow,
                Provider = result.ProviderName ?? "openrouter",
                CostSource = costSource,
                UpstreamProvider = result.UpstreamProvider,

                // The request's own shape, so a change to how a caller calls this
                // gateway can be confirmed from the logs rather than inferred.
                ReasoningEffort = request.Reasoning?.Effort,
                RequestedMaxTokens = request.MaxTokens,
                Streamed = request.Stream,
                TimeToFirstTokenMs = request.Stream ? timeToFirstTokenMs : null,
                AvoidedCostUsd = result.AvoidedCostUsd,
                RequestMessages = request.Messages.Select(m => new ChatMessage
                {
                    Role = m.Role,
                    Content = m.Content
                }).ToList(),
                ResponseContent = result.Choices.FirstOrDefault()?.Message.Content,
                FinishReason = result.FinishReason,
                RequestParams = new Dictionary<string, object>
                {
                    ["temperature"] = request.Temperature ?? 0.7,
                    ["max_tokens"] = request.MaxTokens ?? 0,
                    ["stream"] = request.Stream
                }
            };

            context.UsageLogs.Add(usageLog);
            await context.SaveChangesAsync();

            _logger.LogDebug("Usage logged for project {ProjectId}, model {ModelId}", request.ProjectId, modelId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log usage for project {ProjectId}", request.ProjectId);
        }
    }

    /// <summary>
    /// Fills in token counts for a stream that ended without reporting them.
    ///
    /// Upstreams report usage on a stream's last chunk, so a stream cut short -- most
    /// often by the caller hanging up -- never gets it, though the tokens up to that
    /// point were generated and are billed. Logging zero would make that spend
    /// vanish, so the counts are estimated from the text at about four characters a
    /// token. Any cost worked out from them is marked "estimated", which is what it is.
    /// </summary>
    private void EstimateMissingUsage(CreateChatCompletionCommand request, ChatCompletionResult result)
    {
        var reply = result.Choices.FirstOrDefault()?.Message.Content ?? "";
        if (result.Usage.TotalTokens > 0 || reply.Length == 0)
        {
            return;
        }

        var prompt = Tokens(request.Messages.Sum(m => m.Content.Length));
        var completion = Tokens(reply.Length);
        result.Usage = new ChatCompletionUsage
        {
            PromptTokens = prompt,
            CompletionTokens = completion,
            TotalTokens = prompt + completion
        };

        _logger.LogDebug(
            "Stream {RequestId} ended without usage; estimated {Prompt}+{Completion} tokens from its text",
            result.Id, prompt, completion);

        static int Tokens(int characters) => (characters + 3) / 4;
    }
}
