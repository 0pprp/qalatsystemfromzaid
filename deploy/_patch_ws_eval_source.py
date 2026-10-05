from pathlib import Path

def must_replace(path: Path, old: str, new: str, label: str) -> None:
    t = path.read_text(encoding="utf-8")
    if old not in t:
        raise SystemExit(f"NOT FOUND: {label} in {path}")
    path.write_text(t.replace(old, new, 1), encoding="utf-8")
    print("patched", label)

root = Path(r"C:\GitHub\SalesHaider-main\SalesHaider-main")

must_replace(
    root / "BE_DelegateWebApplication/BE_DelegateWebApplication/Services/FollowerSaleAuthService.Contracts.cs",
    """    public static string FormatShopEvaluationBlock(
        int rating,
        string? note,
        decimal shopStockEstimatedValue,
        decimal estimatedDailyRevenue,
        string? customerName = null,
        string? customerPhone = null,
        decimal? saleAmount = null)
    {
        var lines = new List<string>
        {
            "تقييم المتجر / مبيعات خاطئة",
            $"التقييم: {rating}",
        };
        if (!string.IsNullOrWhiteSpace(note))
            lines.Add($"الملاحظة: {note.Trim()}");
""",
    """    /// <param name="evaluatorRole">
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
""",
    "FormatShopEvaluationBlock",
)

must_replace(
    root / "BE_DelegateWebApplication/BE_DelegateWebApplication/Controllers/FollowersController.cs",
    """                    var reason = FollowerSaleAuthRules.FormatShopEvaluationBlock(
                        row.Rating,
                        row.Note,
                        row.ShopStockEstimatedValue,
                        row.EstimatedDailyRevenue,
                        scope?.CustomerName,
                        scope?.Phone);""",
    """                    var reason = FollowerSaleAuthRules.FormatShopEvaluationBlock(
                        row.Rating,
                        row.Note,
                        row.ShopStockEstimatedValue,
                        row.EstimatedDailyRevenue,
                        scope?.CustomerName,
                        scope?.Phone,
                        evaluatorRole: "متابع");""",
    "FollowersController reason",
)

must_replace(
    root / "BE_DelegateWebApplication/BE_DelegateWebApplication/Controllers/DelegatesController.cs",
    """                var evalBlock = FollowerSaleAuthRules.FormatShopEvaluationBlock(
                    rating, shopRatingNote, stock, daily, name, phone);""",
    """                var evalBlock = FollowerSaleAuthRules.FormatShopEvaluationBlock(
                    rating, shopRatingNote, stock, daily, name, phone,
                    evaluatorRole: "مندوب الجباية");""",
    "DelegatesController evalBlock",
)

must_replace(
    root / "BE_DelegateWebApplication/BE_DelegateWebApplication/Controllers/DelegatesController.cs",
    """                var reason = FollowerSaleAuthRules.FormatShopEvaluationBlock(
                    row.Rating,
                    row.Note,
                    row.ShopStockEstimatedValue,
                    row.EstimatedDailyRevenue,
                    scope.Value.CustomerName,
                    scope.Value.Phone,
                    row.SaleAmount);""",
    """                var reason = FollowerSaleAuthRules.FormatShopEvaluationBlock(
                    row.Rating,
                    row.Note,
                    row.ShopStockEstimatedValue,
                    row.EstimatedDailyRevenue,
                    scope.Value.CustomerName,
                    scope.Value.Phone,
                    row.SaleAmount,
                    evaluatorRole: "مندوب الجباية");""",
    "DelegatesController first-collection reason",
)

print("BE OK")
