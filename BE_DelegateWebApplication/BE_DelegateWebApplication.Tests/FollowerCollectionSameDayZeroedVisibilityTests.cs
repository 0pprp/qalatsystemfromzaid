using BE_DelegateWebApplication.Services.CollectionPayments;
using Xunit;

namespace BE_DelegateWebApplication.Tests;

/// <summary>
/// Follower جباية list must keep same-day fully-paid customers visible until the next
/// Baghdad business day (03:00), matching GetCustomersDelegateAll for delegate devices.
/// </summary>
public sealed class FollowerCollectionSameDayZeroedVisibilityTests
{
    [Theory]
    [InlineData(155000, false, true)]  // still has remaining
    [InlineData(1, false, true)]
    [InlineData(0, true, true)]        // zeroed today — still show
    [InlineData(0, false, false)]      // zeroed earlier — hide
    [InlineData(-1, false, false)]
    public void IncludeInCollectionList_MatchesDelegateDeviceRule(
        double amountRemaining,
        bool paidOnCurrentBusinessDay,
        bool expected)
    {
        Assert.Equal(
            expected,
            FollowerCollectionListRules.IncludeInCollectionList(amountRemaining, paidOnCurrentBusinessDay));
    }

    [Fact]
    public void DeployMarker_IsPresent()
    {
        Assert.Equal("FollowerCollectionSameDayZeroed_V2", FollowerCollectionListRules.DeployMarker);
    }

    [Fact]
    public void SqlFilter_UsesCustomersPaymentsBusinessDayOr()
    {
        var sql = FollowerCollectionListRules.SqlIncludeCustomerPredicate;
        Assert.Contains("AmountRemaining", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CustomersPayments", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("@IraqBizDate", sql, StringComparison.Ordinal);
        Assert.Contains("DATEADD(HOUR, 3", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IraqBizDate_UsesBaghdadBusinessDateNotCalendarOnly()
    {
        // 02:30 Baghdad = 23:30 previous UTC → business date is previous calendar day
        var utcBefore3am = new DateTime(2026, 10, 23, 23, 30, 0, DateTimeKind.Utc);
        var biz = CollectionPaymentRules.BaghdadBusinessDate(utcBefore3am);
        Assert.Equal(new DateTime(2026, 10, 23), biz);

        var utcAfter3am = new DateTime(2026, 10, 24, 0, 30, 0, DateTimeKind.Utc); // 03:30 Baghdad
        var biz2 = CollectionPaymentRules.BaghdadBusinessDate(utcAfter3am);
        Assert.Equal(new DateTime(2026, 10, 24), biz2);
    }
}
