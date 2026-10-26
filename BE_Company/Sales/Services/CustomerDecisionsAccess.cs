using BE_Company.Sales.Authorization;

namespace BE_Company.Sales.Services;

/// <summary>Role gates for CustomerDecisions (legal conversion + waiver accountant queues).</summary>
public static class CustomerDecisionsAccess
{
    /// <summary>Legal conversion approve/reject — monitor only (via branch JWT or gateway actor-role).</summary>
    public static bool CanReviewLegalConversion(string? userType) =>
        string.Equals(userType, "المراقب", StringComparison.Ordinal);

    /// <summary>Waiver discount apply — main accountant only.</summary>
    public static bool CanApproveWaiver(string? userType) =>
        string.Equals(userType, "محاسب رئيسي", StringComparison.Ordinal);

    public static bool CanDecide(string? userType) =>
        string.Equals(userType, "مدير فرع", StringComparison.Ordinal);

    public static bool HasRequiredReviewNote(string? note) =>
        !string.IsNullOrWhiteSpace(note);

    /// <summary>
    /// List conversion/waiver queues: accountant, branch manager, or trusted sales-gateway proxy
    /// (monitor/director central read via SendManagerAsync → UserType مدير مبيعات|المراقب + AuthSource Gateway).
    /// </summary>
    public static bool CanListAccountantQueues(string? userType, string? authSource)
    {
        if (string.Equals(userType, "محاسب رئيسي", StringComparison.Ordinal)
            || string.Equals(userType, "مدير فرع", StringComparison.Ordinal))
        {
            return true;
        }

        return string.Equals(authSource, SalesGatewayKeyHandler.AuthSourceGateway, StringComparison.Ordinal);
    }
}
