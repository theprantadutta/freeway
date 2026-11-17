using System.Globalization;

namespace Freeway.Infrastructure.Services;

/// <summary>
/// The arithmetic behind per-project credit caps and the budget a project may still
/// spend. Kept free of the database and the clock so every rule can be tested alone.
/// </summary>
public static class CreditBudgetMath
{
    /// <summary>How much of the other projects' expected spend is held back, as a multiple.</summary>
    public const decimal ReserveFactor = 1.5m;

    /// <summary>
    /// Parses ANTHROPIC_CREDIT_PROJECT_CAPS, e.g. "quotely:50, atlas:12.5". Keys are
    /// project names (case-insensitive) or project ids; malformed entries are skipped
    /// rather than failing startup, because a cap is a guard, not a requirement.
    /// </summary>
    public static Dictionary<string, decimal> ParseCaps(string? value)
    {
        var caps = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(value)) return caps;

        foreach (var entry in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var colon = entry.LastIndexOf(':');
            if (colon <= 0 || colon == entry.Length - 1) continue;

            var key = entry[..colon].Trim();
            if (key.Length > 0 &&
                decimal.TryParse(entry[(colon + 1)..].Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var cap) &&
                cap >= 0)
            {
                caps[key] = cap;
            }
        }

        return caps;
    }

    /// <summary>The cap for a project, matched by id first, then by name. Null for none.</summary>
    public static decimal? CapFor(IReadOnlyDictionary<string, decimal> caps, Guid projectId, string? projectName)
    {
        if (caps.Count == 0) return null;
        if (caps.TryGetValue(projectId.ToString(), out var byId)) return byId;
        if (projectName is not null && caps.TryGetValue(projectName.Trim(), out var byName)) return byName;
        return null;
    }

    /// <summary>
    /// What a project can spend from here without leaving the others short.
    ///
    /// The reserve is what the other projects are on course to spend before the reset
    /// at their pace so far, times <see cref="ReserveFactor"/>. The result never goes
    /// negative and never exceeds what is left under the project's own cap.
    /// </summary>
    public static decimal Spendable(
        decimal remainingUsable,
        decimal othersUsed,
        double daysElapsed,
        double daysLeft,
        decimal? cap,
        decimal projectUsed)
    {
        var othersBurn = othersUsed / (decimal)Math.Max(daysElapsed, 0.25);
        var reserve = othersBurn * (decimal)Math.Max(daysLeft, 0) * ReserveFactor;

        var spendable = remainingUsable - reserve;
        if (cap is { } c)
        {
            spendable = Math.Min(spendable, c - projectUsed);
        }

        return Math.Round(Math.Max(0, spendable), 4);
    }
}
