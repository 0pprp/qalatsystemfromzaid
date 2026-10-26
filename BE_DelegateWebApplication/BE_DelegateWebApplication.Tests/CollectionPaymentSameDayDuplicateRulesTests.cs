using BE_DelegateWebApplication.Services.CollectionPayments;
using Xunit;

namespace BE_DelegateWebApplication.Tests;

public sealed class CollectionPaymentSameDayDuplicateRulesTests
{
    [Fact]
    public void BaghdadBusinessDate_Before3am_RollsToPreviousDay()
    {
        // 2026-10-11 01:30 Baghdad = 2026-10-10 22:30 UTC → business day 2026-10-10
        var utc = new DateTime(2026, 10, 10, 22, 30, 0, DateTimeKind.Utc);
        var biz = CollectionPaymentRules.BaghdadBusinessDate(utc);
        Assert.Equal(new DateTime(2026, 10, 10), biz);
    }

    [Fact]
    public void BaghdadBusinessDate_After3am_SameCalendarDay()
    {
        // 2026-10-11 11:34 Baghdad = 2026-10-11 08:34 UTC
        var utc = new DateTime(2026, 10, 11, 8, 34, 0, DateTimeKind.Utc);
        var biz = CollectionPaymentRules.BaghdadBusinessDate(utc);
        Assert.Equal(new DateTime(2026, 10, 11), biz);
    }

    [Fact]
    public void SameDayMorningAndAfternoon_SameBusinessDay()
    {
        var morning = new DateTime(2026, 10, 11, 8, 34, 0, DateTimeKind.Utc);
        var afternoon = new DateTime(2026, 10, 11, 14, 6, 0, DateTimeKind.Utc);
        Assert.True(CollectionPaymentRules.IsSameBusinessDay(morning, afternoon));
    }

    [Fact]
    public void SameAmount_WithinTolerance()
    {
        Assert.True(CollectionPaymentRules.IsSameAmountIqd(100000, 100000));
        Assert.True(CollectionPaymentRules.IsSameAmountIqd(100000, 100000.4));
        Assert.False(CollectionPaymentRules.IsSameAmountIqd(100000, 100001.5));
    }

    [Fact]
    public void DuplicateMessage_IsArabicAndStable()
    {
        Assert.Contains("اليوم", CollectionPaymentRules.SameDaySameAmountMessage);
        Assert.Contains("التكرار", CollectionPaymentRules.SameDaySameAmountMessage);
    }
}
