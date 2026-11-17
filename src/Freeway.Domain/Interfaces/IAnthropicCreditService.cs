using Freeway.Domain.Common;
using Freeway.Domain.Entities;

namespace Freeway.Domain.Interfaces;

/// <summary>
/// The Claude API, paid for out of the monthly credit a Max plan includes.
///
/// First choice for the lanes it is enabled for, ahead of the Claude Code bridge and
/// OpenRouter, because the credit is use-it-or-lose-it: whatever is not spent by the
/// end of the cycle is gone. Like the bridge it is strictly an optimisation. Every
/// method reports failure as an unsuccessful result rather than throwing, and the
/// caller carries on down the chain.
///
/// The provider offers no way for an individual account to read its balance, so the
/// spend is counted here, from the usage every response reports, at list price.
/// </summary>
public interface IAnthropicCreditService
{
    /// <summary>A key is set and ANTHROPIC_CREDIT_ENABLED is not false.</summary>
    bool IsConfigured { get; }

    /// <summary>
    /// SHA-256 fingerprint of the key in use (never the key), matching
    /// <see cref="ProviderKey.Fingerprint"/>. Null when there is no key.
    /// </summary>
    string? KeyFingerprint { get; }

    /// <summary>Whether this lane is one the credit serves.</summary>
    bool Serves(PaidTier tier);

    /// <summary>The model the credit uses for a lane.</summary>
    string ModelFor(PaidTier tier);

    /// <summary>Last known state. Never performs a call, so it is safe on the request path.</summary>
    AnthropicCreditStatus Status { get; }

    Task<ChatCompletionResult> CompleteAsync(
        List<ChatMessage> messages,
        ChatCompletionOptions? options,
        PaidTier tier,
        CancellationToken cancellationToken = default);

    Task<ChatCompletionStream> StreamAsync(
        List<ChatMessage> messages,
        ChatCompletionOptions? options,
        PaidTier tier,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens the current cycle's grant if it is missing and recounts what has been
    /// spent against it from the usage logs. Run on a schedule and after a grant edit.
    /// </summary>
    Task<AnthropicCreditStatus> RefreshAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirms the key works with a call that costs nothing (the models endpoint),
    /// clears a block left by an earlier rejection if it now does, then refreshes.
    /// </summary>
    Task<AnthropicCreditStatus> CheckAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Why a project may not use the credit right now because it has reached its own
    /// cap (ANTHROPIC_CREDIT_PROJECT_CAPS), or null when it may. Projects without a cap
    /// always get null, without touching the database.
    /// </summary>
    Task<string?> ProjectCapReachedAsync(Guid projectId, CancellationToken cancellationToken = default);

    /// <summary>The credit as one project sees it: what is left, what it has used, and what it may still spend.</summary>
    Task<CreditBudget> GetBudgetAsync(Guid projectId, CancellationToken cancellationToken = default);
}

/// <summary>Returned by GET /v1/budget. Serialised in snake_case.</summary>
public class CreditBudget
{
    public CreditBudgetCycle Cycle { get; set; } = new();
    public CreditBudgetCredit Credit { get; set; } = new();
    public CreditBudgetProject Project { get; set; } = new();

    /// <summary>
    /// What this project can spend before the reset without leaving the others short:
    /// max(0, min(remaining_usable − reserve, cap − project's credit used)), where the
    /// reserve is the other projects' daily pace × days left × 1.5.
    /// </summary>
    public decimal SpendableUsd { get; set; }
}

public class CreditBudgetCycle
{
    public DateTime? StartsAt { get; set; }
    public DateTime? ResetsAt { get; set; }
    public double DaysLeft { get; set; }
}

public class CreditBudgetCredit
{
    /// <summary>off / unchecked / active / no_grant / exhausted / blocked.</summary>
    public string State { get; set; } = "off";
    public decimal GrantUsd { get; set; }
    public decimal UsedUsd { get; set; }

    /// <summary>What Freeway will spend before it stops using the credit (grant × stop-at share).</summary>
    public decimal UsableUsd { get; set; }
    public decimal RemainingUsableUsd { get; set; }
    public decimal BurnPerDayUsd { get; set; }
    public decimal ProjectedUnusedUsd { get; set; }
}

public class CreditBudgetProject
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal CreditUsedUsd { get; set; }

    /// <summary>Credit used since 00:00 UTC today.</summary>
    public decimal CreditUsedTodayUsd { get; set; }

    /// <summary>Requests this cycle served from the credit.</summary>
    public int Requests { get; set; }

    /// <summary>This project's share of the credit used this cycle, 0 to 1.</summary>
    public decimal ShareOfCredit { get; set; }

    /// <summary>The project's own cap, or null when it has none.</summary>
    public decimal? CapUsd { get; set; }
}

public enum AnthropicCreditState
{
    /// <summary>No key, or switched off.</summary>
    Off,

    /// <summary>Configured but not counted yet, e.g. just after startup.</summary>
    Unchecked,

    Active,

    /// <summary>No grant covers the current time.</summary>
    NoGrant,

    /// <summary>Freeway's count has reached the stop-at share of the grant.</summary>
    Exhausted,

    /// <summary>Anthropic refused recently (balance, key, rate limit); retried after a pause.</summary>
    Blocked
}

/// <summary>Where the credit stands, as of the last refresh plus every call since.</summary>
public class AnthropicCreditStatus
{
    public AnthropicCreditState State { get; set; } = AnthropicCreditState.Off;
    public bool CanServe => State == AnthropicCreditState.Active;

    public CreditGrant? Grant { get; set; }

    /// <summary>Spent against <see cref="Grant"/> so far, at list price.</summary>
    public decimal UsedUsd { get; set; }

    /// <summary>The share of the grant at which the credit stops being used, 0 to 1.</summary>
    public decimal StopAt { get; set; }

    /// <summary>The grant times <see cref="StopAt"/>: what Freeway will actually spend.</summary>
    public decimal UsableUsd => Grant is null ? 0 : Math.Round(Grant.AmountUsd * StopAt, 4);

    public decimal RemainingUsd => Grant is null ? 0 : Math.Max(0, Grant.AmountUsd - UsedUsd);

    public string? BlockedReason { get; set; }
    public DateTime? BlockedUntil { get; set; }

    /// <summary>The most recent failure, kept after a block lifts so it can be seen.</summary>
    public string? LastError { get; set; }
    public DateTime? LastErrorAt { get; set; }

    public DateTime? RefreshedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }

    public string Describe() => State switch
    {
        AnthropicCreditState.Active => $"active: ${UsedUsd:0.00} of ${Grant?.AmountUsd ?? 0:0.00} used",
        AnthropicCreditState.Exhausted => $"exhausted: ${UsedUsd:0.00} used, stops at ${UsableUsd:0.00}",
        AnthropicCreditState.Blocked => $"paused until {BlockedUntil:u}: {BlockedReason}",
        AnthropicCreditState.NoGrant => "no credit grant covers today",
        AnthropicCreditState.Unchecked => "not counted yet",
        _ => "off (no ANTHROPIC_API_KEY, or ANTHROPIC_CREDIT_ENABLED=false)"
    };
}
