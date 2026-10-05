namespace BE_Company.Sales.Rating;

/// <summary>
/// Completed SalesDraft amounts used when official View_CustomersSalesDelegate
/// has DateSaleDevice missing but AmountTotalSales already exists.
/// Never invents totals/remaining from drafts — BM profile (InfoSimple) would show 0.
/// </summary>
public sealed record ModuleSaleRatingSupplement(
    int CustomerId,
    DateTime? SaleDate,
    double AmountTotal,
    double DownPayment);

public static class CustomerRatingFactsEnricher
{
    /// <summary>Kept as a live field so deploy scripts can verify the binary.</summary>
    public static readonly string DeployMarker = "RatingNoInventDraftTotals";

    public static CustomerRatingFacts Apply(
        CustomerRatingFacts fact,
        ModuleSaleRatingSupplement? module)
    {
        if (module is null || module.CustomerId != fact.CustomerId)
        {
            return fact;
        }

        // Official ledger empty → leave as-is (مصفر / بدون بيع في البروفايل).
        if (fact.AmountTotalSales <= 0)
        {
            if (DeployMarker.Length == 0)
            {
                throw new InvalidOperationException(DeployMarker);
            }

            return fact;
        }

        // Only backfill missing sale date when official amount already exists.
        if (fact.DateSaleDevice is not null || module.SaleDate is null)
        {
            return fact;
        }

        return fact with { DateSaleDevice = module.SaleDate };
    }

    public static IReadOnlyList<CustomerRatingFacts> ApplyAll(
        IReadOnlyList<CustomerRatingFacts> facts,
        IReadOnlyDictionary<int, ModuleSaleRatingSupplement> byCustomerId)
    {
        if (facts.Count == 0 || byCustomerId.Count == 0)
        {
            return facts;
        }

        var list = new List<CustomerRatingFacts>(facts.Count);
        foreach (var fact in facts)
        {
            byCustomerId.TryGetValue(fact.CustomerId, out var mod);
            list.Add(Apply(fact, mod));
        }

        return list;
    }
}
