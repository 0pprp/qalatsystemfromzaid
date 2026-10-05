using BE_Company.Sales.Rating;
using Xunit;

namespace BE_Company.Sales.Tests;

public class CustomerRatingCalculatorTests
{
    private static readonly DateTime AsOf = new(2026, 3, 1, 0, 0, 0, DateTimeKind.Unspecified);

    [Fact]
    public void IsLegal_Does_Not_Affect_Rating()
    {
        var normal = CustomerRatingCalculator.Evaluate(Settled(100) with { IsLegal = false }, AsOf);
        var legal = CustomerRatingCalculator.Evaluate(Settled(100) with { IsLegal = true }, AsOf);
        Assert.Equal(CustomerRatingLevel.Excellent, normal.Level);
        Assert.Equal(normal.Level, legal.Level);
        Assert.Equal(normal.Score, legal.Score);
        Assert.Equal("ممتاز", legal.RatingLabel);
        Assert.True(legal.IsLegal);
        Assert.False(normal.IsLegal);
    }

    [Theory]
    [InlineData(100, CustomerRatingLevel.Excellent, 10)]
    [InlineData(140, CustomerRatingLevel.Excellent, 10)]
    [InlineData(141, CustomerRatingLevel.Good, 5)]
    [InlineData(160, CustomerRatingLevel.Good, 5)]
    [InlineData(161, CustomerRatingLevel.Weak, 0)]
    [InlineData(179, CustomerRatingLevel.Weak, 0)]
    [InlineData(180, CustomerRatingLevel.Rejected, -5)] // متلكئ
    [InlineData(199, CustomerRatingLevel.Rejected, -5)]
    [InlineData(200, CustomerRatingLevel.Bad, -10)] // سيء
    [InlineData(250, CustomerRatingLevel.Bad, -10)]
    public void Settled_Days_Boundaries(int days, CustomerRatingLevel level, int score)
    {
        var result = CustomerRatingCalculator.Evaluate(Settled(days), AsOf);
        Assert.Equal(level, result.Level);
        Assert.Equal(score, result.Score);
        Assert.Equal(CustomerRatingLabels.FromLevel(level), result.RatingLabel);
        Assert.True(result.IsSettled);
    }

    [Theory]
    [InlineData(7.1, CustomerRatingLevel.Excellent, 10)]
    [InlineData(7.11, CustomerRatingLevel.Excellent, 10)]
    [InlineData(6.25, CustomerRatingLevel.Good, 5)]
    [InlineData(6.26, CustomerRatingLevel.Good, 5)]
    [InlineData(5.56, CustomerRatingLevel.Weak, 0)]
    [InlineData(5.5556, CustomerRatingLevel.Weak, 0)]
    [InlineData(5.5, CustomerRatingLevel.Rejected, -5)]
    [InlineData(5.0, CustomerRatingLevel.Rejected, -5)]
    [InlineData(4.99, CustomerRatingLevel.Bad, -10)]
    public void Active_Rate_Boundaries(double rate, CustomerRatingLevel level, int score)
    {
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
    public void Labels_Are_Mutalaki_And_Sayi_Not_Legal()
    {
        Assert.Equal("متلكئ", CustomerRatingLabels.Rejected);
        Assert.Equal("سيء", CustomerRatingLabels.Bad);
        Assert.DoesNotContain(
            CustomerRatingLevel.Legal,
            new[]
            {
                CustomerRatingCalculator.ClassifySettledDays(100).Level,
                CustomerRatingCalculator.ClassifySettledDays(200).Level,
                CustomerRatingCalculator.ClassifyActiveRate(7.1).Level,
                CustomerRatingCalculator.ClassifyActiveRate(1).Level,
            });
    }

    [Fact]
    public void EvaluateMany_Matches_Sequential_And_Scales()
    {
        var facts = Enumerable.Range(1, 5_000)
            .Select(i => Settled(100 + (i % 120)) with { CustomerId = i })
            .ToList();

        var sequential = facts.Select(f => CustomerRatingCalculator.Evaluate(f, AsOf)).ToList();
        var parallel = CustomerRatingCalculator.EvaluateMany(facts, AsOf, parallelThreshold: 100);

        Assert.Equal(sequential.Count, parallel.Count);
        for (var i = 0; i < sequential.Count; i++)
        {
            Assert.Equal(sequential[i].Level, parallel[i].Level);
            Assert.Equal(sequential[i].Score, parallel[i].Score);
        }
    }

    [Fact]
    public void Negative_Remaining_Is_Weak_Even_If_Legal()
    {
        var facts = new CustomerRatingFacts(1, 10, true, false, AsOf.AddDays(-50), AsOf, 1000, 1200, -200);
        var result = CustomerRatingCalculator.Evaluate(facts, AsOf);
        Assert.Equal(CustomerRatingLevel.Weak, result.Level);
        Assert.Equal(0, result.Score);
        Assert.Contains("سالب", result.Reason);
        Assert.True(result.IsLegal);
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
    public void RatingEquation_Default_1_Does_Not_Change_Active_Rate()
    {
        const double sales = 1_000_000;
        const int days = 100;
        const double receipts = 40_000;
        var saleDate = AsOf.Date.AddDays(-(days - 1));
        var facts = new CustomerRatingFacts(
            1, 10, false, false, saleDate, AsOf, sales, receipts, sales - receipts,
            RatingEquationFactor: 1);

        var result = CustomerRatingCalculator.Evaluate(facts, AsOf);
        var expectedRate = receipts * 1000.0 / (sales * days);
        Assert.Equal(expectedRate, result.PaymentRate!.Value, 4);
        Assert.DoesNotContain("معادلة", result.Reason);
    }

    [Fact]
    public void RatingEquation_2_Doubles_Active_Receipts_For_Rating_Only()
    {
        const double sales = 1_000_000;
        const int days = 100;
        const double receipts = 40_000;
        var saleDate = AsOf.Date.AddDays(-(days - 1));
        var facts = new CustomerRatingFacts(
            1, 10, false, false, saleDate, AsOf, sales, receipts, sales - receipts,
            RatingEquationFactor: 2);

        var result = CustomerRatingCalculator.Evaluate(facts, AsOf);
        var expectedRate = (receipts * 2) * 1000.0 / (sales * days);
        Assert.Equal(expectedRate, result.PaymentRate!.Value, 4);
        Assert.Contains("×2", result.Reason);
        // Remaining still based on real receipts — facts unchanged by Evaluate
        Assert.Equal(sales - receipts, facts.AmountRemaining);
        Assert.Equal(receipts, facts.ReceiptsTotal);
    }

    [Fact]
    public void RatingEquation_2_Compresses_Settled_Days_For_Classification()
    {
        // 280 calendar days → with ×2 effective 140 → ممتاز
        var result = CustomerRatingCalculator.Evaluate(
            Settled(280) with { RatingEquationFactor = 2 }, AsOf);
        Assert.Equal(CustomerRatingLevel.Excellent, result.Level);
        Assert.Equal(10, result.Score);
        Assert.Equal(280, result.DaysToSettle);
        Assert.Contains("فعلي للتقييم 140", result.Reason);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    public void RatingEquation_Invalid_Treated_As_1(double factor)
    {
        var withBad = CustomerRatingCalculator.Evaluate(Settled(200) with { RatingEquationFactor = factor }, AsOf);
        var withOne = CustomerRatingCalculator.Evaluate(Settled(200) with { RatingEquationFactor = 1 }, AsOf);
        Assert.Equal(withOne.Level, withBad.Level);
        Assert.Equal(withOne.Score, withBad.Score);
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
