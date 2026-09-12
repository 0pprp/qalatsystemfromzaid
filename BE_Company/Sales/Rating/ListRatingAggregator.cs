namespace BE_Company.Sales.Rating;

public sealed record ListRatingResult(
    int CustomerCount,
    int ExcellentCount,
    int GoodCount,
    int WeakCount,
    int RejectedCount,
    int LegalCount,
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
            return new ListRatingResult(0, 0, 0, 0, 0, 0, 0, 0, 0, ListRiskIndicator.None,
                emptyClass.Level, emptyClass.Label);
        }

        var excellent = list.Count(r => r.Level == CustomerRatingLevel.Excellent);
        var good = list.Count(r => r.Level == CustomerRatingLevel.Good);
        var weak = list.Count(r => r.Level == CustomerRatingLevel.Weak);
        var rejected = list.Count(r => r.Level == CustomerRatingLevel.Rejected);
        var legal = list.Count(r => r.Level == CustomerRatingLevel.Legal);
        var totalPoints = list.Sum(r => r.Score);
        var average = (double)totalPoints / count;
        var legalPct = legal * 100.0 / count;
        var final = ClassifyAverage(average);
        return new ListRatingResult(
            count, excellent, good, weak, rejected, legal,
            totalPoints, average, legalPct, ClassifyRisk(legalPct),
            final.Level, final.Label);
    }

    public static ListRatingResult AggregateRecent(IEnumerable<CustomerRatingResult> ratings) =>
        Aggregate(ratings.Where(r => r.DateSaleDevice is DateTime d && d.Date >= RecentCutoffDate));

    public static (CustomerRatingLevel Level, string Label) ClassifyAverage(double averageScore)
    {
        if (averageScore >= 7) return (CustomerRatingLevel.Excellent, CustomerRatingLabels.Excellent);
        if (averageScore >= 3) return (CustomerRatingLevel.Good, CustomerRatingLabels.Good);
        if (averageScore >= 0) return (CustomerRatingLevel.Weak, CustomerRatingLabels.Weak);
        return (CustomerRatingLevel.Rejected, CustomerRatingLabels.Rejected);
    }

    public static ListRiskIndicator ClassifyRisk(double legalPercentage)
    {
        if (legalPercentage <= 0) return ListRiskIndicator.None;
        if (legalPercentage < 5) return ListRiskIndicator.Low;
        if (legalPercentage < 15) return ListRiskIndicator.Medium;
        return ListRiskIndicator.High;
    }
}
