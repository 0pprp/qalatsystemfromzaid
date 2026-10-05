using BE_Company.Sales.Rating;
using Xunit;

namespace BE_Company.Sales.Tests;

public class ActiveRatingCohortTests
{
    [Fact]
    public void Build_HeadlineAndList_SameExcellentCount()
    {
        var asOf = new DateTime(2026, 9, 30);
        var facts = new List<CustomerRatingFacts>
        {
            // Active excellent (paid recently, high rate)
            Fact(1, sale: asOf.AddDays(-30), paid: asOf.AddDays(-1), total: 1_000_000, receipts: 250_000),
            // Active excellent
            Fact(2, sale: asOf.AddDays(-40), paid: asOf.AddDays(-2), total: 1_000_000, receipts: 300_000),
            // Inactive — paid 45 days ago, excluded from both
            Fact(3, sale: asOf.AddDays(-60), paid: asOf.AddDays(-45), total: 1_000_000, receipts: 400_000),
            // Settled (remaining 0) — excluded
            Fact(4, sale: asOf.AddDays(-20), paid: asOf.AddDays(-1), total: 1_000_000, receipts: 1_000_000),
        };

        var cohort = ActiveRatingCohort.Build(facts, asOf);
        var agg = ListRatingAggregator.Aggregate(cohort.Select(c => c.Rated));
        var excellentRows = cohort.Where(c => c.Rated.Level == CustomerRatingLevel.Excellent).ToList();

        Assert.Equal(agg.ExcellentCount, excellentRows.Count);
        Assert.Equal(agg.CustomerCount, cohort.Count);
        Assert.DoesNotContain(cohort, c => c.Fact.CustomerId == 3);
        Assert.DoesNotContain(cohort, c => c.Fact.CustomerId == 4);
    }

    private static CustomerRatingFacts Fact(
        int id,
        DateTime sale,
        DateTime paid,
        double total,
        double receipts) =>
        new(
            id,
            DelegateId: 1,
            IsLegal: false,
            IsFakeSale: false,
            DateSaleDevice: sale,
            LastPaymentDate: paid,
            AmountTotalSales: total,
            ReceiptsTotal: receipts,
            AmountRemaining: Math.Max(0, total - receipts));
}
