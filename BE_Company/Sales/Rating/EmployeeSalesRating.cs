namespace BE_Company.Sales.Rating;

public sealed record EmployeeSalesRatingSnapshot(
    int CustomerCount,
    double AverageScore,
    string RatingLabel,
    int ExcellentCount,
    int GoodCount,
    int WeakCount,
    int RejectedCount,
    int LegalCount,
    int BadCount);

/// <summary>
/// Sales-employee rating: period window uses DateSaleDevice in [from, to]
/// with the same level-point average as list/follower/branch.
/// </summary>
public static class EmployeeSalesRating
{
    public static EmployeeSalesRatingSnapshot Snapshot(
        IEnumerable<CustomerRatingFacts> facts,
        DateTime asOfDate,
        bool usePhantomSaleDate = false)
    {
        var list = facts as IReadOnlyList<CustomerRatingFacts> ?? facts.ToList();
        var rated = CustomerRatingCalculator.EvaluateMany(list, asOfDate, usePhantomSaleDate: usePhantomSaleDate);
        var agg = ListRatingAggregator.Aggregate(rated);
        return FromAggregate(agg);
    }

  /// <summary>
  /// Only customers whose sale date falls in [fromDate, toDate]; score as-of asOfDate
  /// (defaults to toDate for backward compatibility).
  /// </summary>
  public static EmployeeSalesRatingSnapshot PeriodSnapshot(
      IEnumerable<CustomerRatingFacts> facts,
      DateTime fromDate,
      DateTime toDate,
      DateTime? asOfDate = null)
  {
      var asOf = (asOfDate ?? toDate).Date;
      var agg = ListRatingTrendCalculator.PeriodAggregate(facts, fromDate, toDate, asOf);
      return FromAggregate(agg);
  }

    private static EmployeeSalesRatingSnapshot FromAggregate(ListRatingResult agg) =>
        new(
            agg.CustomerCount,
            Math.Round(agg.AverageScore, 2, MidpointRounding.AwayFromZero),
            agg.FinalRatingLabel,
            agg.ExcellentCount,
            agg.GoodCount,
            agg.WeakCount,
            agg.RejectedCount,
            agg.LegalCount,
            agg.BadCount);
}
