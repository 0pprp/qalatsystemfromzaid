namespace BE_Company.Sales.Rating;

/// <summary>
/// Virtual sale-date adjustment for «التقييم الوهمي» math only.
/// Does not mutate ledger DateSaleDevice or period filters.
/// </summary>
public static class PhantomSaleDate
{
    public static readonly DateTime FloorDate = new(2026, 5, 1);
    public static readonly DateTime BoostEndInclusive = new(2026, 8, 31);
    public static readonly DateTime UnchangedFrom = new(2026, 9, 1);
    public const int BoostDays = 30;

    public const string MetricLabel = "التقييم الوهمي";
    public const string RealMetricLabel = "التقييم الحقيقي";

    /// <summary>
    /// Before 2026-05-01 → 2026-05-01;
    /// 2026-05-01..2026-08-31 → +30 days;
    /// from 2026-09-01 → unchanged.
    /// </summary>
    public static DateTime? Adjust(DateTime? dateSaleDevice)
    {
        if (dateSaleDevice is not DateTime raw)
        {
            return null;
        }

        var d = raw.Date;
        if (d < FloorDate)
        {
            return FloorDate;
        }

        if (d <= BoostEndInclusive)
        {
            return d.AddDays(BoostDays);
        }

        return d;
    }
}
