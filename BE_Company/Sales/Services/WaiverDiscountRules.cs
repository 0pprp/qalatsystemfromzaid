using System.Globalization;

namespace BE_Company.Sales.Services;

/// <summary>
/// Pure rules for accountant waiver discount (sale-price reduction, not payment).
/// </summary>
public static class WaiverDiscountRules
{
    public const string PendingDecisionType = "طلب تنازل";
    public const string FinalDecisionType = "تنازل";

    /// <summary>
    /// Caps requested discount so sale total never drops below already-paid amount.
    /// </summary>
    public static double CapDiscount(double saleTotal, double paidTotal, double requested)
    {
        if (requested <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(requested), "مبلغ التنازل يجب أن يكون أكبر من صفر");
        }

        var remaining = Math.Max(0, saleTotal - Math.Max(0, paidTotal));
        return Math.Min(requested, remaining);
    }

    public static string BuildCustomerNote(string? customerName, double amount, DateTime localDate)
    {
        var name = string.IsNullOrWhiteSpace(customerName) ? "—" : customerName.Trim();
        var amountText = amount.ToString("#,##0.##", CultureInfo.InvariantCulture);
        var dateText = localDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return $"تم التنازل عن الزبون {name} بمبلغ {amountText} من قبل المدير المفوض بتاريخ {dateText}";
    }
}
