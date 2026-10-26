namespace Freeway.Infrastructure.Services;

/// <summary>
/// Claude API list prices, per million tokens, for counting what a call took off the
/// credit. The balance cannot be read back for an individual account, so these are
/// the whole of the accounting -- keep them in step with the published pricing page.
///
/// A model missing from the table is charged at the most expensive known rate, so an
/// unrecognised name makes the credit look more spent rather than less.
/// </summary>
public static class AnthropicPricing
{
    public sealed record Rates(decimal Input, decimal Output, decimal CacheWrite, decimal CacheRead);

    // Cache writes are 1.25x input (the five-minute TTL used here); reads as published.
    private static readonly Dictionary<string, Rates> Table = new(StringComparer.OrdinalIgnoreCase)
    {
        ["claude-fable-5-1"] = new(10.00m, 50.00m, 12.50m, 0.25m),
        ["claude-opus-5-5"] = new(4.00m, 20.00m, 5.00m, 0.20m),
        ["claude-opus-5"] = new(5.00m, 25.00m, 6.25m, 0.50m),
        ["claude-sonnet-5-5"] = new(2.00m, 10.00m, 2.50m, 0.20m),
        ["claude-sonnet-5"] = new(2.00m, 10.00m, 2.50m, 0.20m),
        ["claude-haiku-5-5"] = new(0.10m, 0.50m, 0.125m, 0.01m),
    };

    private static readonly Rates Ceiling = Table["claude-fable-5-1"];

    public static Rates For(string? model)
    {
        if (string.IsNullOrWhiteSpace(model)) return Ceiling;
        if (Table.TryGetValue(model, out var rates)) return rates;

        // Responses can name a dated or suffixed variant of a model in the table.
        var match = Table.Keys
            .Where(k => model.StartsWith(k, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(k => k.Length)
            .FirstOrDefault();
        return match is null ? Ceiling : Table[match];
    }

    // Haiku 5.5 charges more once a prompt passes 100K tokens.
    private const long HaikuLongPrompt = 100_000;
    private static readonly Rates HaikuLong = new(0.50m, 2.50m, 0.625m, 0.05m);

    public static decimal Cost(string? model, long input, long output, long cacheWrite, long cacheRead)
    {
        var r = For(model);
        if (ReferenceEquals(r, Table["claude-haiku-5-5"]) && input + cacheWrite + cacheRead > HaikuLongPrompt)
        {
            r = HaikuLong;
        }
        return (input * r.Input + output * r.Output + cacheWrite * r.CacheWrite + cacheRead * r.CacheRead)
               / 1_000_000m;
    }
}
