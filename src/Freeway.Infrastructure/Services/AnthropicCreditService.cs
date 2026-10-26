using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using Freeway.Domain.Common;
using Freeway.Domain.Entities;
using Freeway.Domain.Interfaces;
using Freeway.Infrastructure.Streaming;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using DomainMessage = Freeway.Domain.Entities.ChatMessage;

namespace Freeway.Infrastructure.Services;

/// <summary>
/// Serves the paid lanes (premium, moderate and low by default) from the Claude API while the plan's monthly
/// credit lasts. See <see cref="IAnthropicCreditService"/> for the contract.
///
/// What it does not do matters as much as what it does. It never retries into a
/// bill: the workspace the key belongs to has nothing but the credit, and when the
/// count says the credit is (nearly) gone this stops being asked at all. It never
/// fails a request: every refusal becomes an unsuccessful result and the request
/// carries on to the bridge and then OpenRouter, exactly as it did before this existed.
/// </summary>
public class AnthropicCreditService : IAnthropicCreditService
{
    public const string ProviderName = "anthropic";

    private readonly AnthropicCreditLedger _ledger;
    private readonly IAppDbContext _db;
    private readonly IDateTimeService _clock;
    private readonly ILogger<AnthropicCreditService> _logger;

    internal static readonly Settings Config = Settings.FromEnvironment();

    // One client for the process: it owns an HttpClient and is safe to share.
    private static readonly Lazy<AnthropicClient> Client = new(() => new AnthropicClient
    {
        ApiKey = Config.ApiKey,
        BaseUrl = Config.BaseUrl,
        // No retries: the fallback is the retry. The bridge and then OpenRouter are
        // already good answers, and retrying here only makes the caller wait longer
        // for them when the API is having trouble.
        MaxRetries = 0,
        Timeout = TimeSpan.FromSeconds(Config.TimeoutSeconds),

        // A short limit on connecting, separate from the long one on answering: an
        // unreachable API should cost a request seconds, not the full timeout, while a
        // long answer that is genuinely arriving still gets all of it.
        HttpClient = new HttpClient(new SocketsHttpHandler
        {
            ConnectTimeout = TimeSpan.FromSeconds(10),
            PooledConnectionLifetime = TimeSpan.FromMinutes(5)
        })
        {
            Timeout = Timeout.InfiniteTimeSpan
        }
    });

    public AnthropicCreditService(
        AnthropicCreditLedger ledger,
        IAppDbContext db,
        IDateTimeService clock,
        ILogger<AnthropicCreditService> logger)
    {
        _ledger = ledger;
        _db = db;
        _clock = clock;
        _logger = logger;
    }

    public bool IsConfigured => Config.Enabled;

    public string? KeyFingerprint => Config.ApiKey.Length == 0 ? null : Fingerprint(Config.ApiKey);

    /// <summary>The first and last few characters of the key, for recognising it on the dashboard.</summary>
    internal static string KeyHint => Config.ApiKey.Length <= 18
        ? "set"
        : $"{Config.ApiKey[..13]}…{Config.ApiKey[^4..]}";

