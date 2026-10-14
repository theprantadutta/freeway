using System.Globalization;
using Freeway.Application.Features.Chat.Commands;
using Freeway.Domain.Common;
using Freeway.Domain.Entities;
using Freeway.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Freeway.Application.Features.Chat;

/// <summary>
/// Where a request was sent, for logging it once the outcome is known.
/// </summary>
/// <param name="ModelId">
/// The model logged against the request, or null to take it from the result -- the
/// free lane and local Claude only learn which model served them as they reply.
/// </param>
/// <param name="Candidates">The paid chain the request would have used, for pricing a local answer.</param>
/// <param name="ServedLocally">The attempt went to the local Claude Code subscription.</param>
public sealed record ChatRoute(
    string? ModelId,
    string ModelType,
    string? ModelTier,
    CachedModel? Model,
    IReadOnlyList<(string ModelId, CachedModel? Model)> Candidates,
    bool ServedLocally);

/// <summary>
/// The calls a route can make, in whichever form the caller wants back: a finished
/// result, or a stream. <see cref="Outcome"/> reads how an attempt went.
/// </summary>
public sealed record ChatBackends<T>(
    Func<Task<T>> Free,
    Func<LocalClaudePriority, Task<T>> Local,
    Func<string, Task<T>> Paid,
    Func<T, ChatCompletionResult> Outcome);

/// <summary>What routing a request came to.</summary>
public sealed class ChatRouting<T>
{
    /// <summary>The attempt that answers the request, or the last one that failed.</summary>
    public T? Attempt { get; init; }

    /// <summary>Set whenever an attempt was made, so the request can be logged.</summary>
    public ChatRoute? Route { get; init; }

    /// <summary>Why the request cannot be served, or null when it can.</summary>
    public string? Error { get; init; }

    public int StatusCode { get; init; } = 200;
}

/// <summary>
/// Decides where a chat request goes and falls back until something answers.
///
/// Shared by the streaming and non-streaming endpoints, which differ only in what an
/// attempt returns. Keeping one copy of the routing is the point: the lanes, the
/// local-Claude rules and the fallback chain are subtle enough that two copies would
/// drift, and a streaming caller must get exactly the routing anyone else gets.
/// </summary>
public sealed class ChatRouter
{
    private readonly IModelCacheService _modelCacheService;
    private readonly IProviderModelCache _providerModelCache;
    private readonly IModelCooldownCache _modelCooldownCache;
    private readonly ILocalClaudeService _localClaude;
    private readonly ILogger<ChatRouter> _logger;

    public ChatRouter(
        IModelCacheService modelCacheService,
        IProviderModelCache providerModelCache,
        IModelCooldownCache modelCooldownCache,
        ILocalClaudeService localClaude,
        ILogger<ChatRouter> logger)
    {
        _modelCacheService = modelCacheService;
        _providerModelCache = providerModelCache;
        _modelCooldownCache = modelCooldownCache;
        _localClaude = localClaude;
        _logger = logger;
    }

    /// <summary>
    /// Outcome of turning the requested "model" value into something callable.
    /// </summary>
    private sealed record ResolvedModel(
        string? ModelId,
        string ModelType,
        CachedModel? Model,
        string? Error = null,
        PaidTier? Tier = null);

    public static List<ChatMessage> ToMessages(CreateChatCompletionCommand request) =>
        request.Messages.Select(m => new ChatMessage
        {
            // "developer" is what newer OpenAI clients send in place of "system". No
            // upstream behind this gateway knows it, and every one of them knows system.
            Role = m.Role.Equals("developer", StringComparison.OrdinalIgnoreCase) ? "system" : m.Role,
            Content = m.Content
        }).ToList();

    public static ChatCompletionOptions ToOptions(CreateChatCompletionCommand request) => new()
    {
        Temperature = request.Temperature,
        MaxTokens = request.MaxTokens,
        TopP = request.TopP,
        FrequencyPenalty = request.FrequencyPenalty,
        PresencePenalty = request.PresencePenalty,
        Stop = request.Stop,
        Stream = request.Stream,
        Reasoning = request.Reasoning is null ? null : new ReasoningOptions
        {
            Effort = request.Reasoning.Effort,
            Exclude = request.Reasoning.Exclude,
            MaxTokens = request.Reasoning.MaxTokens,
            Enabled = request.Reasoning.Enabled
        }
    };

