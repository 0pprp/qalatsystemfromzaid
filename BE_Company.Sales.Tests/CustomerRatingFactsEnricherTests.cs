using BE_Company.Sales.Rating;
using Xunit;

namespace BE_Company.Sales.Tests;

public class CustomerRatingFactsEnricherTests
{
    private static CustomerRatingFacts OfficialZero(int id) =>
        new(id, 10, false, false, null, null, 0, 0, 0, 0);

    [Fact]
    public void Does_not_invent_totals_from_module_when_official_sale_is_empty()
    {
        var saleDate = new DateTime(2026, 9, 19);
        var fact = OfficialZero(77);
        var enriched = CustomerRatingFactsEnricher.Apply(
            fact,
            new ModuleSaleRatingSupplement(77, saleDate, 2_900_000, 145_000));

        Assert.Equal(0, enriched.AmountTotalSales);
        Assert.Equal(0, enriched.AmountRemaining);
        Assert.Null(enriched.DateSaleDevice);
        Assert.Equal(0, enriched.ReceiptsTotal);
    }

    [Fact]
    public void Does_not_override_positive_official_amounts()
    {
        var fact = new CustomerRatingFacts(
            5, 1, false, false,
            new DateTime(2026, 1, 1),
            new DateTime(2026, 2, 1),
            1_000_000, 200_000, 800_000, 2);

        var enriched = CustomerRatingFactsEnricher.Apply(
            fact,
            new ModuleSaleRatingSupplement(5, new DateTime(2026, 9, 1), 2_900_000, 145_000));

        Assert.Equal(1_000_000, enriched.AmountTotalSales);
        Assert.Equal(200_000, enriched.ReceiptsTotal);
        Assert.Equal(800_000, enriched.AmountRemaining);
        Assert.Equal(new DateTime(2026, 1, 1), enriched.DateSaleDevice);
    }

    [Fact]
    public void Fills_missing_sale_date_only_when_official_amount_exists()
    {
        var fact = new CustomerRatingFacts(
            3, null, false, false,
            null,
            new DateTime(2026, 9, 20),
            500_000, 100_000, 400_000, 1);

        var enriched = CustomerRatingFactsEnricher.Apply(
            fact,
            new ModuleSaleRatingSupplement(3, new DateTime(2026, 9, 19), 2_900_000, 0));

        Assert.Equal(500_000, enriched.AmountTotalSales);
        Assert.Equal(new DateTime(2026, 9, 19), enriched.DateSaleDevice);
        Assert.Equal(100_000, enriched.ReceiptsTotal);
        Assert.Equal(400_000, enriched.AmountRemaining);
    }

    [Fact]
    public void Official_zero_stays_weak_missing_sale_data()
    {
        var saleDate = new DateTime(2026, 9, 19);
        var asOf = new DateTime(2026, 9, 24);
        var enriched = CustomerRatingFactsEnricher.Apply(
            OfficialZero(9),
            new ModuleSaleRatingSupplement(9, saleDate, 2_900_000, 145_000));

        var rated = CustomerRatingCalculator.Evaluate(enriched, asOf);
        Assert.Equal(CustomerRatingLevel.Weak, rated.Level);
        Assert.Contains("لا توجد بيانات بيع كافية", rated.Reason);
    }

    [Fact]
    public void Null_supplement_leaves_fact_unchanged()
    {
        var fact = OfficialZero(1);
        Assert.Same(fact, CustomerRatingFactsEnricher.Apply(fact, null));
    }
}
