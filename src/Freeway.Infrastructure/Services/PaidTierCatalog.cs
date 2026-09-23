using Freeway.Domain.Common;

namespace Freeway.Infrastructure.Services;

/// <summary>
/// Tier policy for the "paid" virtual model: which models belong to which tier, and in what
/// order they should be attempted.
///
/// A tier is defined by two things:
///   1. A curated, ordered preference list of model IDs. The first one still offered by the
///      upstream catalog wins. This is what makes "premium" mean "a good model" rather than
///      just "an expensive model".
///   2. A price band, used to classify every other model and to provide a fallback chain when
///      none of the curated models are available.
///
/// Both are overridable via environment variables so the catalog can be retuned without a
/// rebuild as upstream model availability shifts.
/// </summary>
public static class PaidTierCatalog
{
    /// <summary>Upper bound (exclusive) of the Low band, in combined USD per million tokens.</summary>
    public static readonly decimal LowMaxPerMillion =
        ReadDecimal("PAID_TIER_LOW_MAX", 1.0m);

    /// <summary>Upper bound (exclusive) of the Moderate band, in combined USD per million tokens.</summary>
    public static readonly decimal ModerateMaxPerMillion =
        ReadDecimal("PAID_TIER_MODERATE_MAX", 10.0m);

    // Curated preference lists, best-first. Low is deliberately uncurated: it keeps the
    // historical "cheapest available" behaviour of the bare "paid" model.
    private static readonly string[] DefaultModerateModels =
    [
        "openai/gpt-5-mini",
        "openai/gpt-4.1-mini",
        "openai/gpt-5.4-mini",
        "openai/o4-mini"
    ];

    private static readonly string[] DefaultPremiumModels =
    [
        "openai/gpt-5.6-sol",
        "openai/gpt-5.6-sol-pro",
        "openai/gpt-5.6-terra",
        "openai/gpt-5.2",
        "anthropic/claude-opus-4.1"
    ];

    public static readonly IReadOnlyList<string> ModerateModels =
        ReadList("PAID_TIER_MODERATE_MODELS", DefaultModerateModels);

    public static readonly IReadOnlyList<string> PremiumModels =
        ReadList("PAID_TIER_PREMIUM_MODELS", DefaultPremiumModels);

    /// <summary>The curated preference list for a tier, best-first. Empty for Low.</summary>
    public static IReadOnlyList<string> CuratedFor(PaidTier tier) => tier switch
    {
        PaidTier.Moderate => ModerateModels,
        PaidTier.Premium => PremiumModels,
        _ => Array.Empty<string>()
    };

    /// <summary>
    /// Classifies a model by its combined per-token price (prompt + completion).
    /// </summary>
    public static PaidTier Classify(decimal combinedPricePerToken)
    {
        var perMillion = combinedPricePerToken * 1_000_000m;

        if (perMillion < LowMaxPerMillion) return PaidTier.Low;
        if (perMillion < ModerateMaxPerMillion) return PaidTier.Moderate;
        return PaidTier.Premium;
    }

    private static decimal ReadDecimal(string name, decimal fallback)
    {
        var raw = Environment.GetEnvironmentVariable(name);
        return decimal.TryParse(raw, out var value) && value > 0 ? value : fallback;
    }

    private static IReadOnlyList<string> ReadList(string name, string[] fallback)
    {
        var raw = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(raw))
            return fallback;

        var parsed = raw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();

        return parsed.Length > 0 ? parsed : fallback;
    }
}