    public async Task<ChatRouting<T>> RouteAsync<T>(CreateChatCompletionCommand request, ChatBackends<T> backends)
    {
        // use_local_only turns the usual contract inside out. Everywhere else the
        // local subscription is an optimisation and a paid model is the safety net;
        // here the caller has said they would rather have an error than a bill, so
        // there is no net and every failure surfaces instead of being absorbed.
        if (request.UseLocalOnly)
        {
            return await RouteLocalOnlyAsync(request, backends);
        }

        // Use orchestrator for "free" requests, direct OpenRouter for "paid" or specific models
        if (request.Model.Equals("free", StringComparison.OrdinalIgnoreCase))
        {
            // Use multi-provider orchestrator with smart fallback
            var free = await backends.Free();
            return Finish(backends, free, new ChatRoute(null, "free", null, null, [], false), failureStatus: 502);
        }

        // Resolve model for paid or specific model requests
        var resolved = ResolveModel(request.Model);

        // Check for validation error
        if (resolved.Error != null)
        {
            return Refuse<T>(resolved.Error, 400);
        }

        var modelType = resolved.ModelType;
        var modelTier = resolved.Tier?.ToSlug();

        // Build an ordered candidate list. For the "paid"/"paid:<tier>"/"image" virtual
        // models we include backup models so one rate-limited/failing model does not fail
        // the whole request. Specific model IDs keep single-attempt semantics.
        var candidates = BuildPaidCandidates(request.Model, resolved);

        // Two lanes can be served from the local Claude Code subscription at no
        // charge, on different terms.
        //
        // Premium is the lane the subscription exists for: it displaces
        // frontier-model pricing, so it queues, waits, and may spend the whole
        // hourly budget. Moderate only rides along on genuinely spare capacity --
        // it displaces models costing a fraction of a cent, so it is never worth
        // a wait or a share of premium's reserve, and the bridge refuses it
        // instantly when either is in question.
        //
        // Both are strictly optimisations. Anything wrong and the request carries
        // on to the paid chain below with the reason in the log.
        var localPriority = resolved.Tier switch
        {
            PaidTier.Premium => (LocalClaudePriority?)LocalClaudePriority.Premium,
            PaidTier.Moderate => LocalClaudePriority.Spillover,
            _ => null
        };

        if (localPriority is { } priority && _localClaude.IsConfigured)
        {
            var health = _localClaude.Health;
            if (health.CanServe)
            {
                var local = await backends.Local(priority);
                var outcome = backends.Outcome(local);
                if (outcome.Success)
                {
                    return Finish(backends, local,
                        new ChatRoute(null, modelType, modelTier, null, candidates, ServedLocally: true),
                        failureStatus: 502);
                }

                await DiscardAsync(local);
                _logger.LogInformation(
                    "{Tier} request did not use local Claude ({Reason}); using a paid model instead",
                    resolved.Tier, outcome.ErrorMessage);
            }
            else
            {
                _logger.LogInformation(
                    "{Tier} request skipped local Claude ({Status}); using a paid model instead",
                    resolved.Tier, health.Describe());
            }
        }

        if (candidates.Count == 0)
        {
            return Refuse<T>($"Model '{request.Model}' not available", 503);
        }

        // Try each candidate in order until one succeeds.
        var (attempt, modelId, model) = await ExecuteWithModelFallbackAsync(candidates, backends);
        return Finish(backends, attempt,
            new ChatRoute(modelId, modelType, modelTier, model, candidates, false),
            failureStatus: 502);
    }

    private static ChatRouting<T> Finish<T>(ChatBackends<T> backends, T attempt, ChatRoute route, int failureStatus)
    {
        var outcome = backends.Outcome(attempt);
        return outcome.Success
            ? new ChatRouting<T> { Attempt = attempt, Route = route }
            : new ChatRouting<T>
            {
                Attempt = attempt,
                Route = route,
                Error = outcome.ErrorMessage ?? "API error",
                StatusCode = failureStatus
            };
    }

