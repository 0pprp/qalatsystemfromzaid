using BE_Company.Sales.Services;
using Xunit;

namespace BE_Company.Sales.Tests;

public sealed class DirectorFollowerOpsTrendRulesTests
{
    [Theory]
    [InlineData(4.5, 4.0, "up")]
    [InlineData(3.0, 3.5, "down")]
    [InlineData(4.0, 4.0, "flat")]
    [InlineData(4.0, 4.005, "flat")]
    [InlineData(4.2, null, "flat")]
    public void ComputeScoreTrend_MatchesFollowerAppRules(double today, double? prior, string expected)
    {
        Assert.Equal(expected, DirectorFollowerOpsTrendRules.ComputeScoreTrend(today, prior));
    }

    [Theory]
    [InlineData(40, 35, "up")]
    [InlineData(10, 12, "down")]
    [InlineData(5, 5, "flat")]
    [InlineData(5, null, "flat")]
    public void ComputeCountTrend_ComparesIntegerCounts(int today, int? prior, string expected)
    {
        Assert.Equal(expected, DirectorFollowerOpsTrendRules.ComputeCountTrend(today, prior));
    }

    [Fact]
    public void WeekAgoDate_IsSevenDaysBefore()
    {
        Assert.Equal(new DateTime(2026, 10, 7), DirectorFollowerOpsTrendRules.WeekAgoDate(new DateTime(2026, 10, 14)));
    }

    [Fact]
    public void YesterdayDate_IsOneDayBefore()
    {
        Assert.Equal(new DateTime(2026, 10, 13), DirectorFollowerOpsTrendRules.YesterdayDate(new DateTime(2026, 10, 14)));
    }
}
