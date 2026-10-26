using BE_Company.Sales.Rating;
using BE_Company.Sales.Services;
using Xunit;

namespace BE_Company.Tests;

public sealed class BmCustomerDeclineRulesTests
{
    [Theory]
    [InlineData("ممتاز", "سيء", true)]
    [InlineData("ممتاز", "متلكئ", true)]
    [InlineData("ممتاز", "ضعيف", false)]
    [InlineData("جيد", "سيء", false)]
    [InlineData("ممتاز", "ممتاز", false)]
    [InlineData(null, "سيء", false)]
    public void IsTargetDecline_OnlyExcellentToBadOrLagging(string? from, string? to, bool expected)
    {
        Assert.Equal(expected, BmCustomerDeclineService.IsTargetDecline(from, to));
        Assert.Equal(CustomerRatingLabels.Excellent, "ممتاز");
        Assert.Equal(CustomerRatingLabels.Bad, "سيء");
        Assert.Equal(CustomerRatingLabels.Rejected, "متلكئ");
    }

    [Fact]
    public void LookbackDays_IsFifteen()
    {
        Assert.Equal(15, BmCustomerDeclineService.LookbackDays);
    }
}
