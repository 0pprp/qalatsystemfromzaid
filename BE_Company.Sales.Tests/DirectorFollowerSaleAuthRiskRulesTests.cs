using BE_Company.Sales.Services;
using Xunit;

namespace BE_Company.Sales.Tests;

public sealed class DirectorFollowerSaleAuthRiskRulesTests
{
    [Theory]
    [InlineData(false, 5, 5, false)]
    [InlineData(true, null, null, false)]
    [InlineData(true, 7, 8, false)]
    [InlineData(true, 6, 8, true)]
    [InlineData(true, 7, 6, true)]
    [InlineData(true, 5, 4, true)]
    [InlineData(true, 6, null, true)]
    [InlineData(true, null, 6, true)]
    [InlineData(true, 7, null, false)]
    public void IsDangerAuthenticated_UsesAuthOrShopRatingThreshold(
        bool authenticated, int? authRating, int? shopRating, bool expected)
    {
        Assert.Equal(
            expected,
            DirectorFollowerSaleAuthRiskRules.IsDangerAuthenticated(authenticated, authRating, shopRating));
    }
}
