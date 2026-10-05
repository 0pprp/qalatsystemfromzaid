namespace BE_Company.Sales.Rating;

/// <summary>
/// Shared active-customer rating cohort for headline buckets and name lists.
/// Must stay identical so ExcellentCount (etc.) never diverge from drill-down rows.
/// </summary>
public static class ActiveRatingCohort
{
    public static IReadOnlyList<(CustomerRatingFacts Fact, CustomerRatingResult Rated)> Build(
        IReadOnlyList<CustomerRatingFacts> openFacts,
        DateTime asOf,
        bool usePhantomSaleDate = false)
    {
        var active = openFacts
            .Where(f => ActiveCustomerRules.IsActive(f.LastPaymentDate, asOf)
                        && ListFollowerRatingCohort.HasOpenBalance(f))
            .ToList();
        if (active.Count == 0)
        {
            return Array.Empty<(CustomerRatingFacts, CustomerRatingResult)>();
        }

        var rated = CustomerRatingCalculator.EvaluateMany(active, asOf.Date, usePhantomSaleDate: usePhantomSaleDate);
        var matched = new List<(CustomerRatingFacts Fact, CustomerRatingResult Rated)>(active.Count);
        for (var i = 0; i < active.Count; i++)
        {
            // Defense in depth: settled / zero-balance never enter headline or list.
            if (rated[i].IsSettled || !ListFollowerRatingCohort.HasOpenBalance(active[i]))
            {
                continue;
            }

            matched.Add((active[i], rated[i]));
        }

        return matched;
    }

    public static IReadOnlyList<CustomerRatingResult> RatedResults(
        IReadOnlyList<CustomerRatingFacts> openFacts,
        DateTime asOf,
        bool usePhantomSaleDate = false)
    {
        var built = Build(openFacts, asOf, usePhantomSaleDate);
        if (built.Count == 0)
        {
            return Array.Empty<CustomerRatingResult>();
        }

        var list = new CustomerRatingResult[built.Count];
        for (var i = 0; i < built.Count; i++)
        {
            list[i] = built[i].Rated;
        }

        return list;
    }
}
