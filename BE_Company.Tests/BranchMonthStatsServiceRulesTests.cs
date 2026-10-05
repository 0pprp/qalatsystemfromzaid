using BE_Company.Sales.Services;
using Xunit;

namespace BE_Company.Tests;

public sealed class BranchMonthStatsServiceRulesTests
{
    [Fact]
    public void ToYearMonth_UsesCalendarMonth()
    {
        Assert.Equal(202609, BranchMonthStatsService.ToYearMonth(new DateTime(2026, 9, 28)));
    }

    [Fact]
    public void PreviousYearMonth_RollsAcrossYear()
    {
        Assert.Equal(202512, BranchMonthStatsService.PreviousYearMonth(202601));
        Assert.Equal(202608, BranchMonthStatsService.PreviousYearMonth(202609));
    }
}
