using BE_Company.Sales.Services;
using Xunit;

namespace BE_Company.Sales.Tests;

public sealed class WaiverDiscountRulesTests
{
    [Theory]
    [InlineData(1_000_000, 200_000, 100_000, 100_000)]
    [InlineData(1_000_000, 200_000, 900_000, 800_000)]
    [InlineData(500_000, 500_000, 50_000, 0)]
    [InlineData(500_000, 0, 500_000, 500_000)]
    public void CapDiscount_NeverExceedsRemaining(
        double saleTotal, double paidTotal, double requested, double expected)
    {
        Assert.Equal(expected, WaiverDiscountRules.CapDiscount(saleTotal, paidTotal, requested));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CapDiscount_RejectsNonPositiveRequest(double requested)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            WaiverDiscountRules.CapDiscount(1_000_000, 0, requested));
    }

    [Fact]
    public void CustomerNote_IncludesCustomerAmountAndToday()
    {
        var note = WaiverDiscountRules.BuildCustomerNote("أحمد علي", 125000, new DateTime(2026, 10, 17));
        Assert.Contains("تم التنازل عن الزبون أحمد علي بمبلغ", note, StringComparison.Ordinal);
        Assert.Contains("125,000", note, StringComparison.Ordinal);
        Assert.Contains("من قبل المدير المفوض بتاريخ", note, StringComparison.Ordinal);
        Assert.Contains("2026-10-17", note, StringComparison.Ordinal);
    }

    [Fact]
    public void DecisionTypes_AreStable()
    {
        Assert.Equal("طلب تنازل", WaiverDiscountRules.PendingDecisionType);
        Assert.Equal("تنازل", WaiverDiscountRules.FinalDecisionType);
    }
}