    private static ChatRouting<T> Refuse<T>(string error, int statusCode) =>
        new() { Error = error, StatusCode = statusCode };

    /// <summary>Releases an attempt that is being passed over, such as a stream that failed.</summary>
    private static async Task DiscardAsync<T>(T attempt)
    {
        if (attempt is IAsyncDisposable disposable)
        {
            await disposable.DisposeAsync();
        }
    }

    /// <summary>
    /// The model_type and model_tier a request should be recorded under, without
    /// resolving it to an actual callable model.
    /// </summary>
    private (string ModelType, string? ModelTier) DescribeLane(string requestedModel)
    {
        if (requestedModel.Equals("free", StringComparison.OrdinalIgnoreCase))
        {
            return ("free", null);
        }

        var resolved = ResolveModel(requestedModel);
        return resolved.Error is null
            ? (resolved.ModelType, resolved.Tier?.ToSlug())
            : ("paid", null);
    }

    /// <summary>
    /// Serves a request that asked for the local subscription and nothing else.
    ///
    /// Runs at premium priority whatever lane was named: the caller has asked for
    /// this explicitly and accepted that it may fail, so refusing instantly the way
    /// spillover does would be the wrong answer to the wrong question. It waits, and
    /// it may use the whole hourly budget -- but it does not get to ignore that
    /// budget, which exists to stop the subscription being spent faster than it
    /// refills, and which an explicit caller has no more right to overrun than
    /// anyone else.
    /// </summary>
    private async Task<ChatRouting<T>> RouteLocalOnlyAsync<T>(
        CreateChatCompletionCommand request,
        ChatBackends<T> backends)
    {
        // Image generation is the one thing the subscription genuinely cannot do, so
        // say so as a bad request rather than letting it fail as if it were a
        // capacity problem the caller could retry out of.
        if (request.Model.StartsWith("image", StringComparison.OrdinalIgnoreCase))
        {
            return Refuse<T>(
                "use_local_only cannot serve image models: local Claude Code generates text only.", 400);
        }

        if (!_localClaude.IsConfigured)
        {
            return Refuse<T>(
                "use_local_only was requested but local Claude is not enabled on this gateway.", 503);
        }

        var health = _localClaude.Health;
        if (!health.CanServe)
        {
            _logger.LogInformation(
                "Local-only request refused: local Claude is {Status}", health.Describe());

            return Refuse<T>($"use_local_only was requested but local Claude is {health.Describe()}.", 503);
        }

        var local = await backends.Local(LocalClaudePriority.Premium);
        var outcome = backends.Outcome(local);

        // Recorded under the lane that was asked for, not under a type of its own.
        // Which backend served it is already carried by cost_source; inventing a
        // "local" model type here would drop these rows out of the lane breakdown
        // the dashboard and the weekly report are built on.
        //
        // Logged either way. A local-only request that failed still belongs in the
        // project's history, or the analytics would show traffic simply vanishing.
        var (loggedType, loggedTier) = DescribeLane(request.Model);
        var route = new ChatRoute(null, loggedType, loggedTier, null, [], ServedLocally: true);

        if (!outcome.Success)
        {
            _logger.LogWarning(
                "Local-only request failed and has no fallback by design ({Reason})",
                outcome.ErrorMessage);

            return new ChatRouting<T>
            {
                Attempt = local,
                Route = route,
                Error = outcome.ErrorMessage ?? "local Claude could not serve this request.",
                StatusCode = 503
            };
        }

        return new ChatRouting<T> { Attempt = local, Route = route };
    }