    internal static string Fingerprint(string key) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..32].ToLowerInvariant();

    public bool Serves(PaidTier tier) => IsConfigured && Config.Tiers.Contains(tier);

    public string ModelFor(PaidTier tier) => tier switch
    {
        PaidTier.Premium => Config.PremiumModel,
        PaidTier.Moderate => Config.ModerateModel,
        _ => Config.LowModel
    };

    public AnthropicCreditStatus Status => _ledger.Snapshot(IsConfigured, Config.StopAt, _clock.UtcNow);

    #region Completion

    public async Task<ChatCompletionResult> CompleteAsync(
        List<DomainMessage> messages,
        ChatCompletionOptions? options,
        PaidTier tier,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var model = ModelFor(tier);

        var request = BuildRequest(messages, options, tier, model, out var unsuitable);
        if (request is null)
        {
            return Failed(model, unsuitable!, stopwatch);
        }

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(Config.TimeoutSeconds));

            var message = await Client.Value.Messages.Create(request, timeout.Token);
            stopwatch.Stop();

            var usage = Count(message.Model.Raw(), message.Usage.InputTokens, message.Usage.OutputTokens,
                message.Usage.CacheCreationInputTokens, message.Usage.CacheReadInputTokens);
            Spend(usage.CostUsd);
            _ledger.RecordSuccess();

            var stopReason = message.StopReason?.Raw();
            if (stopReason == "refusal")
            {
                // Billed, but no answer. Another backend may well give one, so this
                // is a failure to route around rather than a reply to hand back.
                return Failed(model, "the model declined to answer (stop_reason: refusal)", stopwatch, cost: usage.CostUsd);
            }

            var text = string.Concat(message.Content
                .Select(b => b.Value)
                .OfType<TextBlock>()
                .Select(b => b.Text));

            var thinking = message.Usage.OutputTokensDetails?.ThinkingTokens ?? 0;
            var (reply, finish) = ApplyTokenLimit(text, usage.Output - thinking, options?.MaxTokens, FinishReason(stopReason));

            _logger.LogInformation(
                "{Tier} request served from the Anthropic credit ({Model}) in {Duration}ms, {Prompt}+{Completion} tokens, ${Cost:0.0000} of credit",
                tier, message.Model.Raw(), stopwatch.ElapsedMilliseconds, usage.Prompt, usage.Output, usage.CostUsd);

            return new ChatCompletionResult
            {
                Id = $"chatcmpl-{message.ID}",
                Model = message.Model.Raw(),
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Choices =
                [
                    new ChatCompletionChoice
                    {
                        Index = 0,
                        Message = new DomainMessage { Role = "assistant", Content = reply },
                        FinishReason = finish
                    }
                ],
                Usage = new ChatCompletionUsage
                {
                    PromptTokens = usage.Prompt,
                    CompletionTokens = usage.Output,
                    TotalTokens = usage.Prompt + usage.Output
                },
                FinishReason = finish,
                Success = true,
                ProviderName = ProviderName,
                UpstreamProvider = message.Model.Raw(),

                // Paid from the credit, not a card: no spend, and the credit's share
                // recorded on its own so the two never mix in the spend charts.
                CostUsd = 0m,
                CostSource = "credit",
                CreditCostUsd = usage.CostUsd,
                ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var (reason, status) = Classify(ex, cancellationToken);
            return Failed(model, reason, stopwatch, status);
        }
    }

    public async Task<ChatCompletionStream> StreamAsync(
        List<DomainMessage> messages,
        ChatCompletionOptions? options,
        PaidTier tier,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var model = ModelFor(tier);

        var request = BuildRequest(messages, options, tier, model, out var unsuitable);
        if (request is null)
        {
            return ChatCompletionStream.Failed(Failed(model, unsuitable!, stopwatch));
        }

        var result = UpstreamStream.NewResult(ProviderName, model);
        var stream = await ChatCompletionStream.OpenAsync(
            result, ReadStreamAsync(request, options, tier, result, cancellationToken));

        if (!stream.Opened)
        {
            // The stream failed before sending anything. Classification and blocking
            // happened inside the reader; only the log line is left.
            _logger.LogInformation("Anthropic credit stream did not open: {Reason}", result.ErrorMessage);
        }

        return stream;
    }

    private async IAsyncEnumerable<ChatCompletionDelta> ReadStreamAsync(
        MessageCreateParams request,
        ChatCompletionOptions? options,
        PaidTier tier,
        ChatCompletionResult result,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // An idle timeout, as for every other stream here: a long answer that keeps
        // arriving is working.
        var idleTimeout = TimeSpan.FromSeconds(Config.TimeoutSeconds);
        using var idle = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        idle.CancelAfter(idleTimeout);

        var stopwatch = Stopwatch.StartNew();
        var servedModel = request.Model.Raw();
        long input = 0, output = 0, cacheWrite = 0, cacheRead = 0, thinking = 0;
        string? stopReason = null;
        var sentAnything = false;

        // Text cut at the caller's max_tokens, as the bridge does: Anthropic's own
        // limit has to leave room for thinking, so it cannot be the caller's.
        var cap = options?.MaxTokens is > 0 ? options.MaxTokens.Value * CharsPerToken : (int?)null;
        var written = 0;
        var capped = false;

        var events = Client.Value.Messages.CreateStreaming(request, idle.Token).GetAsyncEnumerator(idle.Token);
        try
        {
            while (true)
            {
                bool more;
                try
                {
                    more = await events.MoveNextAsync();
                }
                catch (Exception ex) when (ex is not ChatCompletionStreamException)
                {
                    var (reason, status) = Classify(ex, cancellationToken);
                    throw new ChatCompletionStreamException(reason, status);
                }

                if (!more) break;
                idle.CancelAfter(idleTimeout);
                if (!sentAnything) _ledger.RecordSuccess();

                var ev = events.Current;
                if (ev.TryPickStart(out var start))
                {
                    servedModel = start.Message.Model.Raw();
                    result.Model = servedModel;
                    result.UpstreamProvider = servedModel;
                    result.Id = $"chatcmpl-{start.Message.ID}";
                    input = start.Message.Usage.InputTokens;
                    output = start.Message.Usage.OutputTokens;
                    cacheWrite = start.Message.Usage.CacheCreationInputTokens ?? 0;
                    cacheRead = start.Message.Usage.CacheReadInputTokens ?? 0;
                }
                else if (ev.TryPickContentBlockDelta(out var block))
                {
                    if (block.Delta.TryPickText(out var text) && !capped)
                    {
                        var piece = text.Text;
                        if (cap is { } limit && written + piece.Length > limit)
                        {
                            piece = piece[..Math.Max(0, limit - written)];
                            capped = true;
                        }

                        written += piece.Length;
                        sentAnything = true;
                        yield return new ChatCompletionDelta { Content = piece, FinishReason = capped ? "length" : null };
                        if (capped) break;
                    }
                    else if (block.Delta.TryPickThinking(out _))
                    {
                        sentAnything = true;
                        yield return new ChatCompletionDelta { IsReasoning = true };
                    }
                }
                else if (ev.TryPickDelta(out var delta))
                {
                    stopReason = delta.Delta.StopReason?.Raw() ?? stopReason;
                    output = Math.Max(output, delta.Usage.OutputTokens);
                    input = Math.Max(input, delta.Usage.InputTokens ?? 0);
                    cacheWrite = Math.Max(cacheWrite, delta.Usage.CacheCreationInputTokens ?? 0);
                    cacheRead = Math.Max(cacheRead, delta.Usage.CacheReadInputTokens ?? 0);
                    thinking = delta.Usage.OutputTokensDetails?.ThinkingTokens ?? thinking;
                }
            }

            if (stopReason == "refusal" && !sentAnything)
            {
                throw new ChatCompletionStreamException("the model declined to answer (stop_reason: refusal)");
            }

            if (!capped)
            {
                yield return new ChatCompletionDelta { FinishReason = stopReason == "refusal" ? "content_filter" : FinishReason(stopReason) };
            }
        }
        finally
        {
            await events.DisposeAsync();

            // Whatever was generated is billed, however the stream ended, so it is
            // counted however it ended too. A stream cut short (max_tokens reached
            // here, or the caller gone) never gets its final usage; the text sent is
            // the floor for what it produced.
            output = Math.Max(output, written / CharsPerToken);
            var usage = Count(servedModel, input, output, cacheWrite, cacheRead);
            Spend(usage.CostUsd);

            if (usage.Prompt + usage.Output > 0)
            {
                result.Usage = new ChatCompletionUsage
                {
                    PromptTokens = usage.Prompt,
                    CompletionTokens = usage.Output,
                    TotalTokens = usage.Prompt + usage.Output
                };
            }

            result.CostUsd = 0m;
            result.CostSource = "credit";
            result.CreditCostUsd = usage.CostUsd;

            _logger.LogInformation(
                "{Tier} stream served from the Anthropic credit ({Model}) in {Duration}ms, {Prompt}+{Completion} tokens ({Thinking} thinking), ${Cost:0.0000} of credit",
                tier, servedModel, stopwatch.ElapsedMilliseconds, usage.Prompt, usage.Output, thinking, usage.CostUsd);
        }
    }

    #endregion

    #region Request shape

    // Rough characters per token, for cutting text at a caller's max_tokens.
    private const int CharsPerToken = 4;

    private MessageCreateParams? BuildRequest(
        List<DomainMessage> messages,
        ChatCompletionOptions? options,
        PaidTier tier,
        string model,
        out string? unsuitable)
    {
        unsuitable = null;

        // System (and "developer", already folded into it) goes in the top-level
        // field; everything else is the conversation. Anything that is not a
        // recognised role is someone talking to the model, which is a user turn.
        var system = string.Join("\n\n", messages
            .Where(m => m.Role.Equals("system", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(m.Content))
            .Select(m => m.Content));

        // Empty turns are dropped: the API rejects empty text, and they say nothing.
        var turns = messages
            .Where(m => !m.Role.Equals("system", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(m.Content))
            .ToList();

        if (turns.Count == 0)
        {
            unsuitable = "the request has no user message";
            return null;
        }

        if (turns[^1].Role.Equals("assistant", StringComparison.OrdinalIgnoreCase))
        {
            // A trailing assistant turn is a prefill, which these models reject.
            unsuitable = "the conversation ends with an assistant turn (prefill), which these models reject";
            return null;
        }

        var conversation = turns.Select(m => m.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase)
                ? (MessageParam)new AssistantMessageParam { Content = m.Content }
                : new UserMessageParam { Content = m.Content })
            .ToList();

        // The caller's max_tokens is a limit on the answer. Anthropic's covers thinking
        // as well, so it gets headroom on top, and the answer is cut to the caller's
        // limit afterwards.
        var maxTokens = options?.MaxTokens is > 0
            ? options.MaxTokens.Value + Config.ThinkingHeadroomTokens
            : Config.DefaultMaxTokens;

        var effort = EffortFor(options?.Reasoning?.Effort, tier);

        // temperature, top_p and the penalties are not sent: current Claude models
        // reject sampling parameters outright.
        //
        // Optional fields are only set when there is something to send: the SDK sends a
        // property that was assigned null as an explicit null, and the API rejects
        // "system": null outright.
        var request = new MessageCreateParams
        {
            Model = model,
            MaxTokens = maxTokens,
            Messages = conversation
        };

        if (!string.IsNullOrEmpty(system)) request = request with { System = system };
        if (options?.Stop is { Count: > 0 } stop) request = request with { StopSequences = stop };
        if (effort is not null) request = request with { OutputConfig = new OutputConfig { Effort = effort.Value } };
        if (Config.PromptCache) request = request with { CacheControl = new CacheControlEphemeral() };

        return request;
    }

    /// <summary>
    /// The caller's reasoning effort wins; otherwise the lane's configured default, or
    /// the model's own when none is configured. OpenAI-style "minimal"/"none" have no
    /// Claude equivalent and are taken as low.
    /// </summary>
    private static Effort? EffortFor(string? requested, PaidTier tier)
    {
        var value = !string.IsNullOrWhiteSpace(requested)
            ? requested
            : tier switch
            {
                PaidTier.Premium => Config.PremiumEffort,
                PaidTier.Moderate => Config.ModerateEffort,
                _ => Config.LowEffort
            };

        return value?.Trim().ToLowerInvariant() switch
        {
            "minimal" or "none" or "low" => Effort.Low,
            "medium" => Effort.Medium,
            "high" => Effort.High,
            "xhigh" => Effort.Xhigh,
            "max" => Effort.Max,
            _ => null
        };
    }

    private static string FinishReason(string? stopReason) => stopReason switch
    {
        "max_tokens" => "length",
        "refusal" => "content_filter",
        _ => "stop"
    };

    /// <summary>
    /// Cuts a finished answer to the caller's max_tokens. Proportional, because the
    /// exact token boundaries are not known; close enough for a limit callers use as
    /// a ceiling rather than a target.
    /// </summary>
    private static (string Text, string Finish) ApplyTokenLimit(string text, long textTokens, int? maxTokens, string finish)
    {
        if (maxTokens is not > 0 || textTokens <= maxTokens || text.Length == 0)
        {
            return (text, finish);
        }

        var keep = (int)Math.Floor(text.Length * (maxTokens.Value / (double)textTokens));
        return (text[..Math.Clamp(keep, 0, text.Length)], "length");
    }

    #endregion

    #region Accounting

    private readonly record struct Usage(int Prompt, int Output, decimal CostUsd);

    private static Usage Count(string model, long input, long output, long? cacheWrite, long? cacheRead)
    {
        var write = cacheWrite ?? 0;
        var read = cacheRead ?? 0;

        // OpenAI's prompt_tokens is everything read, cached or not; Anthropic splits it.
        var prompt = (int)(input + write + read);
        return new Usage(prompt, (int)output, AnthropicPricing.Cost(model, input, output, write, read));
    }

    private void Spend(decimal usd) => _ledger.AddSpend(usd, _clock.UtcNow);

    /// <summary>
    /// Turns a failure into a reason and status, and pauses the credit where trying
    /// again at once would only fail again.
    /// </summary>
    private (string Reason, int? Status) Classify(Exception ex, CancellationToken callerToken)
    {
        var now = _clock.UtcNow;

        if (ex is OperationCanceledException)
        {
            if (callerToken.IsCancellationRequested)
            {
                return ("the caller cancelled the request", null);
            }

            OnOutageSignal("Anthropic did not answer in time", now);
            return ("Anthropic did not answer in time", 504);
        }

        var status = ex is AnthropicApiException api ? (int)api.StatusCode : (int?)null;
        var message = ex.Message;

        // Out of credit. The count missed it or the grant has not arrived yet; either
        // way, ask again in an hour rather than on every request.
        if (status == 402 || message.Contains("credit balance", StringComparison.OrdinalIgnoreCase))
        {
            var reason = "Anthropic says the credit balance is too low";
            _ledger.Block(reason, TimeSpan.FromHours(1), now);
            _logger.LogWarning("Anthropic credit refused for lack of balance; pausing it for an hour");
            return (reason, 402);
        }

        switch (status)
        {
            case 401 or 403:
            {
                var reason = $"Anthropic rejected the key ({status}): {Trim(message)}";
                _ledger.Block(reason, TimeSpan.FromMinutes(15), now);
                _ledger.RecordKeyRejection(reason, now);
                _logger.LogError("Anthropic credit key rejected ({Status}); pausing for 15 minutes: {Message}", status, message);
                return (reason, status);
            }
            case 429:
            {
                var reason = "Anthropic rate limit reached";
                _ledger.Block(reason, TimeSpan.FromMinutes(1), now);
                _logger.LogWarning("Anthropic credit rate limited; pausing for a minute");
                return (reason, 429);
            }
            case 529:
            {
                // Overloaded: the API says so itself, so do not make it say it again
                // to every request for the next minute.
                var reason = "Anthropic is overloaded (529)";
                _ledger.Block(reason, TimeSpan.FromMinutes(1), now);
                _logger.LogWarning("Anthropic overloaded; pausing the credit for a minute");
                return (reason, 529);
            }
            default:
            {
                var reason = status is null ? $"Anthropic call failed: {Trim(message)}" : $"Anthropic API error {status}: {Trim(message)}";
                // Only what looks like the API being down counts towards skipping it:
                // a 4xx is about this request, and the next one may well be fine.
                if (status is null or >= 500) OnOutageSignal(reason, now);
                else _ledger.NoteError(reason, now);
                _logger.LogWarning(ex, "Anthropic credit call failed ({Status})", status);
                return (reason, status);
            }
        }

        static string Trim(string s) => s.Length > 300 ? s[..300] : s;
    }

    /// <summary>
    /// A failure that looks like the API being down rather than anything about this
    /// request. A few in a row and the credit is skipped for a while, so an outage
    /// sends requests straight to the bridge instead of making each wait first.
    /// </summary>
    private void OnOutageSignal(string reason, DateTime now)
    {
        var run = _ledger.RecordFailure(reason, now);
        if (run >= Config.OutageFailures)
        {
            _ledger.Block($"{reason} ({run} failures in a row)", TimeSpan.FromMinutes(Config.OutagePauseMinutes), now);
            _logger.LogWarning(
                "Anthropic failed {Count} times in a row; skipping the credit for {Minutes} minutes",
                run, Config.OutagePauseMinutes);
        }
    }

    private ChatCompletionResult Failed(string model, string reason, Stopwatch stopwatch, int? status = null, decimal? cost = null) => new()
    {
        Model = model,
        Success = false,
        ErrorMessage = reason,
        HttpStatusCode = status,
        ProviderName = ProviderName,
        CostUsd = 0m,
        CostSource = "credit",
        CreditCostUsd = cost,
        ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds
    };

    #endregion

    #region Grant and refresh

    public async Task<AnthropicCreditStatus> RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return Status;
        }

        var now = _clock.UtcNow;
        var grant = await EnsureCurrentGrantAsync(now, cancellationToken);

        var used = grant is null
            ? 0m
            : await _db.UsageLogs
                .Where(l => l.Provider == ProviderName && l.CreditCostUsd != null &&
                            l.CreatedAt >= grant.StartsAt && l.CreatedAt < grant.ExpiresAt)
                .SumAsync(l => l.CreditCostUsd ?? 0m, cancellationToken);

        _ledger.Apply(grant, used, now);
        var status = Status;
        _logger.LogDebug("Anthropic credit: {Status}", status.Describe());
        return status;
    }

    public async Task<AnthropicCreditStatus> CheckAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return Status;
        }

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(20));

            // The models endpoint costs nothing and fails the same way a real call
            // would on a bad key.
            await Client.Value.Models.Retrieve(Config.PremiumModel, cancellationToken: timeout.Token);
            _ledger.ClearBlock();
        }
        catch (Exception ex)
        {
            Classify(ex, cancellationToken);
        }

        return await RefreshAsync(cancellationToken);
    }

    /// <summary>
    /// The grant covering <paramref name="now"/>, opening one when there is none.
    ///
    /// Cycles run from one renewal day to the next. When the latest grant has run out,
    /// the next one starts where it ended and lasts a month, so editing a grant's dates
    /// on the dashboard moves every later cycle with it. The very first one starts on
    /// the most recent renewal day.
    /// </summary>
    private async Task<CreditGrant?> EnsureCurrentGrantAsync(DateTime now, CancellationToken cancellationToken)
    {
        var current = await _db.CreditGrants
            .Where(g => g.Provider == ProviderName && g.StartsAt <= now && g.ExpiresAt > now)
            .OrderByDescending(g => g.StartsAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (current is not null || Config.MonthlyUsd <= 0)
        {
            return current;
        }

        var latest = await _db.CreditGrants
            .Where(g => g.Provider == ProviderName)
            .OrderByDescending(g => g.ExpiresAt)
            .FirstOrDefaultAsync(cancellationToken);

        DateTime start;
        if (latest is not null && latest.ExpiresAt <= now)
        {
            start = latest.ExpiresAt;
            while (NextMonth(start) <= now)
            {
                start = NextMonth(start);
            }
        }
        else if (latest is not null)
        {
            // A grant in the future and none now: leave the gap alone.
            return null;
        }
        else
        {
            start = LastRenewal(now, Config.RenewalDay);
        }

        var grant = new CreditGrant
        {
            Id = Guid.NewGuid(),
            Provider = ProviderName,
            AmountUsd = Config.MonthlyUsd,
            StartsAt = start,
            ExpiresAt = NextMonth(start),
            Source = "auto",
            Note = "Opened automatically from ANTHROPIC_CREDIT_MONTHLY_USD",
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.CreditGrants.Add(grant);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Opened Anthropic credit cycle ${Amount} from {Start:u} to {End:u}",
            grant.AmountUsd, grant.StartsAt, grant.ExpiresAt);
        return grant;

        DateTime NextMonth(DateTime d) => d.AddMonths(1);
    }

    /// <summary>Midnight UTC on the most recent occurrence of <paramref name="day"/>, today included.</summary>
    internal static DateTime LastRenewal(DateTime now, int day)
    {
        DateTime On(int year, int month) =>
            new(year, month, Math.Min(day, DateTime.DaysInMonth(year, month)), 0, 0, 0, DateTimeKind.Utc);

        var candidate = On(now.Year, now.Month);
        if (candidate > now)
        {
            var previous = now.AddMonths(-1);
            candidate = On(previous.Year, previous.Month);
        }

        return candidate;
    }

    #endregion

    #region Settings

    /// <summary>Read once per process; changing any of it means a restart, as with every other setting.</summary>
    internal sealed class Settings
    {
        public string ApiKey { get; init; } = "";
        public bool Enabled { get; init; }
        public decimal MonthlyUsd { get; init; }
        public int RenewalDay { get; init; }
        public decimal StopAt { get; init; }
        public HashSet<PaidTier> Tiers { get; init; } = [];
        public string PremiumModel { get; init; } = "claude-opus-5-5";
        public string ModerateModel { get; init; } = "claude-sonnet-5-5";
        public string? PremiumEffort { get; init; }
        public string? ModerateEffort { get; init; }
        public bool PromptCache { get; init; }
        public int TimeoutSeconds { get; init; }
        public int ThinkingHeadroomTokens { get; init; }
        public int DefaultMaxTokens { get; init; }
        public int KeyLifetimeDays { get; init; }
        public int KeyWarnDays { get; init; }
        public bool KeyReminders { get; init; }
        public string BaseUrl { get; init; } = "https://api.anthropic.com";
        public int OutageFailures { get; init; }
        public int OutagePauseMinutes { get; init; }
        public string LowModel { get; init; } = "claude-haiku-5-5";
        public string? LowEffort { get; init; }

        public static Settings FromEnvironment()
        {
            static string? Env(string name) =>
                Environment.GetEnvironmentVariable(name) is { Length: > 0 } v ? v.Trim() : null;

            static decimal Dec(string name, decimal fallback) =>
                decimal.TryParse(Env(name), NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : fallback;

            static int Int(string name, int fallback) =>
                int.TryParse(Env(name), out var v) ? v : fallback;

            var key = Env("ANTHROPIC_API_KEY") ?? "";

            // On whenever a key is present; ANTHROPIC_CREDIT_ENABLED=false turns it
            // off without removing the key. No key, nothing changes.
            var enabled = key.Length > 0 &&
                          !(bool.TryParse(Env("ANTHROPIC_CREDIT_ENABLED"), out var on) && !on);

            var tiers = (Env("ANTHROPIC_CREDIT_TIERS") ?? "premium,moderate,low")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(t => PaidTierExtensions.TryParseModel($"paid:{t}", out var tier) ? tier : (PaidTier?)null)
                .Where(t => t is not null)
                .Select(t => t!.Value)
                .ToHashSet();

            return new Settings
            {
                ApiKey = key,
                Enabled = enabled,
                MonthlyUsd = Dec("ANTHROPIC_CREDIT_MONTHLY_USD", 100m),
                RenewalDay = Math.Clamp(Int("ANTHROPIC_CREDIT_RENEWAL_DAY", 1), 1, 31),
                StopAt = Math.Clamp(Dec("ANTHROPIC_CREDIT_STOP_AT", 0.97m), 0m, 1m),
                Tiers = tiers,
                PremiumModel = Env("ANTHROPIC_PREMIUM_MODEL") ?? "claude-opus-5-5",
                ModerateModel = Env("ANTHROPIC_MODERATE_MODEL") ?? "claude-sonnet-5-5",
                PremiumEffort = Env("ANTHROPIC_PREMIUM_EFFORT"),
                ModerateEffort = Env("ANTHROPIC_MODERATE_EFFORT") ?? "low",
                PromptCache = !(bool.TryParse(Env("ANTHROPIC_PROMPT_CACHE"), out var cache) && !cache),
                TimeoutSeconds = Math.Max(10, Int("ANTHROPIC_TIMEOUT_SECONDS", 120)),
                ThinkingHeadroomTokens = Math.Max(0, Int("ANTHROPIC_THINKING_HEADROOM_TOKENS", 4096)),
                DefaultMaxTokens = Math.Max(256, Int("ANTHROPIC_DEFAULT_MAX_TOKENS", 16000)),
                // Keys made in the Console last 30 days unless chosen otherwise; 0 means
                // they do not expire and only a rejection is reported.
                KeyLifetimeDays = Math.Max(0, Int("ANTHROPIC_KEY_LIFETIME_DAYS", 30)),
                KeyWarnDays = Math.Max(1, Int("ANTHROPIC_KEY_WARN_DAYS", 3)),
                KeyReminders = !(bool.TryParse(Env("ANTHROPIC_KEY_REMINDERS_ENABLED"), out var remind) && !remind),
                BaseUrl = (Env("ANTHROPIC_BASE_URL") ?? "https://api.anthropic.com").TrimEnd('/'),
                OutageFailures = Math.Max(1, Int("ANTHROPIC_OUTAGE_FAILURES", 3)),
                OutagePauseMinutes = Math.Max(1, Int("ANTHROPIC_OUTAGE_PAUSE_MINUTES", 5)),
                LowModel = Env("ANTHROPIC_LOW_MODEL") ?? "claude-haiku-5-5",
                LowEffort = Env("ANTHROPIC_LOW_EFFORT") ?? "low"
            };
        }
    }

    #endregion
}
