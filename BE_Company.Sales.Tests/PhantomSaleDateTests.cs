using BE_Company.Sales.Rating;
using Xunit;

namespace BE_Company.Sales.Tests;

public class PhantomSaleDateTests
{
    [Fact]
    public void Adjust_Null_ReturnsNull() =>
        Assert.Null(PhantomSaleDate.Adjust(null));

    [Theory]
    [InlineData("2026-04-30", "2026-05-01")]
    [InlineData("2025-01-01", "2026-05-01")]
    [InlineData("2026-05-01", "2026-05-31")]
    [InlineData("2026-08-31", "2026-09-30")]
    [InlineData("2026-09-01", "2026-09-01")]
    [InlineData("2026-10-01", "2026-10-01")]
    public void Adjust_Boundaries(string input, string expected)
    {
        var got = PhantomSaleDate.Adjust(DateTime.Parse(input));
        Assert.Equal(DateTime.Parse(expected), got);
    }

    [Fact]
    public void Evaluate_Phantom_ShortensDaysSinceSale_VsReal()
    {
        var asOf = new DateTime(2026, 10, 1);
        var facts = new CustomerRatingFacts(
            CustomerId: 1,
            DelegateId: 1,
            IsLegal: false,
            IsFakeSale: false,
            DateSaleDevice: new DateTime(2026, 1, 1),
            LastPaymentDate: asOf.AddDays(-1),
            AmountTotalSales: 1_000_000,
            ReceiptsTotal: 100_000,
            AmountRemaining: 900_000);

        var real = CustomerRatingCalculator.Evaluate(facts, asOf, usePhantomSaleDate: false);
        var phantom = CustomerRatingCalculator.Evaluate(facts, asOf, usePhantomSaleDate: true);

        Assert.False(real.IsSettled);
        Assert.False(phantom.IsSettled);
        Assert.True(phantom.DaysSinceSale < real.DaysSinceSale);
        Assert.True(phantom.Score >= real.Score);
    }
}