    /// <summary>
    /// What this request would have cost had it gone to a paid model, using the
    /// model it would actually have reached first and the tokens it actually used.
    ///
    /// The CLI reports what Claude would have billed, which is only a fair stand-in
    /// for the premium lane, where the model it displaces is priced similarly. For
    /// moderate it is out by more than an order of magnitude, and recording it there
    /// would inflate the "avoided" column in the weekly report into fiction. Returns
    /// null when the model's pricing is unknown, so the caller keeps what it had.
    /// </summary>
    public static decimal? EstimateAvoidedCost(
        IReadOnlyList<(string ModelId, CachedModel? Model)> candidates,
        ChatCompletionResult local)
    {
        var model = candidates.Select(c => c.Model).FirstOrDefault(m => m is not null);
        if (model is null) return null;

        if (!decimal.TryParse(model.PromptPrice, NumberStyles.Float, CultureInfo.InvariantCulture, out var prompt) ||
            !decimal.TryParse(model.CompletionPrice, NumberStyles.Float, CultureInfo.InvariantCulture, out var completion) ||
            prompt < 0 || completion < 0)
        {
            return null;
        }

        // Both prices are per token, as OpenRouter publishes them.
        var estimate =
            prompt * local.Usage.PromptTokens +
            completion * local.Usage.CompletionTokens;

        return estimate > 0 ? estimate : null;
    }

    // Number of backup models to try after the primary for "paid"/"image" virtual models.
    private static readonly int PaidFallbackCount =
        int.TryParse(Environment.GetEnvironmentVariable("PAID_FALLBACK_COUNT"), out var n) && n >= 0 ? n : 3;

    /// <summary>
    /// Builds the ordered list of models to attempt. For the "paid", "paid:&lt;tier&gt;" and
    /// "image" virtual models the selected model is tried first, followed by the rest of that
    /// tier's chain as backups. Specific model IDs resolve to a single candidate (no silent
    /// substitution).
    /// </summary>
    private List<(string ModelId, CachedModel? Model)> BuildPaidCandidates(
        string requestedModel,
        ResolvedModel resolved)
    {
        var candidates = new List<(string ModelId, CachedModel? Model)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(CachedModel? m)
        {
            if (m == null || string.IsNullOrEmpty(m.Id) || !seen.Add(m.Id))
                return;
            candidates.Add((m.Id, m));
        }

        // "paid" and "paid:<tier>" both land here; bare "paid" resolves to the Low tier so its
        // behaviour is unchanged.
        if (PaidTierExtensions.TryParseModel(requestedModel, out var tier))
        {
            Add(resolved.Model ?? _modelCacheService.GetSelectedPaidModel(tier));
            foreach (var m in _modelCacheService.GetPaidModels(tier)) // curated first, then cheapest
            {
                if (candidates.Count > PaidFallbackCount) break;
                Add(m);
            }
        }
        else if (requestedModel.Equals("image", StringComparison.OrdinalIgnoreCase))
        {
            Add(resolved.Model ?? _modelCacheService.GetSelectedImageModel());
            foreach (var m in _modelCacheService.GetImageModels()) // ranked cheapest-first
            {
                if (candidates.Count > PaidFallbackCount) break;
                Add(m);
            }
        }
        else if (resolved.ModelId != null)
        {
            // Specific model ID - single attempt, no substitution.
            candidates.Add((resolved.ModelId, resolved.Model));
            return candidates;
        }

        // Push models currently on cooldown (recently rate-limited) to the back so we prefer
        // healthy models first, while still keeping them as a last resort if all else fails.
        return candidates
            .OrderBy(c => _modelCooldownCache.IsRateLimited(c.ModelId) ? 1 : 0)
            .ToList();
    }

