using Freeway.Infrastructure.Services;

namespace Freeway.Tests;

public class CreditBudgetMathTests
{
    [Fact]
    public void ParseCaps_reads_names_ids_and_decimals()
    {
        var id = Guid.NewGuid();
        var caps = CreditBudgetMath.ParseCaps($"quotely:50, Atlas:12.5 ,{id}:3");

        Assert.Equal(50m, caps["Quotely"]);
        Assert.Equal(12.5m, caps["atlas"]);
        Assert.Equal(3m, caps[id.ToString()]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseCaps_empty_means_no_caps(string? value)
    {
        Assert.Empty(CreditBudgetMath.ParseCaps(value));
    }

    [Fact]
    public void ParseCaps_skips_malformed_entries_instead_of_failing()
    {
        var caps = CreditBudgetMath.ParseCaps("quotely:fifty,:10,atlas:,flowpilot:-1,ok:7");

        Assert.Single(caps);
        Assert.Equal(7m, caps["ok"]);
    }

    [Fact]
    public void CapFor_prefers_the_id_then_the_name()
    {
        var id = Guid.NewGuid();
        var caps = CreditBudgetMath.ParseCaps($"quotely:50,{id}:5");

        Assert.Equal(5m, CreditBudgetMath.CapFor(caps, id, "Quotely"));
        Assert.Equal(50m, CreditBudgetMath.CapFor(caps, Guid.NewGuid(), " quotely "));
        Assert.Null(CreditBudgetMath.CapFor(caps, Guid.NewGuid(), "AI Persona"));
        Assert.Null(CreditBudgetMath.CapFor(new Dictionary<string, decimal>(), id, "Quotely"));
    }

    [Fact]
    public void Spendable_holds_back_the_others_pace_with_headroom()
    {
        // Others spent 10 over 10 days (1/day). 20 days left: reserve 1 × 20 × 1.5 = 30.
        var spendable = CreditBudgetMath.Spendable(
            remainingUsable: 80m, othersUsed: 10m, daysElapsed: 10, daysLeft: 20, cap: null, projectUsed: 2m);

        Assert.Equal(50m, spendable);
    }

    [Fact]
    public void Spendable_never_exceeds_what_is_left_under_the_cap()
    {
        var spendable = CreditBudgetMath.Spendable(
            remainingUsable: 80m, othersUsed: 10m, daysElapsed: 10, daysLeft: 20, cap: 50m, projectUsed: 42m);

        Assert.Equal(8m, spendable);
    }

    [Fact]
    public void Spendable_is_zero_past_the_cap_or_when_the_reserve_takes_everything()
    {
        Assert.Equal(0m, CreditBudgetMath.Spendable(80m, 10m, 10, 20, cap: 50m, projectUsed: 55m));
        Assert.Equal(0m, CreditBudgetMath.Spendable(10m, 60m, 10, 20, cap: null, projectUsed: 0m));
    }

    [Fact]
    public void Spendable_on_the_first_day_does_not_divide_by_zero()
    {
        // Elapsed is floored at a quarter day, so a busy first hour cannot blow up the pace.
        var spendable = CreditBudgetMath.Spendable(97m, 0.5m, 0, 30, cap: null, projectUsed: 0m);

        Assert.Equal(97m - 0.5m / 0.25m * 30m * 1.5m, spendable);
    }

    [Fact]
    public void Spendable_on_the_last_day_keeps_no_reserve()
    {
        Assert.Equal(40m, CreditBudgetMath.Spendable(40m, 60m, 30, 0, cap: null, projectUsed: 0m));
    }
}
