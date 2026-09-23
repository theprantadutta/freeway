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
        _baseUrl = (Environment.GetEnvironmentVariable("LOCAL_CLAUDE_URL") ?? "").TrimEnd('/');
        _token = Environment.GetEnvironmentVariable("LOCAL_CLAUDE_TOKEN") ?? "";
        _timeoutSeconds = int.TryParse(Environment.GetEnvironmentVariable("LOCAL_CLAUDE_TIMEOUT_SECONDS"), out var t)
            ? t
            : 150;
    }

    public bool IsConfigured =>
        _enabled && !string.IsNullOrWhiteSpace(_baseUrl) && !string.IsNullOrWhiteSpace(_token);

    public LocalClaudeHealth Health => _healthCache.Current;

    private void SetHealth(LocalClaudeHealth health) => _healthCache.Set(health);

    public async Task<LocalClaudeHealth> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            var off = new LocalClaudeHealth
            {
                State = LocalClaudeState.Unknown,
                Detail = "LOCAL_CLAUDE_ENABLED is off, or URL/token are not set",
                CheckedAt = DateTime.UtcNow
            };
            SetHealth(off);
            return off;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/health");
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
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        if (!IsConfigured)
        {
            return Failed("local Claude is not configured", stopwatch);
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/complete");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
            request.Content = JsonContent.Create(new
            {
                messages = messages.Select(m => new { role = m.Role, content = m.Content }).ToList(),
                maxTokens = options?.MaxTokens
            });

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(_timeoutSeconds));

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
                // A refusal here is the freshest evidence we have, so the cached
                // verdict is downgraded rather than left saying "available".
                var reason = parsed?.Reason ?? "bridge returned an unreadable body";
                SetHealth(new LocalClaudeHealth
                {
                    State = reason.Contains("rate", StringComparison.OrdinalIgnoreCase)
                        ? LocalClaudeState.RateLimited
                        : LocalClaudeState.Unavailable,
                    Detail = reason,
                    CheckedAt = DateTime.UtcNow
                });
                return Failed(reason, stopwatch);
            }

            _logger.LogInformation(
                "Premium request served by local Claude ({Model}) in {Duration}ms, {Prompt}+{Completion} tokens, ${Avoided} of list price avoided",
                parsed.Model ?? "unknown", parsed.DurationMs ?? stopwatch.ElapsedMilliseconds,
                parsed.PromptTokens, parsed.CompletionTokens, parsed.ListCostUsd);

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
            SetHealth(new LocalClaudeHealth
            {
                State = LocalClaudeState.Unavailable,
                Detail = ex.Message,
                CheckedAt = DateTime.UtcNow
            });
            return Failed($"could not reach the bridge: {ex.Message}", stopwatch);
        }
    }

    private ChatCompletionResult Failed(string reason, Stopwatch stopwatch)
    {
        stopwatch.Stop();
        _logger.LogWarning("Could not use local Claude ({Reason}); falling back to a paid model", reason);

        return new ChatCompletionResult
        {
            Success = false,
            ErrorMessage = $"local Claude unavailable: {reason}",
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
    }

    private class BridgeCompletion
    {
        public bool Ok { get; set; }
        public string? Reason { get; set; }
        public string? Text { get; set; }
        public string? Model { get; set; }
        public decimal ListCostUsd { get; set; }
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public string? StopReason { get; set; }
        public int? DurationMs { get; set; }
    }
}
