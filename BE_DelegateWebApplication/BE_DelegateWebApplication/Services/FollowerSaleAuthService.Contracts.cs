namespace BE_DelegateWebApplication.Services;

public static class FollowerSaleAuthRules
{
    public const int MinRating = 1;
    public const int MaxRating = 10;
    public const int OptionalNoteMinRating = 7;

    /// <summary>Ratings ≤6 require a note and become «مبيعات خاطئة» on new-sale intake.</summary>
    public const int WrongSaleMaxRating = 6;

    public static bool IsValidRating(int rating) =>
        rating >= MinRating && rating <= MaxRating;

    /// <summary>≥7 note optional; &lt;7 note required (non-whitespace).</summary>
    public static bool IsValidNoteForRating(int rating, string? note)
    {
        if (!IsValidRating(rating)) return false;
        if (rating >= OptionalNoteMinRating) return true;
        return !string.IsNullOrWhiteSpace(note);
    }

    public static bool IsWrongSaleRating(int rating) =>
        IsValidRating(rating) && rating <= WrongSaleMaxRating;

    public static bool IsValidShopMoney(decimal value) => value > 0;

    public static string? NormalizeNote(string? note)
    {
        var t = (note ?? string.Empty).Trim();
        return t.Length == 0 ? null : t;
    }

    /// <summary>Build Arabic reason block for WrongSale exception / request notes.</summary>
    /// <param name="evaluatorRole">
    /// «متابع» أو «مندوب الجباية» — يظهر في نص الإشعار حتى يختار المدير المفوض الكونتينر الصحيح.
    /// </param>
    public static string FormatShopEvaluationBlock(
        int rating,
        string? note,
        decimal shopStockEstimatedValue,
        decimal estimatedDailyRevenue,
        string? customerName = null,
        string? customerPhone = null,
        decimal? saleAmount = null,
        string? evaluatorRole = null)
    {
        var lines = new List<string>
        {
            "تقييم المتجر / مبيعات خاطئة",
        };
        if (!string.IsNullOrWhiteSpace(evaluatorRole))
        {
            lines.Add($"المصدر: {evaluatorRole.Trim()}");
        }

        lines.Add($"التقييم: {rating}");
        if (!string.IsNullOrWhiteSpace(note))
            lines.Add($"الملاحظة: {note.Trim()}");
        lines.Add($"قيمة بضاعة المحل: {shopStockEstimatedValue:N0}");
        lines.Add($"واردات المحل اليومية: {estimatedDailyRevenue:N0}");
        if (!string.IsNullOrWhiteSpace(customerName))
            lines.Add($"اسم الزبون: {customerName.Trim()}");
        if (!string.IsNullOrWhiteSpace(customerPhone))
            lines.Add($"الهاتف: {customerPhone.Trim()}");
        if (saleAmount is > 0)
            lines.Add($"سعر البيع: {saleAmount.Value:N0}");
        return string.Join("\n", lines);
    }
}

public static class FollowerListStatsRules
{
    public static int ToYearMonth(DateTime date) => date.Year * 100 + date.Month;

    public static int PreviousYearMonth(int yearMonth)
    {
        var y = yearMonth / 100;
        var m = yearMonth % 100;
        if (m <= 1) return (y - 1) * 100 + 12;
        return y * 100 + (m - 1);
    }

    public static decimal Delta(decimal current, decimal baseline) => current - baseline;
}

public sealed class FollowerSaleAuthRow
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public int ListId { get; set; }
    public int? CustomerSaleId { get; set; }
    public int? SaleDraftId { get; set; }
    public int FollowerUserId { get; set; }
    public string? FollowerName { get; set; }
    public int Rating { get; set; }
    public string? Note { get; set; }
    public decimal ShopStockEstimatedValue { get; set; }
    public decimal EstimatedDailyRevenue { get; set; }
    public DateTime AuthenticatedAtUtc { get; set; }
}

public sealed class FollowerPendingSaleRow
{
    public int CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? PhoneNumber { get; set; }
    public int ListId { get; set; }
    public string? ListName { get; set; }
    public int? CustomerSaleId { get; set; }
    public int? SaleDraftId { get; set; }
    public DateTime? SaleDate { get; set; }
    public decimal? AmountTotalSales { get; set; }
    public decimal? AmountDaySales { get; set; }
    public string? ShopName { get; set; }
    public string? Address { get; set; }
}

public sealed class FollowerListStatsRow
{
    public int ListId { get; set; }
    public string? ListName { get; set; }
    public int YearMonth { get; set; }
    public int PreviousYearMonth { get; set; }
    public int NewCustomersCount { get; set; }
    public decimal NewCustomersSaleSum { get; set; }
    public decimal NewCustomersDaySalesSum { get; set; }
    public int ZeroedCustomersCount { get; set; }
    public decimal ZeroedCustomersSaleSum { get; set; }
    public decimal ZeroedCustomersDaySalesSum { get; set; }
    public int NetCustomersCount { get; set; }
    public decimal NetSaleSum { get; set; }
    public decimal NetDaySalesSum { get; set; }
    public int ActiveCustomersCount { get; set; }
    public double AverageScore { get; set; }
    public string? FinalRatingLabel { get; set; }
    public int ExcellentCount { get; set; }
    public int GoodCount { get; set; }
    public int WeakCount { get; set; }
    public int RejectedCount { get; set; }
    public int LegalCount { get; set; }
    public int BadCount { get; set; }
    public int RatedCustomerCount { get; set; }
    public decimal CurrentTotalSales { get; set; }
    public decimal PreviousTotalSales { get; set; }
    public decimal AddedAmount { get; set; }
    public decimal CurrentDaySales { get; set; }
    public decimal PreviousDaySales { get; set; }
    public decimal DaySalesDelta { get; set; }
    public bool PreviousMonthFrozen { get; set; }
}

public sealed class FollowerListStatsCustomerRow
{
    public int CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? PhoneNumber { get; set; }
    public double AmountTotalSales { get; set; }
    public double AmountDaySales { get; set; }
    public double AmountRemaining { get; set; }
    public double? WeekPaid { get; set; }
    public double? PaidPercent { get; set; }
    public string? RatingLabel { get; set; }
    public int? Score { get; set; }
}

public interface IFollowerSaleAuthService
{
    Task EnsureSchemaAsync(CancellationToken ct = default);

    Task<IReadOnlyList<FollowerPendingSaleRow>> ListPendingAsync(
        int followerUserId,
        CancellationToken ct);

    Task<FollowerSaleAuthRow> AuthenticateAsync(
        int followerUserId,
        string? followerName,
        int customerId,
        int listId,
        int rating,
        string? note,
        decimal shopStockEstimatedValue,
        decimal estimatedDailyRevenue,
        CancellationToken ct);

    Task<FollowerSaleAuthRow?> GetForCustomerAsync(int customerId, int listId, CancellationToken ct);
}

public interface IFollowerListStatsService
{
    Task EnsureSchemaAsync(CancellationToken ct = default);

    Task<IReadOnlyList<FollowerListStatsRow>> GetStatsForFollowerAsync(
        int followerUserId,
        DateTime nowLocal,
        CancellationToken ct);

    Task<IReadOnlyList<FollowerListStatsCustomerRow>> GetRatingCustomersAsync(
        int followerUserId,
        int listId,
        string levelKey,
        DateTime nowLocal,
        CancellationToken ct);

    Task<IReadOnlyList<FollowerListStatsCustomerRow>> GetActiveCustomersAsync(
        int followerUserId,
        int listId,
        DateTime nowLocal,
        CancellationToken ct);
}
