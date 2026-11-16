using System.Globalization;
using BE_Company.Sales.DTO;

namespace BE_Company.Sales.Services
{
    /// <summary>
    /// Flags a fully paid customer when one receipt in the 30 days up to and
    /// including the last payment is at least 20% of the sale price.
    /// </summary>
    public static class ClosingLumpPayments
    {
        public const double Share = 0.20;
        public const int LookbackDays = 30;

        public static bool AppliesToCategory(string? category) =>
            string.Equals(category, SalesRequestEvaluationService.CategoryTriple, StringComparison.Ordinal)
            || string.Equals(category, SalesRequestEvaluationService.CategoryPhone, StringComparison.Ordinal);

        public static bool IsFinishedAccount(double totalSale, double remaining) =>
            totalSale > 0 && remaining >= 0 && remaining < 1;

        public static bool IsLargeEnough(double amount, double totalSale) =>
            totalSale > 0 && amount > 0 && amount + 0.5 >= totalSale * Share;

        public static List<SalesRequestLargePaymentNoteDTO> Find(
            IEnumerable<CustomerPaymentSlice> payments,
            double totalSale,
            double remaining)
        {
            if (!IsFinishedAccount(totalSale, remaining))
            {
                return [];
            }

            var rows = new List<(DateTime Day, double Amount)>();
            foreach (var pay in payments)
            {
                if (pay.Amount <= 0)
                {
                    continue;
                }

                rows.Add((pay.PaymentDate.Date, pay.Amount));
            }

            if (rows.Count == 0)
            {
                return [];
            }

            var last = rows.Max(r => r.Day);
            var start = last.AddDays(-LookbackDays);
            return rows
                .Where(r => r.Day >= start && r.Day <= last && IsLargeEnough(r.Amount, totalSale))
                .OrderByDescending(r => r.Day)
                .ThenByDescending(r => r.Amount)
                .Select(r => new SalesRequestLargePaymentNoteDTO
                {
                    PaymentDate = r.Day,
                    Amount = r.Amount,
                    Text = Format(r.Day, r.Amount)
                })
                .ToList();
        }

        public static string Format(DateTime date, double amount)
        {
            var money = Math.Round(amount, 0, MidpointRounding.AwayFromZero)
                .ToString("N0", CultureInfo.InvariantCulture);
            return $"في يوم {date.Day}/{date.Month} دفع {money} دينار";
        }
    }

    public sealed class CustomerPaymentSlice
    {
        public int CustomerId { get; init; }
        public DateTime PaymentDate { get; init; }
        public double Amount { get; init; }
    }
}
