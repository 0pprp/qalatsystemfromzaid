using BE_Company.Sales.Rating;
using Xunit;

namespace BE_Company.Sales.Tests;

public class CustomerRatingCalculatorTests
{
    private static readonly DateTime AsOf = new(2026, 3, 1, 0, 0, 0, DateTimeKind.Unspecified);

    [Fact]
    public void Legal_Always_Overrides_With_Minus10()
    {
        var facts = Settled(daysToSettle: 100) with { IsLegal = true };
        var result = CustomerRatingCalculator.Evaluate(facts, AsOf);
        Assert.Equal(CustomerRatingLevel.Legal, result.Level);
        Assert.Equal(-10, result.Score);
        Assert.Equal("قانونية", result.RatingLabel);
    }

    [Theory]
    [InlineData(100, CustomerRatingLevel.Excellent, 10)]
    [InlineData(140, CustomerRatingLevel.Excellent, 10)]
    [InlineData(141, CustomerRatingLevel.Good, 5)]
    [InlineData(160, CustomerRatingLevel.Good, 5)]
    [InlineData(161, CustomerRatingLevel.Weak, 0)]
    [InlineData(179, CustomerRatingLevel.Weak, 0)]
    [InlineData(180, CustomerRatingLevel.Rejected, -5)]
    public void Settled_Days_Boundaries(int days, CustomerRatingLevel level, int score)
    {
        var result = CustomerRatingCalculator.Evaluate(Settled(days), AsOf);
        Assert.Equal(level, result.Level);
        Assert.Equal(score, result.Score);
        Assert.True(result.IsSettled);
    }

    [Theory]
    [InlineData(7.1, CustomerRatingLevel.Excellent, 10)]
    [InlineData(7.11, CustomerRatingLevel.Excellent, 10)]
    [InlineData(6.25, CustomerRatingLevel.Good, 5)]
    [InlineData(6.26, CustomerRatingLevel.Good, 5)]
    [InlineData(5.5, CustomerRatingLevel.Weak, 0)]
    [InlineData(5.51, CustomerRatingLevel.Weak, 0)]
    [InlineData(5.49, CustomerRatingLevel.Rejected, -5)]
    public void Active_Rate_Boundaries(double rate, CustomerRatingLevel level, int score)
    {
        // Rate = Receipts * 1000 / (Sales * Days) => Receipts = Rate * Sales * Days / 1000
        const double sales = 1_000_000;
        const int days = 100;
        var receipts = rate * sales * days / 1000.0;
        var saleDate = AsOf.Date.AddDays(-(days - 1));
        var facts = new CustomerRatingFacts(
            CustomerId: 1,
            DelegateId: 10,
            IsLegal: false,
            IsFakeSale: false,
            DateSaleDevice: saleDate,
            LastPaymentDate: AsOf.Date.AddDays(-1),
            AmountTotalSales: sales,
            ReceiptsTotal: receipts,
            AmountRemaining: sales - receipts);

        var result = CustomerRatingCalculator.Evaluate(facts, AsOf);
        Assert.Equal(level, result.Level);
        Assert.Equal(score, result.Score);
        Assert.False(result.IsSettled);
        Assert.NotNull(result.PaymentRate);
        Assert.Equal(rate, result.PaymentRate!.Value, 2);
    }

    [Fact]
    public void Negative_Remaining_Is_Weak_Unless_Legal()
    {
        var facts = new CustomerRatingFacts(1, 10, false, false, AsOf.AddDays(-50), AsOf, 1000, 1200, -200);
        var result = CustomerRatingCalculator.Evaluate(facts, AsOf);
        Assert.Equal(CustomerRatingLevel.Weak, result.Level);
        Assert.Equal(0, result.Score);
        Assert.Contains("سالب", result.Reason);
    }

    [Fact]
    public void No_Sales_Is_Weak()
    {
        var facts = new CustomerRatingFacts(1, 10, false, false, null, null, 0, 0, 0);
        var result = CustomerRatingCalculator.Evaluate(facts, AsOf);
        Assert.Equal(CustomerRatingLevel.Weak, result.Level);
        Assert.Equal(0, result.Score);
        Assert.Contains("بيع", result.Reason);
    }

    [Fact]
    public void Settled_Missing_LastPayment_Is_Incomplete()
    {
        var facts = new CustomerRatingFacts(1, 10, false, false, AsOf.AddDays(-100), null, 1000, 1000, 0);
        var result = CustomerRatingCalculator.Evaluate(facts, AsOf);
        Assert.Equal(CustomerRatingLevel.Weak, result.Level);
        Assert.Contains("غير مكتملة", result.Reason);
    }

    [Fact]
    public void FakeSale_Does_Not_Change_Rating()
    {
        var normal = CustomerRatingCalculator.Evaluate(Settled(100) with { IsFakeSale = false }, AsOf);
        var fake = CustomerRatingCalculator.Evaluate(Settled(100) with { IsFakeSale = true }, AsOf);
        Assert.Equal(normal.Level, fake.Level);
        Assert.Equal(normal.Score, fake.Score);
    }

    [Fact]
    public void Legal_Overrides_Negative_Remaining()
    {
        var facts = new CustomerRatingFacts(1, 10, true, false, AsOf.AddDays(-50), AsOf, 1000, 1200, -200);
        var result = CustomerRatingCalculator.Evaluate(facts, AsOf);
        Assert.Equal(CustomerRatingLevel.Legal, result.Level);
        Assert.Equal(-10, result.Score);
    }

    private static CustomerRatingFacts Settled(int daysToSettle) =>
        new(
            CustomerId: 1,
            DelegateId: 10,
            IsLegal: false,
            IsFakeSale: false,
            DateSaleDevice: AsOf.Date.AddDays(-(daysToSettle - 1)),
            LastPaymentDate: AsOf.Date,
            AmountTotalSales: 1_000_000,
            ReceiptsTotal: 1_000_000,
            AmountRemaining: 0);
}
