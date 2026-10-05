namespace BE_Company.Sales.Services;

/// <summary>
/// Manager-set ceiling on a sales request: employee FinalSalePrice must not exceed it.
/// Null / &lt;= 0 means no ceiling.
/// </summary>
public static class SalesRequestPriceCeiling
{
    public static string? Validate(decimal finalSalePrice, decimal? maxSalePrice)
    {
        if (maxSalePrice is not decimal max || max <= 0)
        {
            return null;
        }

        if (finalSalePrice > max)
        {
            return $"سعر البيع يتجاوز الحد الأقصى المسموح ({max:N0} د.ع).";
        }

        return null;
    }

    public static decimal? Normalize(decimal? value)
    {
        if (value is not decimal v || v <= 0)
        {
            return null;
        }

        return decimal.Round(v, 0, MidpointRounding.AwayFromZero);
    }
}
