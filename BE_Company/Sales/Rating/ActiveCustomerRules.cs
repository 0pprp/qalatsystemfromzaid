namespace BE_Company.Sales.Rating;

/// <summary>
/// Rating cohort for list/follower/branch: open accounts with a payment in the last 30 days
/// ("تسديد يومي"). Customers with no payment for a full month are excluded from agent rating.
/// Independent of the sale-date period picker and of the WeakWeek 3.8% rule.
/// </summary>
public static class ActiveCustomerRules
{
    public const int MaxDaysWithoutPayment = 30;

    public static bool IsActive(DateTime? lastPaymentDate, DateTime asOf)
    {
        if (lastPaymentDate is not DateTime paid)
        {
            return false;
        }

        var cutoff = asOf.Date.AddDays(-MaxDaysWithoutPayment);
        return paid.Date >= cutoff;
    }
}
