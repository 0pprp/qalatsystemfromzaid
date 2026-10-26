namespace BE_Company.Sales.Services;

/// <summary>
/// Helpers for board cards: follower / supervision / sales input ratings.
/// Deploy marker: ActivateInputRatingSlots_V1
/// </summary>
public static class DirectorInputRatingSlots
{
    public const string DeployMarker = "ActivateInputRatingSlots_V1";
    /// <summary>Board sales card reads only SalesEmployeeListDayRating — no ShopRating fallback.</summary>
    public const string SalesCardOnlyDayRatingMarker = "SalesDelegateCard_OnlyListDayRating_V2";

    public const string ActorFollower = "Follower";
    public const string ActorSupervision = "Supervision";
    public const string ActorSales = "Sales";

    public static DirectorDelegateFollowupBoardOperatorRatingDto? PickLatest(
        IEnumerable<DirectorInputRatingRow> rows,
        DateTime asOf)
    {
        var list = rows
            .Where(r => r.Score is >= 0)
            .OrderByDescending(r => r.RatingDate.Date)
            .ThenByDescending(r => r.Score)
            .ToList();
        if (list.Count == 0)
            return null;

        var todayIdx = list.FindIndex(r => r.RatingDate.Date == asOf.Date);
        var today = todayIdx >= 0 ? list[todayIdx] : list[0];
        return new DirectorDelegateFollowupBoardOperatorRatingDto
        {
            ListId = 0,
            RatingDate = today.RatingDate.Date,
            Score = today.Score,
            Note = today.Note,
            RatedByUserName = today.RatedBy
        };
    }

    public static IReadOnlyList<DirectorDelegateFollowupDayRatingDto> BuildLast30Days(
        IEnumerable<DirectorInputRatingRow> rows,
        IReadOnlyList<DateTime> historyDays)
    {
        var byDay = rows
            .GroupBy(r => r.RatingDate.Date)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.Score).First());

        return historyDays.Select(d =>
        {
            if (!byDay.TryGetValue(d.Date, out var row))
            {
                return new DirectorDelegateFollowupDayRatingDto
                {
                    Date = d.Date,
                    Score = 0,
                    Label = "",
                    BoardOperatorScore = null,
                    BoardOperatorNote = null
                };
            }

            return new DirectorDelegateFollowupDayRatingDto
            {
                Date = d.Date,
                Score = row.Score,
                Label = $"{row.Score}/10",
                BoardOperatorScore = row.Score,
                BoardOperatorNote = row.Note
            };
        }).ToList();
    }
}

public readonly record struct DirectorInputRatingRow(
    int Score,
    string? Note,
    DateTime RatingDate,
    string? RatedBy);
