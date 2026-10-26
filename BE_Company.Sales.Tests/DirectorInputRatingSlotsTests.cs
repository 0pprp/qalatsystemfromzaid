using BE_Company.Sales.Services;
using Xunit;

namespace BE_Company.Sales.Tests;

/// <summary>ActivateInputRatingSlots_V1</summary>
public sealed class DirectorInputRatingSlotsTests
{
    [Fact]
    public void PickLatest_PrefersSameDayOverOlder()
    {
        var asOf = new DateTime(2026, 10, 25);
        var rows = new[]
        {
            new DirectorInputRatingRow(7, "قديم", asOf.AddDays(-2), "أ"),
            new DirectorInputRatingRow(9, "اليوم", asOf, "ب"),
            new DirectorInputRatingRow(5, "أمس", asOf.AddDays(-1), "ج"),
        };

        var pick = DirectorInputRatingSlots.PickLatest(rows, asOf);

        Assert.NotNull(pick);
        Assert.Equal(9, pick!.Score);
        Assert.Equal("اليوم", pick.Note);
        Assert.Equal("ب", pick.RatedByUserName);
    }

    [Fact]
    public void PickLatest_Empty_ReturnsNull()
    {
        Assert.Null(DirectorInputRatingSlots.PickLatest([], DateTime.UtcNow));
    }

    [Fact]
    public void BuildLast30Days_MapsScoresOntoHistoryDays()
    {
        var days = new[]
        {
            new DateTime(2026, 10, 24),
            new DateTime(2026, 10, 23),
            new DateTime(2026, 10, 22),
        };
        var rows = new[]
        {
            new DirectorInputRatingRow(8, "أ", days[0], null),
            new DirectorInputRatingRow(6, "ب", days[2], null),
        };

        var series = DirectorInputRatingSlots.BuildLast30Days(rows, days);

        Assert.Equal(3, series.Count);
        Assert.Equal(8, series[0].BoardOperatorScore);
        Assert.Null(series[1].BoardOperatorScore);
        Assert.Equal(6, series[2].BoardOperatorScore);
    }
}
