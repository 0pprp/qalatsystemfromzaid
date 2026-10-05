namespace BE_Company.Sales.Rating;

/// <summary>
/// Builds daily list-rating series for names sold inside [saleFrom, saleTo].
/// Score = sum(level points) / customer count
/// (ممتاز 10، جيد 5، ضعيف 0، متلكئ -5، سيء -10). IsLegal لا يؤثر على التقييم.
/// Follower/branch = pooled customers across lists (not mean of list averages).
/// Evaluation as-of can run past the sale window (e.g. August sales scored through today).
/// </summary>
public static class ListRatingTrendCalculator
{
    /// <summary>
    /// Customers whose DateSaleDevice falls in [fromDate, toDate], and on/before asOfDate
    /// (so early chart days do not include later sales in the window).
    /// When asOfDate &gt; toDate, the full sale window is included and levels keep updating.
    /// </summary>
    public static IEnumerable<CustomerRatingFacts> FactsSoldInPeriod(
        IEnumerable<CustomerRatingFacts> facts,
        DateTime fromDate,
        DateTime toDate,
        DateTime asOfDate)
    {
        var from = fromDate.Date;
        var to = toDate.Date;
        var asOf = asOfDate.Date;
        if (asOf < from)
        {
            return [];
        }

        var end = asOf <= to ? asOf : to;
        return facts.Where(f =>
        {
            if (f.DateSaleDevice is null)
            {
                return false;
            }

            var sale = f.DateSaleDevice.Value.Date;
            return sale >= from && sale <= end;
        });
    }

    /// <summary>
    /// Aggregate level buckets + average for sales with DateSaleDevice in [fromDate, toDate]
    /// (progressive until asOf reaches toDate), evaluated as of asOfDate.
    /// </summary>
    public static ListRatingResult PeriodAggregate(
        IEnumerable<CustomerRatingFacts> facts,
        DateTime fromDate,
        DateTime toDate,
        DateTime asOfDate,
        bool usePhantomSaleDate = false)
    {
        var inPeriod = FactsSoldInPeriod(facts, fromDate, toDate, asOfDate).ToList();
        var rated = CustomerRatingCalculator.EvaluateMany(inPeriod, asOfDate, usePhantomSaleDate: usePhantomSaleDate);
        return ListRatingAggregator.Aggregate(rated);
    }

    public static double ListAverageScore(
        IEnumerable<CustomerRatingFacts> listFacts,
        DateTime asOfDate)
    {
        var list = listFacts as IReadOnlyList<CustomerRatingFacts> ?? listFacts.ToList();
        var rated = CustomerRatingCalculator.EvaluateMany(list, asOfDate);
        return ListRatingAggregator.Aggregate(rated).AverageScore;
    }

    /// <summary>
    /// For each calendar day in [fromDate, toDate], pooled average across all lists'
    /// customers sold within the period up to that day.
    /// </summary>
    public static IReadOnlyDictionary<DateTime, double> DailyAverageAcrossLists(
        IReadOnlyList<int> listIds,
        IReadOnlyDictionary<int, IReadOnlyList<CustomerRatingFacts>> factsByList,
        DateTime fromDate,
        DateTime toDate) =>
        DailyAverageAcrossLists(listIds, factsByList, fromDate, toDate, toDate);

    /// <summary>
    /// Sale cohort = DateSale in [saleFromDate, saleToDate].
    /// Chart days run through asOfEndDate so payments after the sale window keep updating.
    /// </summary>
    public static IReadOnlyDictionary<DateTime, double> DailyAverageAcrossLists(
        IReadOnlyList<int> listIds,
        IReadOnlyDictionary<int, IReadOnlyList<CustomerRatingFacts>> factsByList,
        DateTime saleFromDate,
        DateTime saleToDate,
        DateTime asOfEndDate)
    {
        var pooled = new List<CustomerRatingFacts>();
        foreach (var listId in listIds)
        {
            if (factsByList.TryGetValue(listId, out var facts) && facts.Count > 0)
            {
                pooled.AddRange(facts);
            }
        }

        return DailyListScores(pooled, saleFromDate, saleToDate, asOfEndDate);
    }

    public static IReadOnlyDictionary<DateTime, double> DailyAverageAcrossLists(
        IReadOnlyList<int> listIds,
        IReadOnlyDictionary<int, IReadOnlyList<CustomerRatingFacts>> factsByList,
        DateTime saleFromDate,
        DateTime saleToDate,
        DateTime asOfEndDate,
        DateTime chartFromDate)
    {
        var pooled = new List<CustomerRatingFacts>();
        foreach (var listId in listIds)
        {
            if (factsByList.TryGetValue(listId, out var facts) && facts.Count > 0)
            {
                pooled.AddRange(facts);
            }
        }

        return DailyListScores(pooled, saleFromDate, saleToDate, asOfEndDate, chartFromDate);
    }

    public static IReadOnlyDictionary<DateTime, double> DailyListScores(
        IReadOnlyList<CustomerRatingFacts> facts,
        DateTime fromDate,
        DateTime toDate) =>
        DailyListScores(facts, fromDate, toDate, toDate, fromDate);

    /// <summary>
    /// Daily scores for names sold in [saleFromDate, saleToDate], evaluated each day
    /// from chartFromDate through asOfEndDate (typically today). After the sale window
    /// ends, the cohort stays fixed and payment-based levels continue to move.
    /// </summary>
    public static IReadOnlyDictionary<DateTime, double> DailyListScores(
        IReadOnlyList<CustomerRatingFacts> facts,
        DateTime saleFromDate,
        DateTime saleToDate,
        DateTime asOfEndDate,
        DateTime? chartFromDate = null)
    {
        saleFromDate = saleFromDate.Date;
        saleToDate = saleToDate.Date;
        asOfEndDate = asOfEndDate.Date;
        var chartFrom = (chartFromDate ?? saleFromDate).Date;
        if (asOfEndDate < saleFromDate)
        {
            asOfEndDate = saleFromDate;
        }

        if (chartFrom < saleFromDate)
        {
            chartFrom = saleFromDate;
        }

        if (chartFrom > asOfEndDate)
        {
            chartFrom = asOfEndDate;
        }

        var result = new Dictionary<DateTime, double>();
        for (var d = chartFrom; d <= asOfEndDate; d = d.AddDays(1))
        {
            var agg = PeriodAggregate(facts, saleFromDate, saleToDate, d);
            result[d] = Math.Round(agg.AverageScore, 2, MidpointRounding.AwayFromZero);
        }

        return result;
    }
}
