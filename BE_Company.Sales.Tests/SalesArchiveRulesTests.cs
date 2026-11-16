using BE_Company.Sales.Services;
using Xunit;

namespace BE_Company.Sales.Tests;

public sealed class SalesArchiveRulesTests
{
    [Theory]
    [InlineData(2026, 2, 1, 2026, 2, 1, 2026, 2, 10)]
    [InlineData(2026, 2, 10, 2026, 2, 1, 2026, 2, 10)]
    [InlineData(2026, 2, 11, 2026, 2, 11, 2026, 2, 20)]
    [InlineData(2026, 2, 20, 2026, 2, 11, 2026, 2, 20)]
    [InlineData(2026, 2, 21, 2026, 2, 21, 2026, 2, 28)]
    [InlineData(2026, 2, 28, 2026, 2, 21, 2026, 2, 28)]
    [InlineData(2024, 2, 29, 2024, 2, 21, 2024, 2, 29)]
    [InlineData(2026, 1, 31, 2026, 1, 21, 2026, 1, 31)]
    [InlineData(2026, 4, 30, 2026, 4, 21, 2026, 4, 30)]
    [InlineData(2026, 3, 1, 2026, 3, 1, 2026, 3, 10)]
    public void PeriodFor_SplitsMonthIntoThreeWindows(
        int year, int month, int day,
        int startYear, int startMonth, int startDay,
        int endYear, int endMonth, int endDay)
    {
        var period = SalesArchiveRules.PeriodFor(new DateOnly(year, month, day));
        Assert.Equal(new DateOnly(startYear, startMonth, startDay), period.Start);
        Assert.Equal(new DateOnly(endYear, endMonth, endDay), period.End);
    }

    [Fact]
    public void PeriodFinished_OpensTheDayAfterTheWindow()
    {
        var end = new DateOnly(2026, 2, 10);
        Assert.False(SalesArchiveRules.PeriodFinished(end, new DateOnly(2026, 2, 10)));
        Assert.True(SalesArchiveRules.PeriodFinished(end, new DateOnly(2026, 2, 11)));
    }

    [Fact]
    public void IraqToday_UsesUtcPlusThree()
    {
        var utc = new DateTime(2026, 2, 10, 21, 30, 0, DateTimeKind.Utc);
        Assert.Equal(new DateOnly(2026, 2, 11), SalesArchiveRules.IraqToday(utc));
    }

    [Fact]
    public void Phase_CurrentWindowCannotBeSent()
    {
        var phase = SalesArchiveRules.Phase(
            new DateOnly(2026, 2, 10),
            new DateOnly(2026, 2, 10),
            itemCount: 10,
            approvedCount: 0,
            refusedCount: 0,
            withAgentCount: 10);
        Assert.Equal(SalesArchiveRules.PhaseOpen, phase);
        Assert.False(SalesArchiveRules.CanSend(phase));
    }

    [Fact]
    public void Phase_FinishedWindowIsReadyUntilSent()
    {
        var phase = SalesArchiveRules.Phase(
            new DateOnly(2026, 2, 10),
            new DateOnly(2026, 2, 11),
            itemCount: 10,
            approvedCount: 0,
            refusedCount: 0,
            withAgentCount: 10);
        Assert.Equal(SalesArchiveRules.PhaseReady, phase);
        Assert.True(SalesArchiveRules.CanSend(phase));
    }

    [Fact]
    public void Phase_SentBundleWaitsForArrival()
    {
        var phase = SalesArchiveRules.Phase(
            new DateOnly(2026, 2, 10),
            new DateOnly(2026, 2, 12),
            itemCount: 10,
            approvedCount: 0,
            refusedCount: 0,
            withAgentCount: 0);
        Assert.Equal(SalesArchiveRules.PhaseInTransit, phase);
        Assert.False(SalesArchiveRules.CanSend(phase));
        Assert.False(SalesArchiveRules.ShownWhenHidingSentAndApproved(phase));
    }

    [Fact]
    public void Phase_PartialRefusalKeepsTheBundleOpenForResend()
    {
        var phase = SalesArchiveRules.Phase(
            new DateOnly(2026, 2, 10),
            new DateOnly(2026, 2, 15),
            itemCount: 10,
            approvedCount: 8,
            refusedCount: 2,
            withAgentCount: 0);
        Assert.Equal(SalesArchiveRules.PhaseNeedsFix, phase);
        Assert.True(SalesArchiveRules.CanSend(phase));
        Assert.True(SalesArchiveRules.ShownWhenHidingSentAndApproved(phase));
    }

    [Fact]
    public void Phase_AllApprovedIsComplete()
    {
        var phase = SalesArchiveRules.Phase(
            new DateOnly(2026, 2, 20),
            new DateOnly(2026, 2, 21),
            itemCount: 4,
            approvedCount: 4,
            refusedCount: 0,
            withAgentCount: 0);
        Assert.Equal(SalesArchiveRules.PhaseApproved, phase);
        Assert.False(SalesArchiveRules.CanSend(phase));
        Assert.False(SalesArchiveRules.ShownWhenHidingSentAndApproved(phase));
    }

