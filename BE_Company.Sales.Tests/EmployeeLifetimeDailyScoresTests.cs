using BE_Company.Sales.Rating;
using BE_Company.Sales.Services;
using Xunit;

namespace BE_Company.Sales.Tests;

public class EmployeeLifetimeDailyScoresTests
{
    private static CustomerRatingFacts Settled(int id, DateTime? sale, int settleDays = 100) =>
        new(id, null, false, false, sale, sale?.AddDays(settleDays - 1), 1000, 1000, 0, 1);

    [Fact]
    public void Undated_Customer_Is_Counted_From_First_Day_And_Matches_Snapshot_End()
    {
        var from = new DateTime(2026, 9, 19);
        var to = new DateTime(2026, 9, 23);
        var dated = new[]
        {
            Settled(1, from, 100),
            Settled(2, from, 100),
        };
        var undated = new[]
        {
            Settled(3, null), // no DateSaleDevice → Weak 0 in Evaluate
        };

        var series = BranchManagerStatsService.EmployeeDailyScoresIncludingUndated(dated, undated, from, to);
        Assert.Equal(5, series.Count);

        // Day 1: 2 excellent (10) + 1 weak undated (0) = 20/3 ≈ 6.67
        Assert.Equal(6.67, series[from], 2);

        var all = dated.Concat(undated);
        var snap = EmployeeSalesRating.Snapshot(all, to);
        Assert.Equal(3, snap.CustomerCount);
        Assert.Equal(snap.AverageScore, series[to], 2);
    }
}
