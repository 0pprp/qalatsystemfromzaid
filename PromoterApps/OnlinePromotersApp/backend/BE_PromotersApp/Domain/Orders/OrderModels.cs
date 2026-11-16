using System.Text.RegularExpressions;

namespace BE_PromotersApp.Domain.Orders;

public static class OrderStatus
{
    public const string New = "New";
    public const string Transferred = "Transferred";
    public const string FieldInspection = "FieldInspection";
    public const string Sold = "Sold";
    public const string NotSold = "NotSold";
    public const string Cancelled = "Cancelled";

    public static readonly string[] All = [New, Transferred, FieldInspection, Sold, NotSold, Cancelled];
    public static readonly string[] Open = [New, Transferred, FieldInspection];

    public static string Label(string status) => status switch
    {
        New => "قيد الإرسال",
        Transferred => "في قسم المبيعات",
        FieldInspection => "قيد الكشف",
        Sold => "تم البيع",
        NotSold => "لم يتم البيع",
        Cancelled => "ملغى",
        _ => status,
    };

    public const string DefaultNotSoldReason = "لم يتم إتمام البيع. يمكنك التواصل مع الشركة لمزيد من التفاصيل.";

    /// <summary>Maps the company system's SalesRequests.Status to the app status (null: request not found, leave the order).</summary>
    public static string? FromBranch(bool found, string? branchStatus, bool isDeleted)
    {
        if (!found) return null;
        if (isDeleted) return NotSold;
        return branchStatus switch
        {
            "Completed" => Sold,
            "Rejected" => NotSold,
            "InProgress" or "Inspected" or "ConvertedToSale" or "LegalFollowup" => FieldInspection,
            _ => Transferred,
        };
    }
}

public sealed record CreateOrderInput(int? ProductId, int? Quantity, string? CustomerName, string? CustomerPhone, int? CityValue, string? Address, string? Notes);

public sealed record TimelineDto(string Status, string StatusLabel, DateTime At, string? Note);

public sealed record OrderSummaryDto(
    long Id, string OrderNumber, string Status, string StatusLabel, string ProductName, int Quantity, string CustomerName,
    string CityName, long MyCommission, DateTime CreatedAt, DateTime UpdatedAt, DateTime? SoldAt);

public sealed record OrderDetailDto(
    long Id, string OrderNumber, string Status, string StatusLabel, string ProductName, int Quantity, long UnitPrice, long TotalPrice,
    long MyCommission, string CustomerName, string CustomerPhone, int CityValue, string CityName, string Address, string? Notes,
    string? PublicReason, IReadOnlyList<TimelineDto> Timeline, DateTime CreatedAt, DateTime UpdatedAt, DateTime? SoldAt, bool CanCancel);

public sealed record AdminOrderRow(
    long Id, string OrderNumber, string Status, string StatusLabel, long UserId, string Username, string? ParentUsername,
    string ProductName, int Quantity, long UnitPrice, long CommissionTotal, long SellerCommission, long ParentCommission, decimal ParentCutPercent,
    string CustomerName, string CustomerPhone, string CityName, string Address, string? Notes, string? PublicReason,
    int? SalesRequestId, string? BranchStatus, DateTime? LastSyncAt, DateTime CreatedAt, DateTime? SoldAt);

public sealed record AdminOrderDetailDto(AdminOrderRow Order, IReadOnlyList<TimelineDto> Timeline);

/// <summary>Flat row of promoters.Orders.</summary>
public sealed class OrderRow
{
    public long Id { get; set; }
    public Guid OrderUuid { get; set; }
    public string OrderNumber { get; set; } = "";
    public long UserId { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public long UnitPrice { get; set; }
    public int Quantity { get; set; }
    public long CommissionTotal { get; set; }
    public long SellerCommission { get; set; }
    public long? ParentUserId { get; set; }
    public decimal ParentCutPercent { get; set; }
    public long ParentCommission { get; set; }
    public int CityValue { get; set; }
    public string CityName { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public string CustomerPhone { get; set; } = "";
    public string Address { get; set; } = "";
    public string? Notes { get; set; }
    public string Status { get; set; } = "";
    public string? PublicReason { get; set; }
    public int? SalesRequestId { get; set; }
    public int? SaleId { get; set; }
    public string? BranchStatus { get; set; }
    public string? BranchRejectReason { get; set; }
    public DateTime? LastSyncAt { get; set; }
    public DateTime? SoldAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>The customer's name as typed in one field: at least three Arabic names.</summary>
public static partial class CustomerName
{
    public const string NotArabic = "اكتب اسم الزبون بالحروف العربية فقط";
    public const string TooShort = "اكتب اسم الزبون الثلاثي (ثلاثة أسماء على الأقل، و«عبد الحسن» تُحسب اسماً واحداً)";

    private static readonly string[] Prefixes = ["عبد", "ابو", "أبو", "ابي", "أبي"];

    /// <summary>Returns the cleaned name or null when fewer than three Arabic names were typed.</summary>
    public static string? Clean(string? input) => Check(input).Name;

    /// <summary>
    /// The cleaned name, or the reason it was refused. Diacritics, tatweel and the invisible characters some keyboards
    /// insert (zero-width joiners, direction marks) are dropped instead of making the name "not Arabic".
    /// </summary>
    public static (string? Name, string? Error) Check(string? input)
    {
        var stripped = Invisible().Replace(input ?? "", "");
        var words = stripped.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return (null, TooShort);
        if (words.Any(w => !ArabicWord().IsMatch(w))) return (null, NotArabic);
        // «عبد الله» and «أبو بكر» count as one name.
        var names = 0;
        for (var i = 0; i < words.Length; i++)
        {
            if (Prefixes.Contains(words[i]) && i + 1 < words.Length) i++;
            names++;
        }
        var cleaned = string.Join(' ', words);
        if (names < 3) return (null, TooShort);
        return cleaned.Length <= 150 ? (cleaned, null) : (null, "الاسم طويل جداً");
    }

    [GeneratedRegex(@"^[ء-يٱ-ۓ]+$")]
    private static partial Regex ArabicWord();

    /// <summary>Harakat, superscript alef, tatweel, zero-width characters and direction marks.</summary>
    [GeneratedRegex("[\u064B-\u065F\u0670\u0640\u200B-\u200F\u202A-\u202E\u2066-\u2069\uFEFF]")]
    private static partial Regex Invisible();
}
