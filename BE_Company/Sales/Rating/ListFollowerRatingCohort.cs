namespace BE_Company.Sales.Rating;

/// <summary>
/// Rating cohort for lists / followers / branch / BM customer drill-down:
/// exclude fully paid (مصفرون) and legal (قانونية — outside the five rating levels).
/// </summary>
public static class ListFollowerRatingCohort
{
    /// <summary>Open-balance, non-legal only — AmountRemaining &gt; 0 and !IsLegal.</summary>
    public static IReadOnlyList<CustomerRatingFacts> OpenBalanceOnly(
        IEnumerable<CustomerRatingFacts> facts) =>
        facts.Where(HasOpenBalance).ToList();

    public static bool HasOpenBalance(CustomerRatingFacts fact) =>
        fact.AmountTotalSales > 0
        && fact.AmountRemaining > 0
        && !fact.IsLegal;
}
