using FleetCheck.Selection;
using Xunit;

namespace FleetCheck.Tests;

public class QuarterlySelectorTests
{
    private readonly QuarterlySelector _selector = new();

    // Helper to quickly make a list of vehicles.
    private List<Vehicle> MakeVehicles(int count)
    {
        var vehicles = new List<Vehicle>();
        for (int i = 1; i <= count; i++)
            vehicles.Add(new Vehicle($"V{i:D6}"));
        return vehicles;
    }

    // Running it twice with the same inputs must give the same result.
    [Fact]
    public void RunningTwice_GivesSameResult()
    {
        var vehicles = MakeVehicles(100);

        var first = _selector.Select("company-A", "2025-Q1", vehicles, 25);
        var second = _selector.Select("company-A", "2025-Q1", vehicles, 25);

        Assert.Equal(first.SelectedVehicleIds, second.SelectedVehicleIds);
        Assert.Equal(first.Seed, second.Seed);
    }

    // The order the vehicles are passed in must not change the result.
    [Fact]
    public void DifferentInputOrder_GivesSameResult()
    {
        var vehicles = MakeVehicles(100);
        var shuffled = vehicles.OrderBy(_ => Guid.NewGuid()).ToList();

        var original = _selector.Select("company-A", "2025-Q1", vehicles, 25);
        var reordered = _selector.Select("company-A", "2025-Q1", shuffled, 25);

        Assert.Equal(original.SelectedVehicleIds, reordered.SelectedVehicleIds);
    }

    // 25% of 100 vehicles should be 25.
    [Fact]
    public void PicksTheCorrectShare()
    {
        var result = _selector.Select("company-A", "2025-Q1", MakeVehicles(100), 25);
        Assert.Equal(25, result.SelectedCount);
    }

    // A different quarter should give a different seed and a different pick.
    [Fact]
    public void DifferentQuarter_GivesDifferentResult()
    {
        var vehicles = MakeVehicles(200);

        var q1 = _selector.Select("company-A", "2025-Q1", vehicles, 25);
        var q2 = _selector.Select("company-A", "2025-Q2", vehicles, 25);

        Assert.NotEqual(q1.Seed, q2.Seed);
        Assert.NotEqual(q1.SelectedVehicleIds, q2.SelectedVehicleIds);
    }

    // A different company should give a different seed.
    [Fact]
    public void DifferentCompany_GivesDifferentSeed()
    {
        var vehicles = MakeVehicles(200);

        var a = _selector.Select("company-A", "2025-Q1", vehicles, 25);
        var b = _selector.Select("company-B", "2025-Q1", vehicles, 25);

        Assert.NotEqual(a.Seed, b.Seed);
    }

    // It must work for a small fleet (20) and a very large one (200,000).
    [Theory]
    [InlineData(20)]
    [InlineData(200_000)]
    public void WorksForSmallAndLargeFleets(int size)
    {
        var result = _selector.Select("company-A", "2025-Q1", MakeVehicles(size), 10);

        int expected = (int)Math.Round(size * 0.10, MidpointRounding.AwayFromZero);
        Assert.Equal(expected, result.SelectedCount);

        // No vehicle should be picked twice.
        Assert.Equal(result.SelectedCount, result.SelectedVehicleIds.Distinct().Count());
    }

    // Over many quarters, each vehicle should be picked a roughly equal number of times.
    [Fact]
    public void PicksAreRoughlyFairOverTime()
    {
        var vehicles = MakeVehicles(100);

        // Count how many times each vehicle gets picked.
        var pickCounts = new Dictionary<string, int>();
        foreach (var v in vehicles)
            pickCounts[v.Id] = 0;

        int runs = 2000;
        for (int q = 0; q < runs; q++)
        {
            var result = _selector.Select("company-A", $"Q{q}", vehicles, 25);
            foreach (var id in result.SelectedVehicleIds)
                pickCounts[id]++;
        }

        // Each vehicle is expected to be picked about 25% of the time (~500).
        double average = pickCounts.Values.Average();
        foreach (var count in pickCounts.Values)
            Assert.InRange(count, average * 0.7, average * 1.3);
    }

    // 25% of a single vehicle should still pick 1, not 0.
    [Fact]
    public void AlwaysPicksAtLeastOne_WhenPercentageIsSet()
    {
        var result = _selector.Select("company-A", "2025-Q1", MakeVehicles(1), 25);
        Assert.Equal(1, result.SelectedCount);
    }

    // An empty fleet should pick nothing and not crash.
    [Fact]
    public void EmptyFleet_PicksNothing()
    {
        var result = _selector.Select("company-A", "2025-Q1", new List<Vehicle>(), 25);
        Assert.Equal(0, result.SelectedCount);
    }

    // Duplicate vehicle ids should be removed before picking.
    [Fact]
    public void DuplicateVehicles_AreRemoved()
    {
        var vehicles = new List<Vehicle>
        {
            new("V1"),
            new("V1"),
            new("V2")
        };

        var result = _selector.Select("company-A", "2025-Q1", vehicles, 100);
        Assert.Equal(2, result.EligibleCount);
    }

    // An invalid percentage should be rejected.
    [Fact]
    public void InvalidPercentage_ThrowsError()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _selector.Select("company-A", "2025-Q1", MakeVehicles(10), 150));
    }
}