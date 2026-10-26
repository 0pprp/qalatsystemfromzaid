using BE_DelegateWebApplication.Services;
using Xunit;

namespace BE_DelegateWebApplication.Tests;

public sealed class FollowerSaleAuthRulesTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(10, true)]
    [InlineData(11, false)]
    [InlineData(-1, false)]
    public void Rating_MustBe_1_To_10(int rating, bool ok)
    {
        Assert.Equal(ok, FollowerSaleAuthRules.IsValidRating(rating));
    }

    [Theory]
    [InlineData(7, null, true)]
    [InlineData(7, "", true)]
    [InlineData(7, "  ", true)]
    [InlineData(10, null, true)]
    [InlineData(6, null, false)]
    [InlineData(6, "", false)]
    [InlineData(6, "  ", false)]
    [InlineData(1, "ضعيف", true)]
    [InlineData(9, "ممتاز", true)]
    public void Note_Optional_When_Rating_AtLeast_7(int rating, string? note, bool ok)
    {
        Assert.Equal(ok, FollowerSaleAuthRules.IsValidNoteForRating(rating, note));
    }

    [Fact]
    public void PreviousYearMonth_Rolls_Across_Year()
    {
        Assert.Equal(202512, FollowerListStatsRules.PreviousYearMonth(202601));
        Assert.Equal(202602, FollowerListStatsRules.PreviousYearMonth(202603));
    }

    [Fact]
    public void Delta_Is_Current_Minus_Baseline()
    {
        Assert.Equal(1500m, FollowerListStatsRules.Delta(5000m, 3500m));
        Assert.Equal(-100m, FollowerListStatsRules.Delta(100m, 200m));
    }

    [Fact]
    public void YearMonth_From_Date()
    {
        Assert.Equal(202609, FollowerListStatsRules.ToYearMonth(new DateTime(2026, 9, 20)));
    }

    [Theory]
    [InlineData(6, true)]
    [InlineData(1, true)]
    [InlineData(7, false)]
    [InlineData(10, false)]
    public void WrongSale_When_Rating_AtMost_6(int rating, bool wrong)
    {
        Assert.Equal(wrong, FollowerSaleAuthRules.IsWrongSaleRating(rating));
    }

    [Fact]
    public void ShopMoney_Must_Be_Positive()
    {
        Assert.True(FollowerSaleAuthRules.IsValidShopMoney(1));
        Assert.False(FollowerSaleAuthRules.IsValidShopMoney(0));
        Assert.False(FollowerSaleAuthRules.IsValidShopMoney(-5));
    }

    [Fact]
    public void FormatShopEvaluationBlock_Contains_Core_Fields()
    {
        var block = FollowerSaleAuthRules.FormatShopEvaluationBlock(
            5, "ضعيف", 2_000_000, 80_000, "أحمد", "07801234567", 1_500_000);
        Assert.Contains("التقييم: 5", block);
        Assert.Contains("أحمد", block);
        Assert.Contains("07801234567", block);
        Assert.Contains("سعر البيع", block);
        Assert.DoesNotContain("تقييم المتجر / مبيعات خاطئة", block);
    }

    [Fact]
    public void FormatShopEvaluationBlock_WrongSaleTag_OnlyWhenRequested()
    {
        var plain = FollowerSaleAuthRules.FormatShopEvaluationBlock(5, "ضعيف", 1, 1);
        Assert.DoesNotContain("مبيعات خاطئة", plain);
        var tagged = FollowerSaleAuthRules.FormatShopEvaluationBlock(5, "ضعيف", 1, 1, includeWrongSaleTag: true);
        Assert.Contains("مبيعات خاطئة", tagged);
    }

    [Fact]
    public void SanitizeNotesForSalesEmployee_RemovesScareTitle()
    {
        var raw = "القائمة: الميثاق (#5)\nتقييم المتجر / مبيعات خاطئة\nالمصدر: مندوب الجباية\nالتقييم: 10";
        var clean = FollowerSaleAuthRules.SanitizeNotesForSalesEmployee(raw);
        Assert.DoesNotContain("مبيعات خاطئة", clean);
        Assert.Contains("المصدر: مندوب الجباية", clean);
        Assert.Contains("القائمة: الميثاق", clean);
    }
}
