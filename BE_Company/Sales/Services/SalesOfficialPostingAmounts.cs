namespace BE_Company.Sales.Services;

/// <summary>
/// Maps SalesDraft final/override prices onto CustomersSales discount columns.
/// Official views compute:
/// AmountTotalSalesDenar = itemSum - DiscountAmountTotal * 1448
/// AmountDaySalesDenar   = daySum  - DiscountAmountTotalDay * 1448
/// Classic UI sends dinar discounts then divides by 1448 before the SP.
/// </summary>
public static class SalesOfficialPostingAmounts
{
    public const decimal DinarPerUnit = 1448m;

    public static (double DiscountTotal, double DiscountDay) ToStoredDiscounts(
        decimal catalogTotalDenar,
        decimal catalogDailyDenar,
        decimal finalTotalDenar,
        decimal finalDailyDenar)
    {
        var catalogTotal = Math.Max(0m, catalogTotalDenar);
        var catalogDaily = Math.Max(0m, catalogDailyDenar);
        var finalTotal = Math.Max(0m, finalTotalDenar);
        var finalDaily = Math.Max(0m, finalDailyDenar);

        var discountTotal = (catalogTotal - finalTotal) / DinarPerUnit;
        var discountDay = (catalogDaily - finalDaily) / DinarPerUnit;
        return ((double)discountTotal, (double)discountDay);
    }

    public static string? ResolveShopName(string? shopProfileName, string? mukhtarNameFallback = null)
    {
        if (!string.IsNullOrWhiteSpace(shopProfileName))
        {
            return shopProfileName.Trim();
        }

        // Never fall back to mukhtar — that produced empty/"لا يوجد" shop names on posted sales.
        _ = mukhtarNameFallback;
        return null;
    }
}