    /// <summary>
    /// Calls OpenRouter for each candidate in order, returning the first success. On failure
    /// it advances to the next candidate. Returns the last failure if all candidates fail.
    /// </summary>
    private async Task<(T Attempt, string ModelId, CachedModel? Model)> ExecuteWithModelFallbackAsync<T>(
        List<(string ModelId, CachedModel? Model)> candidates,
        ChatBackends<T> backends)
    {
        for (int i = 0; i < candidates.Count; i++)
        {
            var (candidateId, candidateModel) = candidates[i];
            var attempt = await backends.Paid(candidateId);
            var result = backends.Outcome(attempt);

            if (result.Success)
            {
                if (i > 0)
                {
                    _logger.LogInformation(
                        "Request succeeded with backup model {Model} (attempt {Attempt}/{Total})",
                        candidateId, i + 1, candidates.Count);
                }
                return (attempt, candidateId, candidateModel);
            }

            // Park rate-limited models so subsequent requests skip them during their cooldown.
            if (result.HttpStatusCode == 429)
            {
                _modelCooldownCache.MarkRateLimited(candidateId);
            }

            _logger.LogWarning(
                "Model {Model} failed ({StatusCode}): {Error}. {Remaining} backup model(s) remaining",
                candidateId, result.HttpStatusCode, result.ErrorMessage, candidates.Count - i - 1);

            if (i == candidates.Count - 1)
            {
                if (candidates.Count > 1)
                {
                    _logger.LogError("All {Count} candidate models failed for the request", candidates.Count);
                }

                return (attempt, candidateId, candidateModel);
            }

            await DiscardAsync(attempt);
        }

        // Callers check for an empty list before getting here.
        throw new InvalidOperationException("No candidate models to try.");
    }

    private ResolvedModel ResolveModel(string requestedModel)
    {
        // Handle "free" keyword
        if (requestedModel.Equals("free", StringComparison.OrdinalIgnoreCase))
        {
            var model = _modelCacheService.GetSelectedFreeModel();
            return new ResolvedModel(model?.Id, "free", model);
        }

        // Handle "paid" and "paid:<tier>". Bare "paid" maps to the Low tier, so callers that
        // predate tiers keep exactly their previous behaviour.
        if (PaidTierExtensions.TryParseModel(requestedModel, out var tier))
        {
            var model = _modelCacheService.GetSelectedPaidModel(tier);
            return new ResolvedModel(model?.Id, "paid", model, null, tier);
        }

        // A "paid:" prefix that did not parse is a caller mistake worth reporting rather than
        // silently falling through to the specific-model lookup.
        if (requestedModel.StartsWith("paid:", StringComparison.OrdinalIgnoreCase))
        {
            return new ResolvedModel(null, "unknown", null,
                $"Unknown paid tier '{requestedModel}'. Valid values are 'paid', 'paid:low', 'paid:moderate' and 'paid:premium'.");
        }

        if (requestedModel.Equals("image", StringComparison.OrdinalIgnoreCase))
        {
            var model = _modelCacheService.GetSelectedImageModel();
            return new ResolvedModel(model?.Id, "image", model);
        }

        // Look up specific model in legacy cache (OpenRouter models)
        var cachedModel = _modelCacheService.GetModelById(requestedModel);
        if (cachedModel != null)
        {
            var type = cachedModel.IsImageModel ? "image" : cachedModel.IsFree ? "free" : "paid";
            // Attribute a directly-requested paid model to the tier it belongs to.
            return new ResolvedModel(cachedModel.Id, type, cachedModel, null, cachedModel.Tier);
        }

        // Check provider model cache for strict validation
        var providers = _providerModelCache.FindProvidersForModel(requestedModel);
        if (providers.Count > 0)
        {
            _logger.LogDebug("Model '{Model}' found on providers: {Providers}",
                requestedModel, string.Join(", ", providers));
            return new ResolvedModel(requestedModel, "specific", null);
        }

        // Check if it looks like an OpenRouter model format (contains /)
        if (requestedModel.Contains('/'))
        {
            // OpenRouter format - check if it's in the openrouter provider cache
            if (_providerModelCache.IsValidModel("openrouter", requestedModel))
            {
                return new ResolvedModel(requestedModel, "paid", null);
            }
        }

        // Model not found in any cache - return error for strict validation
        var summary = _providerModelCache.GetCacheSummary();
        if (summary.TotalModelCount > 0)
        {
            // Cache is populated, so this is a genuinely invalid model
            _logger.LogWarning("Model '{Model}' not found in any provider cache", requestedModel);
            return new ResolvedModel(null, "unknown", null,
                $"Model '{requestedModel}' is not available. Use GET /v1/models to see available models.");
        }

        // Cache not yet populated - allow pass-through for backwards compatibility
        _logger.LogDebug("Provider model cache not yet populated, allowing pass-through for '{Model}'", requestedModel);
        return new ResolvedModel(requestedModel, "unknown", null);
    }
}
