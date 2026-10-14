using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Freeway.Domain.Entities;
using Freeway.Domain.Interfaces;
using Freeway.Infrastructure.Streaming;
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
            var deadlineSeconds = spillover ? _spilloverTimeoutSeconds : _timeoutSeconds;

            request.Content = JsonContent.Create(new
            {
                messages = messages.Select(m => new { role = m.Role, content = m.Content }).ToList(),
                maxTokens = options?.MaxTokens,
                priority = spillover ? "spillover" : "premium",

                // The bridge measures how long calls take on its own host and refuses
                // work it cannot finish in time, rather than starting something this
                // side will abandon. Without it a slow host turns every spillover
                // attempt into the deadline added to a paid call.
                deadlineMs = deadlineSeconds * 1000
            });

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(deadlineSeconds));

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

                // "busy", "budget" and "too_slow" are the bridge working as designed,
                // not failing: it is declining spillover to protect premium's share,
                // or work it could not finish inside the caller's deadline. Treating
                // them as evidence of ill health would let cheap traffic mark the
                // bridge unavailable and lock premium out of the subscription
                // entirely -- the exact outcome the reserve exists to prevent.
                var deliberate = IsDeliberate(parsed?.ReasonCode);

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

            var (text, completionTokens, finishReason) = ApplyTokenLimit(
                parsed.Text ?? "", parsed.CompletionTokens, options?.MaxTokens, parsed.StopReason);

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
                        Message = new ChatMessage { Role = "assistant", Content = text },
                        FinishReason = finishReason
                    }
                ],
                Usage = new ChatCompletionUsage
                {
                    PromptTokens = parsed.PromptTokens,
                    CompletionTokens = completionTokens,
                    TotalTokens = parsed.PromptTokens + completionTokens
                },
                FinishReason = finishReason,
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

    public async Task<ChatCompletionStream> StreamAsync(
        List<ChatMessage> messages,
        ChatCompletionOptions? options = null,
        LocalClaudePriority priority = LocalClaudePriority.Premium,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var spillover = priority == LocalClaudePriority.Spillover;

        if (!IsConfigured)
        {
            return ChatCompletionStream.Failed(Failed("local Claude is not configured", stopwatch, spillover));
        }

        var result = UpstreamStream.NewResult("claude-code", "claude-code");
        result.Id = $"chatcmpl-local-{Guid.NewGuid():N}";

        var state = new BridgeStreamState();
        var stream = await ChatCompletionStream.OpenAsync(
            result, ReadBridgeStreamAsync(messages, options, spillover, result, state, cancellationToken));

        if (stream.Opened || state.Reported)
        {
            return stream;
        }

        // A failure the bridge reported has already been judged and logged where it was
        // read. Anything else never got that far -- the bridge was unreachable, or
        // slower than the caller would wait -- and is treated as CompleteAsync treats it:
        // a spillover deadline says nothing about premium, so only premium marks health.
        if (!spillover)
        {
            SetHealth(new LocalClaudeHealth
            {
                State = LocalClaudeState.Unavailable,
                Detail = stream.Result.ErrorMessage,
                CheckedAt = DateTime.UtcNow
            });
        }

        return ChatCompletionStream.Failed(Failed(
            $"could not reach the bridge: {stream.Result.ErrorMessage}", stopwatch, spillover));
    }

    /// <summary>
    /// Reads the bridge's streaming reply: a "delta" line per piece of text, then one
    /// "done" or "error" line with the same fields a non-streaming reply carries.
    /// </summary>
    private async IAsyncEnumerable<ChatCompletionDelta> ReadBridgeStreamAsync(
        List<ChatMessage> messages,
        ChatCompletionOptions? options,
        bool spillover,
        ChatCompletionResult result,
        BridgeStreamState state,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var fullTimeout = TimeSpan.FromSeconds(_timeoutSeconds);
        var deadlineSeconds = spillover ? _spilloverTimeoutSeconds : _timeoutSeconds;

        using var idle = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        idle.CancelAfter(TimeSpan.FromSeconds(deadlineSeconds));

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/complete");
        if (!string.IsNullOrWhiteSpace(_token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);

        request.Content = JsonContent.Create(new
        {
            messages = messages.Select(m => new { role = m.Role, content = m.Content }).ToList(),
            maxTokens = options?.MaxTokens,
            priority = spillover ? "spillover" : "premium",
            deadlineMs = deadlineSeconds * 1000,
            stream = true
        });

        using var response = await UpstreamStream.SendAsync(
            _httpClient, request, "claude-bridge", _logger, idle.Token);

        // max_tokens, enforced as the text arrives. The non-streaming path cuts by the
        // share of the answer the cap allows, which needs the final token count; a
        // stream has to decide before that exists, so it counts characters at the
        // usual four to a token. Past the cap nothing more is passed on, but the call
        // is still read to the end: the bridge only charges its budget for calls that
        // finish, and the token counts arrive on the last line.
        var maxTokens = options?.MaxTokens is > 0 ? options.MaxTokens : null;
        var charBudget = maxTokens * CharsPerToken;
        var sent = 0;
        var capped = false;
        var started = false;

        // The first wait is bounded by the caller's deadline, which for spillover is
        // seconds. Lines after that are allowed the full timeout between them.
        await foreach (var line in UpstreamStream.ReadLinesAsync(response, idle, fullTimeout, idle.Token))
        {
            var parsed = JsonSerializer.Deserialize<BridgeStreamLine>(line, JsonOptions);
            if (parsed is null)
            {
                continue;
            }

            if (parsed.Type == "delta")
            {
                if (capped || string.IsNullOrEmpty(parsed.Text))
                {
                    continue;
                }

                started = true;
                var text = parsed.Text;
                if (charBudget is { } budget && sent + text.Length >= budget)
                {
                    capped = true;
                    var keep = Math.Max(0, budget - sent);
                    sent += keep;
                    yield return new ChatCompletionDelta { Content = text[..keep], FinishReason = "length" };
                    continue;
                }

                sent += text.Length;
                yield return new ChatCompletionDelta { Content = text };
                continue;
            }

            if (parsed.Type == "error" || !parsed.Ok)
            {
                state.Reported = true;
                throw new ChatCompletionStreamException(BridgeFailure(parsed, spillover));
            }

            // "done": the same envelope CompleteAsync reads.
            _logger.LogInformation(
                "{Lane} request streamed by local Claude ({Model}) in {Duration}ms, {Prompt}+{Completion} tokens, no charge",
                spillover ? "Spillover" : "Premium",
                parsed.Model ?? "unknown", parsed.DurationMs ?? 0,
                parsed.PromptTokens, parsed.CompletionTokens);

            var completionTokens = capped && maxTokens is { } cap
                ? Math.Min(cap, parsed.CompletionTokens)
                : parsed.CompletionTokens;

            result.Model = parsed.Model ?? "claude-code";
            result.UpstreamProvider = parsed.Model;
            result.Usage = new ChatCompletionUsage
            {
                PromptTokens = parsed.PromptTokens,
                CompletionTokens = completionTokens,
                TotalTokens = parsed.PromptTokens + completionTokens
            };

            // Nothing is billed: it comes out of a subscription. What the CLI reports
            // is the list price avoided, recorded separately as money not spent.
            result.CostUsd = 0m;
            result.CostSource = "subscription";
            result.AvoidedCostUsd = parsed.ListCostUsd;

            if (!capped)
            {
                yield return new ChatCompletionDelta
                {
                    // An empty answer still has to open the stream with something.
                    Content = started ? null : "",
                    FinishReason = MapStopReason(parsed.StopReason)
                };
            }

            yield break;
        }

        throw new ChatCompletionStreamException("the bridge closed the stream before the call finished");
    }

    /// <summary>
    /// Turns a bridge-reported failure into a reason, applying the same judgement
    /// CompleteAsync does about whether it says anything about the bridge's health.
    /// </summary>
    private string BridgeFailure(BridgeCompletion parsed, bool spillover)
    {
        var reason = parsed.Reason ?? "bridge reported a failure without a reason";
        var deliberate = IsDeliberate(parsed.ReasonCode);

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

        return Failed(reason, Stopwatch.StartNew(), spillover, deliberate).ErrorMessage!;
    }

    private static bool IsDeliberate(string? reasonCode) => reasonCode is "busy" or "budget" or "too_slow";

    /// <summary>Rough characters per token, for cutting a stream at max_tokens.</summary>
    private const int CharsPerToken = 4;

    /// <summary>
    /// Claude's stop reasons in the vocabulary OpenAI clients expect. "end_turn" passed
    /// through verbatim is not a finish_reason any of them know.
    /// </summary>
    private static string MapStopReason(string? stopReason) => stopReason switch
    {
        null or "end_turn" or "stop_sequence" => "stop",
        "max_tokens" => "length",
        "refusal" => "content_filter",
        _ => stopReason
    };

    /// <summary>Set once the bridge itself has reported how a stream failed.</summary>
    private sealed class BridgeStreamState
    {
        public bool Reported { get; set; }
    }

    /// <summary>
    /// Honours max_tokens on the local path, which the CLI cannot be relied on to do
    /// itself.
    ///
    /// The bridge passes the cap to Claude Code as an environment variable, but that
    /// was measured truncating at small values and being ignored at larger ones, so
    /// the contract is kept here instead: an over-long answer is cut and reported as
    /// finish_reason "length", exactly as a paid provider would report it. Callers
    /// asking for a bounded answer get one from every lane, not just the paid ones.
    ///
    /// Cutting after the fact does not save the tokens -- they were already generated
    /// and already charged against the hourly budget. Only the environment variable
    /// can do that, and only when Claude Code honours it.
    /// </summary>
    private static (string Text, int CompletionTokens, string FinishReason) ApplyTokenLimit(
        string text,
        int completionTokens,
        int? maxTokens,
        string? stopReason)
    {
        var finish = MapStopReason(stopReason);

        if (maxTokens is not > 0 || completionTokens <= maxTokens || text.Length == 0)
        {
            return (text, completionTokens, finish);
        }

        // Cut by the share of the answer the caller allowed for. Characters are a
        // proxy for tokens, which is approximate by nature -- the alternative is a
        // tokeniser the gateway does not otherwise need, for a path that is already
        // best-effort.
        var keep = (int)Math.Floor(text.Length * (maxTokens.Value / (double)completionTokens));
        keep = Math.Clamp(keep, 1, text.Length);

        return (text[..keep], maxTokens.Value, "length");
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

    private class BridgeStreamLine : BridgeCompletion
    {
        /// <summary>"delta", "done" or "error".</summary>
        public string? Type { get; set; }
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
