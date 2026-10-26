using Xunit;
using BE_DelegateWebApplication.Services;
using BE_DelegateWebApplication.Services.FollowerRating;

namespace BE_DelegateWebApplication.Tests;

public sealed class FollowerListTrendAndDeclineContractTests
{
    [Theory]
    [InlineData(5.0, null, "flat")]
    [InlineData(5.0, 5.0, "flat")]
    [InlineData(5.005, 5.0, "flat")]
    [InlineData(6.0, 5.0, "up")]
    [InlineData(4.0, 5.0, "down")]
    public void Trend_ComparesTodayToWeekAgo(double today, double? weekAgo, string expected)
    {
        Assert.Equal(expected, FollowerListTrendRules.ComputeTrend(today, weekAgo));
    }

    [Fact]
    public void WeekAgoDate_IsSevenCalendarDaysBefore()
    {
        var asOf = new DateTime(2026, 10, 6);
        Assert.Equal(new DateTime(2026, 9, 29), FollowerListTrendRules.WeekAgoDate(asOf));
    }

    [Fact]
    public void MonthAgoDate_IsSameDayPreviousMonth()
    {
        Assert.Equal(new DateTime(2026, 9, 7), FollowerListTrendRules.MonthAgoDate(new DateTime(2026, 10, 7)));
        // AddMonths clamps end-of-month (31 Mar → 28 Feb).
        Assert.Equal(new DateTime(2026, 2, 28), FollowerListTrendRules.MonthAgoDate(new DateTime(2026, 3, 31)));
    }

    [Theory]
    [InlineData(6.0, 5.0, "up")]
    [InlineData(4.0, 5.0, "down")]
    [InlineData(5.0, 5.0, "flat")]
    public void MonthTrend_UsesSameComputeTrend(double today, double? prior, string expected)
    {
        Assert.Equal(expected, FollowerListTrendRules.ComputeTrend(today, prior));
    }

    [Theory]
    [InlineData(FollowerCustomerRatingLabels.Excellent, FollowerCustomerRatingLabels.Weak, true)]
    [InlineData(FollowerCustomerRatingLabels.Good, FollowerCustomerRatingLabels.Rejected, true)]
    [InlineData(FollowerCustomerRatingLabels.Weak, FollowerCustomerRatingLabels.Bad, true)]
    [InlineData(FollowerCustomerRatingLabels.Excellent, FollowerCustomerRatingLabels.Bad, false)]
    [InlineData(FollowerCustomerRatingLabels.Good, FollowerCustomerRatingLabels.Weak, false)]
    public void TargetTransitions(string from, string to, bool expected)
    {
        Assert.Equal(expected, FollowerDeclineRules.IsTargetTransition(from, to));
    }

    [Fact]
    public void Declining_RequiresTenPointDropWithinSevenDays()
    {
        Assert.True(FollowerDeclineRules.IsDeclining("ممتاز", 10, "ضعيف", 0, 5));
        Assert.True(FollowerDeclineRules.IsDeclining("جيد", 5, "متلكئ", -5, 7));
        Assert.True(FollowerDeclineRules.IsDeclining("ضعيف", 0, "سيء", -10, 1));
        Assert.False(FollowerDeclineRules.IsDeclining("ممتاز", 10, "ضعيف", 0, 8));
        Assert.False(FollowerDeclineRules.IsDeclining("ممتاز", 10, "جيد", 5, 3));
    }

    [Fact]
    public void CollectionLoginLockMessage_IsArabicAndClear()
    {
        Assert.Contains("جباية المتابع", FollowerCollectionLoginGate.LockedMessage);
        Assert.Contains("غير فعال", FollowerCollectionLoginGate.LockedMessage);
    }

    [Fact]
    public void FollowerCollectionPayment_RequiresEnabledList()
    {
        Assert.Contains("غير مفعّلة", FollowerCollectionPaymentService.NotEnabledMessage);
        Assert.Contains("غير مرتبطة", FollowerCollectionPaymentService.NotLinkedMessage);
    }
}
