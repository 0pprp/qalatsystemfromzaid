using BE_SalesEmployee.MainFollower.Domain;
using Xunit;

namespace BE_SalesEmployee.Tests;

public sealed class SaleReviewRulesTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    [InlineData(-1)]
    public void Score_must_be_1_to_10(int score)
    {
        Assert.NotNull(SaleReviewRules.Validate(score, note: "ملاحظة طويلة كافية"));
    }

    [Fact]
    public void Score_7_or_above_allows_empty_note()
    {
        Assert.Null(SaleReviewRules.Validate(7, note: null));
        Assert.Null(SaleReviewRules.Validate(10, note: "  "));
        Assert.Null(SaleReviewRules.Validate(7, note: "مراقبة جيدة"));
    }

    [Fact]
    public void Score_6_or_below_requires_note()
    {
        Assert.NotNull(SaleReviewRules.Validate(6, note: null));
        Assert.NotNull(SaleReviewRules.Validate(1, note: ""));
        Assert.Null(SaleReviewRules.Validate(5, note: "ضعيف الأداء"));
    }
}
