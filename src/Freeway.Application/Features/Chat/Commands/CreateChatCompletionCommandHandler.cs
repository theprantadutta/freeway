using Freeway.Application.Common;
using Freeway.Application.DTOs;
using Freeway.Domain.Entities;
using Freeway.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Freeway.Application.Features.Chat.Commands;

public class CreateChatCompletionCommandHandler : IRequestHandler<CreateChatCompletionCommand, Result<ChatCompletionResponseDto>>
{
    private readonly IOpenRouterService _openRouterService;
    private readonly IProviderOrchestrator _providerOrchestrator;
    private readonly IModelCacheService _modelCacheService;
    private readonly IProviderModelCache _providerModelCache;
    private readonly IModelCooldownCache _modelCooldownCache;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDateTimeService _dateTimeService;
    private readonly ILogger<CreateChatCompletionCommandHandler> _logger;

    public CreateChatCompletionCommandHandler(
        IOpenRouterService openRouterService,
        IProviderOrchestrator providerOrchestrator,
        IModelCacheService modelCacheService,
        IProviderModelCache providerModelCache,
        IModelCooldownCache modelCooldownCache,
        IServiceScopeFactory scopeFactory,
        IDateTimeService dateTimeService,
        ILogger<CreateChatCompletionCommandHandler> logger)
    {
        _openRouterService = openRouterService;
        _providerOrchestrator = providerOrchestrator;
        _modelCacheService = modelCacheService;
        _providerModelCache = providerModelCache;
        _modelCooldownCache = modelCooldownCache;
        _scopeFactory = scopeFactory;
        _dateTimeService = dateTimeService;
        _logger = logger;
    }

    public async Task<Result<ChatCompletionResponseDto>> Handle(CreateChatCompletionCommand request, CancellationToken cancellationToken)
    {
        // Convert messages
        var messages = request.Messages.Select(m => new ChatMessage
        {
            Role = m.Role,
            Content = m.Content
        }).ToList();

        // Create options
        var options = new ChatCompletionOptions
        {
            Temperature = request.Temperature,
            MaxTokens = request.MaxTokens,
            TopP = request.TopP,
            FrequencyPenalty = request.FrequencyPenalty,
            PresencePenalty = request.PresencePenalty,
            Stop = request.Stop,
            Stream = request.Stream
        };

        ChatCompletionResult result;
        string modelId;
        string modelType;
        CachedModel? model = null;

        // Use orchestrator for "free" requests, direct OpenRouter for "paid" or specific models
        if (request.Model.Equals("free", StringComparison.OrdinalIgnoreCase))
        {
            // Use multi-provider orchestrator with smart fallback
            result = await _providerOrchestrator.ExecuteWithFallbackAsync(messages, options, cancellationToken);
            modelId = result.Model;
            modelType = "free";
        }
        else
        {
            // Resolve model for paid or specific model requests
            var resolved = ResolveModel(request.Model);

            // Check for validation error
            if (resolved.error != null)
            {
                return Result<ChatCompletionResponseDto>.Failure(resolved.error, 400);
            }

            modelType = resolved.modelType;

            // Build an ordered candidate list. For the "paid"/"image" virtual models we
            // include backup models (next cheapest) so one rate-limited/failing model does
            // not fail the whole request. Specific model IDs keep single-attempt semantics.
            var candidates = BuildPaidCandidates(request.Model, resolved);

            if (candidates.Count == 0)
            {
                return Result<ChatCompletionResponseDto>.ServiceUnavailable($"Model '{request.Model}' not available");
            }

            // Try each candidate in order until one succeeds.
            (result, modelId, model) = await ExecuteWithModelFallbackAsync(candidates, messages, options, cancellationToken);
        }

        // Log usage in background (fire-and-forget with its own scope)
        _ = Task.Run(() => LogUsageInBackgroundAsync(request, modelId, modelType, model, result));

        if (!result.Success)
        {
            return Result<ChatCompletionResponseDto>.BadGateway(result.ErrorMessage ?? "API error");
        }

        return Result<ChatCompletionResponseDto>.Success(new ChatCompletionResponseDto
        {
            Id = result.Id,
            Object = "chat.completion",
            Created = result.Created,
            Model = result.Model,
            Choices = result.Choices.Select(c => new ChatChoiceDto
            {
                Index = c.Index,
                Message = new ChatMessageDto
                {
                    Role = c.Message.Role,
                    Content = c.Message.Content
                },
                FinishReason = c.FinishReason
            }).ToList(),
            Usage = new UsageDto
            {
                PromptTokens = result.Usage.PromptTokens,
                CompletionTokens = result.Usage.CompletionTokens,
                TotalTokens = result.Usage.TotalTokens
            }
        });
    }

