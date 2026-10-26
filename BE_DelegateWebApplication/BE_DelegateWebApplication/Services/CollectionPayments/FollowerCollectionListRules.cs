namespace BE_DelegateWebApplication.Services.CollectionPayments;

/// <summary>
/// Follower جباية customer list inclusion — same rule as dbo.GetCustomersDelegateAll:
/// open balance OR payment on current Baghdad business day (03:00 roll).
/// Deploy marker: FollowerCollectionSameDayZeroed_V1
/// </summary>
public static class FollowerCollectionListRules
{
    public const string DeployMarker = "FollowerCollectionSameDayZeroed_V2";

    /// <summary>
    /// SQL predicate (AND …) keeping same-day zeroed customers until next business day.
    /// Uses @IraqBizDate (date) and alias vc for View_Customers, c for Customers.
    /// </summary>
    public static readonly string SqlIncludeCustomerPredicate = @"
(
    ISNULL(vc.AmountRemaining, 0) > 0
    OR EXISTS (
      SELECT 1
      FROM dbo.CustomersPayments cp
      WHERE cp.CustomerID = c.CustomerID
        AND CASE
              WHEN DATEPART(HOUR, DATEADD(HOUR, 3, cp.PaymentDate)) < 3
                THEN DATEADD(DAY, -1, CONVERT(date, DATEADD(HOUR, 3, cp.PaymentDate)))
              ELSE CONVERT(date, DATEADD(HOUR, 3, cp.PaymentDate))
            END = @IraqBizDate
    )
)";

    public static bool IncludeInCollectionList(double amountRemaining, bool paidOnCurrentBusinessDay) =>
        amountRemaining > 0 || paidOnCurrentBusinessDay;
}
