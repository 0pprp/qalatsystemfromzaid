using BE_Company.Sales.Rating;
using Xunit;

namespace BE_Company.Sales.Tests;

public sealed class ListFollowerRatingCohortTests
{
    [Fact]
    public void OpenBalanceOnly_DropsZeroAndNegativeRemaining()
    {
        var facts = new[]
        {
            Fact(1, remaining: 1_000_000),
            Fact(2, remaining: 0),
            Fact(3, remaining: -1000),
            Fact(4, remaining: 500),
        };

        var open = ListFollowerRatingCohort.OpenBalanceOnly(facts);

        Assert.Equal(new[] { 1, 4 }, open.Select(f => f.CustomerId).ToArray());
    }

    [Fact]
    public void Snapshot_WithCohort_IgnoresFullyPaid()
    {
        var asOf = new DateTime(2026, 9, 25);
        var facts = new[]
        {
            // Fully paid — excluded from list/follower cohort.
            new CustomerRatingFacts(1, 10, false, false, asOf.AddDays(-10), asOf.AddDays(-1),
                1_000_000, 1_000_000, 0, 1),
            // Still owes — stays in cohort.
            new CustomerRatingFacts(2, 10, false, false, asOf.AddDays(-30), asOf.AddDays(-1),
                1_000_000, 100_000, 900_000, 1),
        };

        var all = EmployeeSalesRating.Snapshot(facts, asOf);
        var openOnly = EmployeeSalesRating.Snapshot(ListFollowerRatingCohort.OpenBalanceOnly(facts), asOf);

        Assert.Equal(2, all.CustomerCount);
        Assert.Equal(1, openOnly.CustomerCount);
    }

    private static CustomerRatingFacts Fact(int id, double remaining) =>
        new(id, 1, false, false, new DateTime(2026, 1, 1), new DateTime(2026, 1, 10),
            1_000_000, 1_000_000 - remaining, remaining, 1);
}
