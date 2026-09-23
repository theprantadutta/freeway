namespace Freeway.Domain.Common;

/// <summary>
/// Cost/capability tiers for the "paid" virtual model. Callers address a tier with
/// "paid:low", "paid:moderate" or "paid:premium". Bare "paid" resolves to <see cref="Low"/>
/// so existing callers keep exactly their current behaviour.
/// </summary>
public enum PaidTier
{
    Low = 0,
    Moderate = 1,
    Premium = 2
}

public static class PaidTierExtensions
{
    /// <summary>All tiers in ascending cost order.</summary>
    public static readonly PaidTier[] All = [PaidTier.Low, PaidTier.Moderate, PaidTier.Premium];

    /// <summary>
    /// Parses a chat "model" value into a paid tier. Accepts "paid" (=> Low), "paid:low",
    /// "paid:moderate" and "paid:premium", case-insensitively and whitespace-tolerant.
    /// Returns false for anything else, including "paid:" and unknown tier names.
    /// </summary>
    public static bool TryParseModel(string? model, out PaidTier tier)
    {
        tier = PaidTier.Low;

        if (string.IsNullOrWhiteSpace(model))
            return false;

        var value = model.Trim();

        // Bare "paid" keeps its historical meaning: the cheapest tier.
        if (value.Equals("paid", StringComparison.OrdinalIgnoreCase))
            return true;

        if (!value.StartsWith("paid:", StringComparison.OrdinalIgnoreCase))
            return false;

        return TryParseSlug(value["paid:".Length..], out tier);
    }

    /// <summary>
    /// Parses a bare tier name ("low"/"moderate"/"premium") as used in route segments.
    /// </summary>
    public static bool TryParseSlug(string? slug, out PaidTier tier)
    {
        tier = PaidTier.Low;

        if (string.IsNullOrWhiteSpace(slug))
            return false;

        switch (slug.Trim().ToLowerInvariant())
        {
            case "low": tier = PaidTier.Low; return true;
            case "moderate": tier = PaidTier.Moderate; return true;
            case "premium": tier = PaidTier.Premium; return true;
            default: return false;
        }
    }

    /// <summary>The tier name as it appears in a route segment, e.g. "premium".</summary>
    public static string ToSlug(this PaidTier tier) => tier switch
    {
        PaidTier.Moderate => "moderate",
        PaidTier.Premium => "premium",
        _ => "low"
    };

    /// <summary>The tier as a chat "model" value, e.g. "paid:premium".</summary>
    public static string ToModelString(this PaidTier tier) => $"paid:{tier.ToSlug()}";
}
