namespace BE_CustomerApp.Domain.Orders;

public static class OrderStatus
{
    public const string New = "New";
    public const string UnderReview = "UnderReview";
    public const string Denied = "Denied";
    public const string Transferred = "Transferred";
    public const string FieldInspection = "FieldInspection";
    public const string Sold = "Sold";
    public const string NotSold = "NotSold";
    public const string Cancelled = "Cancelled";
    /// <summary>The sale is fully paid in the company system (cash sales right after the sale, installments at the last payment).</summary>
    public const string Finished = "Finished";

    public static readonly string[] All = [New, UnderReview, Denied, Transferred, FieldInspection, Sold, Finished, NotSold, Cancelled];
    public static readonly string[] Open = [New, UnderReview, Transferred, FieldInspection];
    public static readonly string[] AwaitingBranch = [Transferred, FieldInspection];

    public static string Label(string status) => status switch
    {
        New => "طلب جديد",
        UnderReview => "قيد المعالجة",
        Denied => "مرفوض",
        Transferred => "في قسم المبيعات",
        FieldInspection => "قيد الكشف الميداني",
        Sold => "تم البيع - قيد التسديد",
        Finished => "مكتمل التسديد",
        NotSold => "لم يتم البيع",
        Cancelled => "ملغى",
        _ => status,
    };

    public const string DefaultNotSoldReason = "لم يتم إتمام البيع بعد الكشف الميداني. يمكنك التواصل مع الشركة لمزيد من التفاصيل.";

    /// <summary>
    /// Maps the company system's SalesRequests.Status (BE_Company SalesManagerContracts) to the app status.
    /// Returns null when the branch row was not found (leave the order unchanged).
    /// </summary>
    public static string? FromBranch(bool found, string? branchStatus, bool isDeleted)
    {
        if (!found) return null;
        if (isDeleted) return NotSold;
        return branchStatus switch
        {
            "Completed" => Sold,
            "Rejected" => NotSold,
            "InProgress" or "Inspected" or "ConvertedToSale" or "LegalFollowup" => FieldInspection,
            _ => Transferred, // New, Assigned, Viewed, Returned, Pending, PreparedForSale, and anything new
        };
    }
}

public static class PurchaseTypes
{
    public const string Cash = "Cash";
    public const string DailyInstallments = "DailyInstallments";

    public static string Label(string t) => t == DailyInstallments ? "أقساط يومية" : "نقد";
}