    // Number of backup models to try after the primary for "paid"/"image" virtual models.
    private static readonly int PaidFallbackCount =
        int.TryParse(Environment.GetEnvironmentVariable("PAID_FALLBACK_COUNT"), out var n) && n >= 0 ? n : 3;

    /// <summary>
    /// Builds the ordered list of models to attempt. For the "paid" and "image" virtual
    /// models the selected model is tried first, followed by the next cheapest models as
    /// backups. Specific model IDs resolve to a single candidate (no silent substitution).
    /// </summary>
    private List<(string modelId, CachedModel? model)> BuildPaidCandidates(
        string requestedModel,
        (string? modelId, string modelType, CachedModel? model, string? error) resolved)
    {
        var candidates = new List<(string modelId, CachedModel? model)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(CachedModel? m)
        {
            if (m == null || string.IsNullOrEmpty(m.Id) || !seen.Add(m.Id))
                return;
            candidates.Add((m.Id, m));
        }

        if (requestedModel.Equals("paid", StringComparison.OrdinalIgnoreCase))
        {
            Add(resolved.model ?? _modelCacheService.GetSelectedPaidModel());
            foreach (var m in _modelCacheService.GetPaidModels()) // ranked cheapest-first
            {
                if (candidates.Count > PaidFallbackCount) break;
                Add(m);
            }
        }
        else if (requestedModel.Equals("image", StringComparison.OrdinalIgnoreCase))
        {
            Add(resolved.model ?? _modelCacheService.GetSelectedImageModel());
            foreach (var m in _modelCacheService.GetImageModels()) // ranked cheapest-first
            {
                if (candidates.Count > PaidFallbackCount) break;
                Add(m);
            }
        }
        else if (resolved.modelId != null)
        {
            // Specific model ID - single attempt, no substitution.
            candidates.Add((resolved.modelId, resolved.model));
            return candidates;
        }

        // Push models currently on cooldown (recently rate-limited) to the back so we prefer
        // healthy models first, while still keeping them as a last resort if all else fails.
        return candidates
            .OrderBy(c => _modelCooldownCache.IsRateLimited(c.modelId) ? 1 : 0)
            .ToList();
    }

    /// <summary>
    /// Calls OpenRouter for each candidate in order, returning the first success. On failure
    /// it advances to the next candidate. Returns the last failure if all candidates fail.
    /// </summary>
    private async Task<(ChatCompletionResult result, string modelId, CachedModel? model)> ExecuteWithModelFallbackAsync(
        List<(string modelId, CachedModel? model)> candidates,
        List<ChatMessage> messages,
        ChatCompletionOptions options,
        CancellationToken cancellationToken)
    {
        ChatCompletionResult? lastResult = null;

        for (int i = 0; i < candidates.Count; i++)
        {
            var (candidateId, candidateModel) = candidates[i];
            var result = await _openRouterService.CreateChatCompletionAsync(candidateId, messages, options, cancellationToken);

            if (result.Success)
            {
                if (i > 0)
                {
                    _logger.LogInformation(
                        "Request succeeded with backup model {Model} (attempt {Attempt}/{Total})",
                        candidateId, i + 1, candidates.Count);
                }
                return (result, candidateId, candidateModel);
            }

            lastResult = result;

            // Park rate-limited models so subsequent requests skip them during their cooldown.
            if (result.HttpStatusCode == 429)
            {
                _modelCooldownCache.MarkRateLimited(candidateId);
            }

            _logger.LogWarning(
                "Model {Model} failed ({StatusCode}): {Error}. {Remaining} backup model(s) remaining",
                candidateId, result.HttpStatusCode, result.ErrorMessage, candidates.Count - i - 1);
        }

        if (candidates.Count > 1)
        {
            _logger.LogError("All {Count} candidate models failed for the request", candidates.Count);
        }

        var (lastId, lastModel) = candidates[^1];
        return (lastResult ?? new ChatCompletionResult { Success = false, ErrorMessage = "No models available" },
                lastId, lastModel);
    }

