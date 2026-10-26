namespace BE_Company.Sales.Services;

/// <summary>
/// Legal (IsLegal) customers: الواصل/الباقي are shown again (masking disabled).
/// Helpers kept for call-site stability.
/// </summary>
public static class LegalCustomerFinancialVisibility
{
    public const string HiddenDisplay = "—";

    /// <summary>Masking disabled — always show paid/remaining.</summary>
    public static bool ShouldHidePaidAndRemaining(bool isLegal) => false;

    public static void MaskRatingCustomerCard(EmployeeRatingCustomerCardDto card, bool isLegal)
    {
        if (card is null)
        {
            return;
        }

        // No longer zero out AmountPaid / AmountRemaining for legal customers.
        if (isLegal)
        {
            card.IsLegal = true;
        }

        card.HidePaidRemaining = false;
    }

    /// <summary>
    /// SQL predicate fragment: keep only non-legal customers (alias must expose CustomerID).
    /// </summary>
    public static string NonLegalCustomerPredicate(string customerAlias = "c") =>
        $"ISNULL({customerAlias}.IsLegal, 0) = 0";
}
