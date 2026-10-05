namespace BE_Company.Sales.Rating;

public sealed record ListRatingResult(
    int CustomerCount,
    int ExcellentCount,
    int GoodCount,
    int WeakCount,
    int RejectedCount,
    int LegalCount,
    int BadCount,
    int TotalPoints,
    double AverageScore,
    double LegalPercentage,
    ListRiskIndicator RiskIndicator,
    CustomerRatingLevel FinalRatingLevel,
    string FinalRatingLabel);

public static class ListRatingAggregator
{
    public static readonly DateTime RecentCutoffDate = new(2025, 9, 1);

    public static ListRatingResult Aggregate(IEnumerable<CustomerRatingResult> ratings)
    {
        var list = ratings as IList<CustomerRatingResult> ?? ratings.ToList();
        var count = list.Count;
        if (count == 0)
        {
            var emptyClass = ClassifyAverage(0);
            return new ListRatingResult(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, ListRiskIndicator.None,
                emptyClass.Level, emptyClass.Label);
        }

        var excellent = 0;
        var good = 0;
        var weak = 0;
        var rejected = 0;
        var bad = 0;
        var totalPoints = 0;
        // Single pass — production path for large cohorts (100k+).
        for (var i = 0; i < list.Count; i++)
        {
            var r = list[i];
            totalPoints += r.Score;
            switch (r.Level)
            {
                case CustomerRatingLevel.Excellent: excellent++; break;
                case CustomerRatingLevel.Good: good++; break;
                case CustomerRatingLevel.Weak: weak++; break;
                case CustomerRatingLevel.Rejected: rejected++; break;
                case CustomerRatingLevel.Bad: bad++; break;
                // Legal is never assigned by the calculator; ignore if present in legacy data.
            }
        }

        var average = (double)totalPoints / count;
        var badPct = bad * 100.0 / count;
        var final = ClassifyAverage(average);
        return new ListRatingResult(
            count, excellent, good, weak, rejected,
            LegalCount: 0,
            bad,
            totalPoints, average,
            LegalPercentage: 0,
            ClassifyRisk(badPct),
            final.Level, final.Label);
    }

    public static ListRatingResult AggregateRecent(IEnumerable<CustomerRatingResult> ratings) =>
        Aggregate(ratings.Where(r => r.DateSaleDevice is DateTime d && d.Date >= RecentCutoffDate));

    public static (CustomerRatingLevel Level, string Label) ClassifyAverage(double averageScore)
    {
        if (averageScore >= 7) return (CustomerRatingLevel.Excellent, CustomerRatingLabels.Excellent);
        if (averageScore >= 3) return (CustomerRatingLevel.Good, CustomerRatingLabels.Good);
        if (averageScore >= 0) return (CustomerRatingLevel.Weak, CustomerRatingLabels.Weak);
        if (averageScore > -7.5) return (CustomerRatingLevel.Rejected, CustomerRatingLabels.Rejected);
        return (CustomerRatingLevel.Bad, CustomerRatingLabels.Bad);
    }

    /// <summary>Risk from سيء share (Legal no longer participates in rating).</summary>
    public static ListRiskIndicator ClassifyRisk(double badPercentage)
    {
        if (badPercentage <= 0) return ListRiskIndicator.None;
        if (badPercentage < 5) return ListRiskIndicator.Low;
        if (badPercentage < 15) return ListRiskIndicator.Medium;
        return ListRiskIndicator.High;
    }
}
