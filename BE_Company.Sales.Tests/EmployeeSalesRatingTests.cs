using BE_Company.Sales.Rating;
using Xunit;

namespace BE_Company.Sales.Tests;

public class EmployeeSalesRatingTests
{
    private static CustomerRatingFacts Settled(int id, DateTime sale, int settleDays) =>
        new(id, null, false, false, sale, sale.AddDays(settleDays - 1), 1000, 1000, 0, 1);

    [Fact]
    public void Snapshot_Uses_All_Facts_Without_Sale_Period_Filter()
    {
        var asOf = new DateTime(2026, 9, 20);
        var facts = new[]
        {
            Settled(1, new DateTime(2026, 8, 1), 100),
            Settled(2, new DateTime(2026, 9, 5), 100),
        };
        var snap = EmployeeSalesRating.Snapshot(facts, asOf);
        Assert.Equal(2, snap.CustomerCount);
        Assert.Equal(10.0, snap.AverageScore, 2);
    }

    [Fact]
    public void Snapshot_Empty_Is_Zero()
    {
        var snap = EmployeeSalesRating.Snapshot([], DateTime.UtcNow.Date);
        Assert.Equal(0, snap.CustomerCount);
        Assert.Equal(0, snap.AverageScore);
    }

    [Fact]
    public void PeriodSnapshot_Only_Sales_Inside_From_To()
    {
        var from = new DateTime(2026, 9, 1);
        var to = new DateTime(2026, 9, 23);
        var facts = new[]
        {
            Settled(1, new DateTime(2026, 8, 31), 100), // excluded
            Settled(2, new DateTime(2026, 9, 1), 100),  // excellent
            Settled(3, new DateTime(2026, 9, 10), 150), // good
            Settled(4, new DateTime(2026, 9, 30), 100), // after to — excluded
        };

        var snap = EmployeeSalesRating.PeriodSnapshot(facts, from, to);

        Assert.Equal(2, snap.CustomerCount);
        Assert.Equal(1, snap.ExcellentCount);
        Assert.Equal(1, snap.GoodCount);
        Assert.Equal(7.5, snap.AverageScore, 2); // (10+5)/2
    }
}