    private (string? modelId, string modelType, CachedModel? model, string? error) ResolveModel(string requestedModel)
    {
        // Handle "free" and "paid" keywords
        if (requestedModel.Equals("free", StringComparison.OrdinalIgnoreCase))
        {
            var model = _modelCacheService.GetSelectedFreeModel();
            return (model?.Id, "free", model, null);
        }

        if (requestedModel.Equals("paid", StringComparison.OrdinalIgnoreCase))
        {
            var model = _modelCacheService.GetSelectedPaidModel();
            return (model?.Id, "paid", model, null);
        }

        if (requestedModel.Equals("image", StringComparison.OrdinalIgnoreCase))
        {
            var model = _modelCacheService.GetSelectedImageModel();
            return (model?.Id, "image", model, null);
        }

        // Look up specific model in legacy cache (OpenRouter models)
        var cachedModel = _modelCacheService.GetModelById(requestedModel);
        if (cachedModel != null)
        {
            var type = cachedModel.IsImageModel ? "image" : cachedModel.IsFree ? "free" : "paid";
            return (cachedModel.Id, type, cachedModel, null);
        }

        // Check provider model cache for strict validation
        var providers = _providerModelCache.FindProvidersForModel(requestedModel);
        if (providers.Count > 0)
        {
            _logger.LogDebug("Model '{Model}' found on providers: {Providers}",
                requestedModel, string.Join(", ", providers));
            return (requestedModel, "specific", null, null);
        }

        // Check if it looks like an OpenRouter model format (contains /)
        if (requestedModel.Contains('/'))
        {
            // OpenRouter format - check if it's in the openrouter provider cache
            if (_providerModelCache.IsValidModel("openrouter", requestedModel))
            {
                return (requestedModel, "paid", null, null);
            }
        }

        // Model not found in any cache - return error for strict validation
        var summary = _providerModelCache.GetCacheSummary();
        if (summary.TotalModelCount > 0)
        {
            // Cache is populated, so this is a genuinely invalid model
            _logger.LogWarning("Model '{Model}' not found in any provider cache", requestedModel);
            return (null, "unknown", null, $"Model '{requestedModel}' is not available. Use GET /v1/models to see available models.");
        }

        // Cache not yet populated - allow pass-through for backwards compatibility
        _logger.LogDebug("Provider model cache not yet populated, allowing pass-through for '{Model}'", requestedModel);
        return (requestedModel, "unknown", null, null);
    }

    private async Task LogUsageInBackgroundAsync(
        CreateChatCompletionCommand request,
        string modelId,
        string modelType,
        CachedModel? model,
        ChatCompletionResult result)
    {
        try
        {
            // Create a new scope so we have our own DbContext instance
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            var dateTimeService = scope.ServiceProvider.GetRequiredService<IDateTimeService>();

            decimal promptCost = 0;
            decimal completionCost = 0;
            decimal totalCost = 0;

            if (model != null)
            {
                decimal.TryParse(model.PromptPrice, out promptCost);
                decimal.TryParse(model.CompletionPrice, out completionCost);
                totalCost = (result.Usage.PromptTokens * promptCost) + (result.Usage.CompletionTokens * completionCost);
            }

            var usageLog = new UsageLog
            {
                Id = Guid.NewGuid(),
                ProjectId = request.ProjectId,
                ModelId = modelId,
                ModelType = modelType,
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
                    ["max_tokens"] = request.MaxTokens ?? 0
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
}
