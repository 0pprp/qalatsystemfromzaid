using BE_DelegateWebApplication.Services;
using Xunit;

namespace BE_DelegateWebApplication.Tests;

/// <summary>
/// Contract checks for first-collection shop eval → WrongSale (≤6).
/// </summary>
public sealed class DelegateFirstCollectionEvalRulesTests
{
    [Theory]
    [InlineData(1, true)]
    [InlineData(6, true)]
    [InlineData(7, false)]
    [InlineData(10, false)]
    public void WrongSale_threshold_matches_first_collection(int rating, bool wrong)
    {
        Assert.Equal(wrong, FollowerSaleAuthRules.IsWrongSaleRating(rating));
    }

    [Fact]
    public void Format_block_includes_sale_amount_when_present()
    {
        var block = FollowerSaleAuthRules.FormatShopEvaluationBlock(
            5, "ضعيف", 100000, 20000, "زبون", "07701234567", 500000);
        Assert.Contains("التقييم: 5", block);
        Assert.Contains("سعر البيع:", block);
        Assert.Contains("500", block);
        Assert.Contains("مبيعات خاطئة", block);
    }
}
