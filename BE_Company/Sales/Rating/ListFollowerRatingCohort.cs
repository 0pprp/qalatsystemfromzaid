namespace BE_Company.Sales.Rating;

/// <summary>
/// Rating cohort for lists / followers / branch / BM customer drill-down:
/// exclude fully paid customers (الباقي ≤ 0 / مصفرون).
/// </summary>
public static class ListFollowerRatingCohort
{
    /// <summary>Open-balance only — AmountRemaining must be &gt; 0.</summary>
    public static IReadOnlyList<CustomerRatingFacts> OpenBalanceOnly(
        IEnumerable<CustomerRatingFacts> facts) =>
        facts.Where(HasOpenBalance).ToList();

    public static bool HasOpenBalance(CustomerRatingFacts fact) =>
        fact.AmountTotalSales > 0 && fact.AmountRemaining > 0;
}
