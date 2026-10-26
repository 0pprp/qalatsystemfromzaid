using BE_Company.Sales.Services;
using Xunit;

namespace BE_Company.Sales.Tests;

public sealed class DirectorDelegateFollowupRulesTests
{
    [Fact]
    public void MaxDaysListActivity_IsForty()
    {
        Assert.Equal(40, DirectorDelegateFollowupRules.MaxDaysListActivity);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(39, true)]
    [InlineData(40, true)]
    [InlineData(41, false)]
    [InlineData(90, false)]
    public void IsListActive_UsesInclusiveFortyDayWindow(int daysAgo, bool expected)
    {
        var asOf = new DateTime(2026, 10, 20);
        var lastPay = asOf.AddDays(-daysAgo);
        Assert.Equal(expected, DirectorDelegateFollowupRules.IsListActive(lastPay, asOf));
    }

    [Fact]
    public void IsListActive_NullLastPayment_IsFalse()
    {
        Assert.False(DirectorDelegateFollowupRules.IsListActive(null, new DateTime(2026, 10, 20)));
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(99_999, true)]
    [InlineData(100_000, false)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    public void IsRemainingLt100k(double remaining, bool expected) =>
        Assert.Equal(expected, DirectorDelegateFollowupRules.IsRemainingLt100k(remaining));

    [Theory]
    [InlineData(100_000, true)]
    [InlineData(199_999, true)]
    [InlineData(99_999, false)]
    [InlineData(200_000, false)]
    public void IsRemaining100To200k(double remaining, bool expected) =>
        Assert.Equal(expected, DirectorDelegateFollowupRules.IsRemaining100To200k(remaining));

    [Theory]
    [InlineData(100_000, true)]
    [InlineData(250_000, true)]
    [InlineData(99_999, false)]
    [InlineData(0, false)]
    public void IsRemainingGe100k(double remaining, bool expected) =>
        Assert.Equal(expected, DirectorDelegateFollowupRules.IsRemainingGe100k(remaining));

    [Fact]
    public void CountRemainingBands_SplitsActiveBalances()
    {
        double[] amounts = [50_000, 80_000, 100_000, 150_000, 250_000, 0];
        var bands = DirectorDelegateFollowupRules.CountRemainingBands(amounts);
        Assert.Equal(2, bands.Lt100k);
        Assert.Equal(2, bands.From100To200k);
        Assert.Equal(3, bands.Ge100k);
    }

    [Theory]
    [InlineData(1, false, true)]
    [InlineData(0, false, false)]
    [InlineData(100, true, false)]
    [InlineData(100, false, true)]
    public void IsRequiredDueEligible_ExcludesZeroAndLegal(double remaining, bool isLegal, bool expected) =>
        Assert.Equal(expected, DirectorDelegateFollowupRules.IsRequiredDueEligible(remaining, isLegal));

    [Fact]
    public void SumRequiredDueInstallment_SkipsLegalAndZeroRemaining()
    {
        (double AmountDaySales, double AmountRemaining, bool IsLegal)[] rows =
        [
            (10_000, 50_000, false),
            (20_000, 50_000, true),
            (30_000, 0, false),
            (5_000, 1, false),
        ];
        Assert.Equal(15_000, DirectorDelegateFollowupRules.SumRequiredDueInstallment(rows));
    }

    [Theory]
    [InlineData(2026, 10, 24, 2026, 10, 23)] // Saturday -> Friday 23
    [InlineData(2026, 10, 23, 2026, 10, 16)] // Friday -> previous Friday 16
    [InlineData(2026, 10, 22, 2026, 10, 16)] // Thursday -> Friday 16
    public void LastFridayBefore_BaghdadCalendar(
        int y, int m, int d, int ey, int em, int ed)
    {
        var asOf = new DateTime(y, m, d);
        Assert.Equal(new DateTime(ey, em, ed), DirectorDelegateFollowupRules.LastFridayBefore(asOf));
    }

    [Fact]
    public void LastNCalendarDaysEndingYesterday_ReturnsThirtyDescendingFromYesterday()
    {
        var asOf = new DateTime(2026, 10, 24);
        var days = DirectorDelegateFollowupRules.LastNCalendarDaysEndingYesterday(asOf, 30);
        Assert.Equal(30, days.Count);
        Assert.Equal(new DateTime(2026, 10, 23), days[0]);
        Assert.Equal(new DateTime(2026, 9, 24), days[29]);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(10, true)]
    [InlineData(11, false)]
    [InlineData(-1, false)]
    public void IsValidBoardOperatorScore(int score, bool expected) =>
        Assert.Equal(expected, DirectorDelegateFollowupRules.IsValidBoardOperatorScore(score));

    [Fact]
    public void CollectedHistoryDays_IsThirty() =>
        Assert.Equal(30, DirectorDelegateFollowupRules.CollectedHistoryDays);

    [Fact]
    public void LastNFridays_ReturnsThirtyFridaysDescending()
    {
        var asOf = new DateTime(2026, 10, 24); // Saturday
        var fridays = DirectorDelegateFollowupRules.LastNFridays(asOf, 30);
        Assert.Equal(30, fridays.Count);
        Assert.Equal(new DateTime(2026, 10, 23), fridays[0]);
        Assert.Equal(DayOfWeek.Friday, fridays[0].DayOfWeek);
        Assert.Equal(new DateTime(2026, 10, 16), fridays[1]);
        Assert.All(fridays, d => Assert.Equal(DayOfWeek.Friday, d.DayOfWeek));
    }

    [Fact]
    public void CountConversions_ImprovedDeclinedUnchanged()
    {
        (string?, string?)[] pairs =
        [
            ("سيء", "ممتاز"),
            ("ممتاز", "سيء"),
            ("جيد", "جيد"),
            ("ضعيف", "متلكئ"),
            (null, "ممتاز"),
        ];
        var c = DirectorDelegateFollowupRules.CountConversions(pairs);
        Assert.Equal(1, c.Improved);
        Assert.Equal(2, c.Declined);
        Assert.Equal(2, c.Unchanged);
    }

    [Fact]
    public void BuildArrivalBreakdown_GroupsFromLabelsPerTarget()
    {
        (string?, string?)[] pairs =
        [
            ("سيء", "ممتاز"),
            ("ضعيف", "ممتاز"),
            ("سيء", "ممتاز"),
            ("ممتاز", "ممتاز"),
            ("ضعيف", "قانونية"),
            ("جيد", "غير شيء"),
        ];
        var targets = new[] { "ممتاز", "قانونية", "غير شيء", "سيء" };
        var map = DirectorDelegateFollowupRules.BuildArrivalBreakdown(pairs, targets);

        Assert.Equal(3, map["ممتاز"].Sum(x => x.Count));
        Assert.Equal(2, map["ممتاز"].First(x => x.From == "سيء").Count);
        Assert.Equal(1, map["ممتاز"].First(x => x.From == "ضعيف").Count);
        Assert.Equal(1, map["قانونية"].Single().Count);
        Assert.Equal("ضعيف", map["قانونية"].Single().From);
        Assert.Equal(1, map["غير شيء"].Single().Count);
        Assert.Empty(map["سيء"]);
    }
}
