using BE_DelegateWebApplication.Services;
using Xunit;

namespace BE_DelegateWebApplication.Tests;

public sealed class FollowerDelegateDayRatingRulesTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(10)]
    public void ValidateScore_accepts_0_to_10(int score)
    {
        var (ok, error) = FollowerDelegateDayRatingRules.ValidateScore(score);
        Assert.True(ok);
        Assert.Null(error);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(11)]
    public void ValidateScore_rejects_out_of_range(int score)
    {
        var (ok, error) = FollowerDelegateDayRatingRules.ValidateScore(score);
        Assert.False(ok);
        Assert.NotNull(error);
    }

    [Fact]
    public void IraqToday_uses_utc_plus_3()
    {
        // 2026-10-20 22:00 UTC => Iraq 2026-10-21
        var utc = new DateTime(2026, 10, 20, 22, 0, 0, DateTimeKind.Utc);
        Assert.Equal(new DateOnly(2026, 10, 21), FollowerDelegateDayRatingRules.IraqToday(utc));
    }
}
