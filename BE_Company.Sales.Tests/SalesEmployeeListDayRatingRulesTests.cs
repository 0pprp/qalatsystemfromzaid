using BE_Company.Sales.Services;
using Xunit;

namespace BE_Company.Sales.Tests;

public sealed class SalesEmployeeListDayRatingRulesTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(10)]
    public void ValidateScore_Accepts0To10(int score)
    {
        var (ok, error) = SalesEmployeeListDayRatingRules.ValidateScore(score);
        Assert.True(ok);
        Assert.Null(error);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(11)]
    public void ValidateScore_RejectsOutOfRange(int score)
    {
        var (ok, error) = SalesEmployeeListDayRatingRules.ValidateScore(score);
        Assert.False(ok);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void IraqToday_UsesUtcPlus3()
    {
        var utc = new DateTime(2026, 10, 24, 22, 0, 0, DateTimeKind.Utc);
        Assert.Equal(new DateOnly(2026, 10, 25), SalesEmployeeListDayRatingRules.IraqToday(utc));
    }

    [Fact]
    public void DeployMarker_IsStable()
    {
        Assert.Equal("SalesEmployeeListDayRating_V1", SalesEmployeeListDayRatingRules.DeployMarker);
    }
}
