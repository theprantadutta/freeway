using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Freeway.Domain.Entities;
using Freeway.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Freeway.Infrastructure.Services;

/// <summary>
/// Talks to the claude-bridge sidecar on the host.
///
/// Treated throughout as a backend that may simply not be there. It is a cost
/// optimisation for one lane, not a dependency: every failure is caught, logged with
/// a reason, and turned into an unsuccessful result for the caller to route around.
/// </summary>
public class LocalClaudeService : ILocalClaudeService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<LocalClaudeService> _logger;

    private readonly bool _enabled;
    private readonly string _baseUrl;
    private readonly string _token;
    private readonly int _timeoutSeconds;
    private readonly int _spilloverTimeoutSeconds;

    private readonly ILocalClaudeHealthCache _healthCache;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public LocalClaudeService(
        HttpClient httpClient,
        ILocalClaudeHealthCache healthCache,
        ILogger<LocalClaudeService> logger)
    {
        _httpClient = httpClient;
        _healthCache = healthCache;
        _logger = logger;

        _enabled = bool.TryParse(Environment.GetEnvironmentVariable("LOCAL_CLAUDE_ENABLED"), out var e) && e;
        // Defaults to the compose service name, so the usual setup needs no URL at all.
        _baseUrl = (Environment.GetEnvironmentVariable("LOCAL_CLAUDE_URL")
                    ?? "http://claude-bridge:8787").TrimEnd('/');
        _token = Environment.GetEnvironmentVariable("LOCAL_CLAUDE_TOKEN") ?? "";
        _timeoutSeconds = int.TryParse(Environment.GetEnvironmentVariable("LOCAL_CLAUDE_TIMEOUT_SECONDS"), out var t)
            ? t
            : 150;

        // Spillover waits seconds, not minutes. It is only worth taking when it is
        // faster than the paid model it replaces; past this it has already lost, and
        // the request is better off going to the model it would have used anyway.
        _spilloverTimeoutSeconds =
            int.TryParse(Environment.GetEnvironmentVariable("LOCAL_CLAUDE_SPILLOVER_TIMEOUT_SECONDS"), out var st)
                ? st
                : 5;
    }

    // The token is optional: on the private compose network the bridge publishes no
    // ports, so there is nothing for a shared secret to protect against. It is still
    // sent when set, for anyone exposing the bridge more widely.
    public bool IsConfigured => _enabled && !string.IsNullOrWhiteSpace(_baseUrl);

    public LocalClaudeHealth Health => _healthCache.Current;

    private void SetHealth(LocalClaudeHealth health) => _healthCache.Set(health);

    public async Task<LocalClaudeHealth> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            var off = new LocalClaudeHealth
            {
                State = LocalClaudeState.Unknown,
                Detail = "LOCAL_CLAUDE_ENABLED is off",
                CheckedAt = DateTime.UtcNow
            };
            SetHealth(off);
            return off;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/health");
            if (!string.IsNullOrWhiteSpace(_token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(_timeoutSeconds));

            var response = await _httpClient.SendAsync(request, cts.Token);
            var body = await response.Content.ReadAsStringAsync(cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                return Record(LocalClaudeState.Unavailable,
                    $"bridge returned {(int)response.StatusCode}");
            }

            var parsed = JsonSerializer.Deserialize<BridgeHealth>(body, JsonOptions);
            if (parsed is null)
            {
                return Record(LocalClaudeState.Unavailable, "bridge returned an unreadable health body");
            }

            var state = parsed.State?.ToLowerInvariant() switch
            {
                "available" => LocalClaudeState.Available,
                "rate_limited" => LocalClaudeState.RateLimited,
                _ => LocalClaudeState.Unavailable
            };

            var health = new LocalClaudeHealth
            {
                State = state,
                Detail = parsed.Detail,
                LatencyMs = parsed.LatencyMs,
                Model = parsed.Model,
                Busy = parsed.Busy,
                Budget = parsed.Budget,
                CheckedAt = DateTime.UtcNow
            };
            SetHealth(health);

            if (state == LocalClaudeState.Available)
                _logger.LogInformation("Local Claude {Status}", health.Describe());
            else
                _logger.LogWarning("Local Claude {Status}; premium will use a paid model", health.Describe());

            return health;
        }
        catch (Exception ex)
        {
            return Record(LocalClaudeState.Unavailable, $"could not reach the bridge: {ex.Message}");
        }
    }

    private LocalClaudeHealth Record(LocalClaudeState state, string detail)
    {
        var health = new LocalClaudeHealth
        {
            State = state,
            Detail = detail,
            CheckedAt = DateTime.UtcNow
        };
        SetHealth(health);
        _logger.LogWarning("Local Claude {Status}; premium will use a paid model", health.Describe());
        return health;
    }

    public async Task<ChatCompletionResult> CompleteAsync(
        List<ChatMessage> messages,
        ChatCompletionOptions? options = null,
        LocalClaudePriority priority = LocalClaudePriority.Premium,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var spillover = priority == LocalClaudePriority.Spillover;

        if (!IsConfigured)
        {
            return Failed("local Claude is not configured", stopwatch, spillover);
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/complete");
            if (!string.IsNullOrWhiteSpace(_token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
            request.Content = JsonContent.Create(new
            {
                messages = messages.Select(m => new { role = m.Role, content = m.Content }).ToList(),
                maxTokens = options?.MaxTokens,
                priority = spillover ? "spillover" : "premium"
            });

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(
                spillover ? _spilloverTimeoutSeconds : _timeoutSeconds));

            var response = await _httpClient.SendAsync(request, cts.Token);
            var body = await response.Content.ReadAsStringAsync(cts.Token);
            stopwatch.Stop();

            if (!response.IsSuccessStatusCode)
            {
                return Failed($"bridge returned {(int)response.StatusCode}", stopwatch);
            }

            var parsed = JsonSerializer.Deserialize<BridgeCompletion>(body, JsonOptions);

            if (parsed is null || !parsed.Ok)
            {
                var reason = parsed?.Reason ?? "bridge returned an unreadable body";

                // "busy" and "budget" are the bridge working as designed, not failing:
                // it is declining spillover to protect premium's share. Treating them
                // as evidence of ill health would let cheap traffic mark the bridge
                // unavailable and lock premium out of the subscription entirely --
                // the exact outcome the reserve exists to prevent.
                var deliberate = parsed?.ReasonCode is "busy" or "budget";

                if (!deliberate)
                {
                    SetHealth(new LocalClaudeHealth
                    {
                        State = reason.Contains("rate", StringComparison.OrdinalIgnoreCase)
                            ? LocalClaudeState.RateLimited
                            : LocalClaudeState.Unavailable,
                        Detail = reason,
                        CheckedAt = DateTime.UtcNow
                    });
                }

                return Failed(reason, stopwatch, spillover, deliberate);
            }

            _logger.LogInformation(
                "{Lane} request served by local Claude ({Model}) in {Duration}ms, {Prompt}+{Completion} tokens, no charge",
                spillover ? "Spillover" : "Premium",
                parsed.Model ?? "unknown", parsed.DurationMs ?? stopwatch.ElapsedMilliseconds,
                parsed.PromptTokens, parsed.CompletionTokens);

            return new ChatCompletionResult
            {
                Id = $"chatcmpl-local-{Guid.NewGuid():N}",
                Model = parsed.Model ?? "claude-code",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Choices =
                [
                    new ChatCompletionChoice
                    {
                        Index = 0,
                        Message = new ChatMessage { Role = "assistant", Content = parsed.Text ?? "" },
                        FinishReason = parsed.StopReason ?? "stop"
                    }
                ],
                Usage = new ChatCompletionUsage
                {
                    PromptTokens = parsed.PromptTokens,
                    CompletionTokens = parsed.CompletionTokens,
                    TotalTokens = parsed.PromptTokens + parsed.CompletionTokens
                },
                FinishReason = parsed.StopReason ?? "stop",
                Success = true,
                ProviderName = "claude-code",
                UpstreamProvider = parsed.Model,

                // Nothing is billed for this: it comes out of a subscription. The
                // amount the CLI reports is what the same work would have cost at
                // list price, recorded separately as money not spent.
                CostUsd = 0m,
                CostSource = "subscription",
                AvoidedCostUsd = parsed.ListCostUsd,

                ResponseTimeMs = parsed.DurationMs ?? (int)stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            // A spillover call gives up after a few seconds. Hitting that deadline
            // says the bridge was slow for a lane that refuses to wait, not that it
            // is unfit for premium, which is allowed minutes -- so it leaves the
            // cached verdict alone.
            if (!spillover)
            {
                SetHealth(new LocalClaudeHealth
                {
                    State = LocalClaudeState.Unavailable,
                    Detail = ex.Message,
                    CheckedAt = DateTime.UtcNow
                });
            }

            return Failed($"could not reach the bridge: {ex.Message}", stopwatch, spillover);
        }
    }

    private ChatCompletionResult Failed(
        string reason,
        Stopwatch stopwatch,
        bool spillover = false,
        bool deliberate = false)
    {
        stopwatch.Stop();

        // A declined spillover is the normal case, many times an hour. Logging it at
        // warning would bury the failures that actually want attention.
        if (spillover || deliberate)
        {
            _logger.LogDebug("Local Claude declined ({Reason}); using a paid model", reason);
        }
        else
        {
            _logger.LogWarning("Could not use local Claude ({Reason}); falling back to a paid model", reason);
        }

        return new ChatCompletionResult
        {
            Success = false,
            // "declined" and "unavailable" are different things, and the log is read
            // to tell them apart: one is the budget working, the other wants looking at.
            ErrorMessage = (spillover || deliberate)
                ? $"local Claude declined: {reason}"
                : $"local Claude unavailable: {reason}",
            ProviderName = "claude-code",
            ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds
        };
    }

    private class BridgeHealth
    {
        public bool Ok { get; set; }
        public string? State { get; set; }
        public string? Detail { get; set; }
        public int? LatencyMs { get; set; }
        public string? Model { get; set; }
        public bool Busy { get; set; }
        public LocalClaudeBudget? Budget { get; set; }
    }

    private class BridgeCompletion
    {
        public bool Ok { get; set; }
        public string? Reason { get; set; }

        /// <summary>"busy" or "budget" when the bridge declined on purpose.</summary>
        public string? ReasonCode { get; set; }
        public string? Text { get; set; }
        public string? Model { get; set; }
        public decimal ListCostUsd { get; set; }
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public string? StopReason { get; set; }
        public int? DurationMs { get; set; }
    }
}
