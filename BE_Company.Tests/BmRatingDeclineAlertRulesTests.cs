using BE_Company.Sales.Services;
using Xunit;

namespace BE_Company.Tests;

public sealed class BmRatingDeclineAlertRulesTests
{
    [Theory]
    [InlineData(3.0, 2.7, true)]
    [InlineData(3.0, 2.71, false)]
    [InlineData(3.0, 2.699999, true)]
    [InlineData(2.5, 2.5, false)]
    [InlineData(2.0, 2.5, false)]
    public void IsDecline_UsesPointThreeThreshold(double oldScore, double newScore, bool expected)
    {
        Assert.Equal(expected, BmRatingDeclineAlertService.IsDecline(oldScore, newScore));
        Assert.Equal(0.3, BranchMonthStatsService.DeclineAlertThreshold, 3);
    }

    [Fact]
    public void LookbackDays_IsSeven()
    {
        Assert.Equal(7, BmRatingDeclineAlertService.LookbackDays);
    }
}

public sealed class BranchMonthStatsScopeRulesTests
{
    [Fact]
    public void PreviousYearMonth_StillWorksAfterScopedService()
    {
        Assert.Equal(202512, BranchMonthStatsService.PreviousYearMonth(202601));
        Assert.Equal(202609, BranchMonthStatsService.ToYearMonth(new DateTime(2026, 9, 15)));
    }
}