    [Fact]
    public void ApplyReview_RequiresADecisionForEveryPendingPaper()
    {
        var items = new (int, string)[]
        {
            (1, SalesArchiveRules.ItemPending),
            (2, SalesArchiveRules.ItemPending)
        };
        var (ok, error, _) = SalesArchiveRules.ApplyReview(
            items,
            [new SalesArchiveRules.ReviewDecision(1, true, null)]);
        Assert.False(ok);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void ApplyReview_RefusedPaperNeedsANote()
    {
        var items = new (int, string)[] { (7, SalesArchiveRules.ItemPending) };
        var (ok, _, updates) = SalesArchiveRules.ApplyReview(
            items,
            [new SalesArchiveRules.ReviewDecision(7, false, "  ")]);
        Assert.False(ok);
        Assert.Empty(updates);
    }

    [Fact]
    public void ApplyReview_ApprovesSelectedAndRefusesTheRest()
    {
        var items = new (int, string)[]
        {
            (1, SalesArchiveRules.ItemPending),
            (2, SalesArchiveRules.ItemPending),
            (3, SalesArchiveRules.ItemApproved)
        };
        var (ok, error, updates) = SalesArchiveRules.ApplyReview(items,
        [
            new SalesArchiveRules.ReviewDecision(1, true, null),
            new SalesArchiveRules.ReviewDecision(2, false, "العقد ناقص")
        ]);
        Assert.True(ok);
        Assert.Null(error);
        Assert.Equal(2, updates.Count);
        Assert.Equal(SalesArchiveRules.ItemApproved, updates[0].Status);
        Assert.Equal(SalesArchiveRules.ItemRefused, updates[1].Status);
        Assert.Equal("العقد ناقص", updates[1].Note);
    }

    [Fact]
    public void ValidateCourier_RequiresNameAndIraqiMobile()
    {
        var bad = SalesArchiveRules.ValidateCourier("علي", "123");
        Assert.False(bad.Ok);
        var good = SalesArchiveRules.ValidateCourier("  علي حسين  ", "+9647701234567");
        Assert.True(good.Ok);
        Assert.Equal("علي حسين", good.Name);
        Assert.Equal("07701234567", good.Phone);
    }

    [Fact]
    public void ItemLabel_JoinsSoldProducts()
    {
        var label = SalesArchiveRules.ItemLabel([("ثلاجة", 1), ("مكيف", 2)]);
        Assert.Equal("ثلاجة، مكيف × 2", label);
    }

    [Fact]
    public void ValidateDeliveryPhotos_AllowsSeveralAndCapsTheSet()
    {
        var ok = SalesArchiveRules.ValidateDeliveryPhotos(3, [100, 200, SalesArchiveRules.MaxDeliveryPhotoBytes]);
        Assert.True(ok.Ok);
        var tooMany = SalesArchiveRules.ValidateDeliveryPhotos(SalesArchiveRules.MaxDeliveryPhotos + 1, []);
        Assert.False(tooMany.Ok);
        var tooBig = SalesArchiveRules.ValidateDeliveryPhotos(1, [SalesArchiveRules.MaxDeliveryPhotoBytes + 1]);
        Assert.False(tooBig.Ok);
    }

    [Fact]
    public void DeployMarker_IsStable()
    {
        Assert.Equal("SalesArchive_V1", SalesArchiveRules.DeployMarker);
    }

    [Fact]
    public void Phase_PapersInTheCommitteeChainAreInTransitForTheAgent()
    {
        var phase = SalesArchiveRules.Phase(
            new DateOnly(2026, 2, 10), new DateOnly(2026, 2, 12),
            itemCount: 5, approvedCount: 2, refusedCount: 0, withAgentCount: 0);
        Assert.Equal(SalesArchiveRules.PhaseInTransit, phase);
        Assert.False(SalesArchiveRules.CanSend(phase));
    }

    [Fact]
    public void Phase_LateSaleInASentBundleMakesItSendableAgain()
    {
        var phase = SalesArchiveRules.Phase(
            new DateOnly(2026, 2, 10), new DateOnly(2026, 2, 12),
            itemCount: 5, approvedCount: 0, refusedCount: 0, withAgentCount: 1);
        Assert.Equal(SalesArchiveRules.PhaseReady, phase);
        Assert.True(SalesArchiveRules.CanSend(phase));
    }

    [Theory]
    [InlineData(SalesArchiveRules.PhaseInTransit, 3, 0, SalesArchiveRules.StageAtCommittee)]
    [InlineData(SalesArchiveRules.PhaseInTransit, 3, 2, SalesArchiveRules.StageAtArchive)]
    [InlineData(SalesArchiveRules.PhaseNeedsFix, 1, 0, SalesArchiveRules.StageAtCommittee)]
    [InlineData(SalesArchiveRules.PhaseNeedsFix, 0, 0, SalesArchiveRules.PhaseNeedsFix)]
    [InlineData(SalesArchiveRules.PhaseReady, 0, 0, SalesArchiveRules.PhaseReady)]
    [InlineData(SalesArchiveRules.PhaseApproved, 0, 0, SalesArchiveRules.PhaseApproved)]
    public void Stage_ShowsWhoMustActNext(string phase, int atCommittee, int toArchive, string expected)
    {
        Assert.Equal(expected, SalesArchiveRules.Stage(phase, atCommittee, toArchive));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(" ", null)]
    [InlineData("a", null)]
    [InlineData("  علي  ", "علي")]
    public void CleanPersonName_TrimsAndRejectsBlank(string? input, string? expected)
    {
        Assert.Equal(expected, SalesArchiveRules.CleanPersonName(input));
    }
}
