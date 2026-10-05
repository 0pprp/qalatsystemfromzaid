using BE_Company.Sales.DTO;

namespace BE_Company.Sales.Services;

/// <summary>
/// Builds the profile "الحساب والتسديدات" summary.
/// Official Customers view can be 0 when SelectItemsSales failed to attach
/// while SalesDraft still holds FinalSalePrice — never show a blank zero account
/// when a completed module sale exists.
/// </summary>
public static class SalesCustomerPaymentSummaryBuilder
{
    public static SalesPaymentSummaryDTO? Build(
        double? accountTotal,
        double? accountReceipts,
        double? accountRemaining,
        DateTime? accountLastPayment,
        int? accountReceiptCount,
        IEnumerable<double> officialSaleTotals,
        double paymentReceipts,
        DateTime? lastPaymentFromPayments,
        IEnumerable<double> completedModuleSaleTotals,
        IEnumerable<double> moduleDownPayments)
    {
        var officialSum = SumPositive(officialSaleTotals);
        var moduleSum = SumPositive(completedModuleSaleTotals);
        var downSum = SumPositive(moduleDownPayments);

        var total = PositiveOrZero(accountTotal);
        if (total <= 0)
        {
            total = officialSum;
        }

        if (total <= 0)
        {
            total = moduleSum;
        }

        if (total <= 0 && paymentReceipts <= 0 && downSum <= 0)
        {
            return null;
        }

        double receipts;
        DateTime? lastPayment;
        int? receiptCount;

        if (PositiveOrZero(accountTotal) > 0)
        {
            receipts = PositiveOrZero(accountReceipts);
            lastPayment = accountLastPayment;
            receiptCount = accountReceiptCount;
            var remaining = accountRemaining;
            if (remaining is null)
            {
                remaining = Round3(total - receipts);
            }

            return new SalesPaymentSummaryDTO
            {
                AmountTotalSales = total,
                ReceiptsTotal = receipts,
                AmountRemaining = remaining,
                LastPaymentDate = lastPayment,
                CountReceiptDevice = receiptCount
            };
        }

        // Fallback path: reconstruct from official sale rows / module sales + payments.
        if (paymentReceipts > 0)
        {
            receipts = paymentReceipts;
            lastPayment = lastPaymentFromPayments;
            receiptCount = null;
        }
        else if (downSum > 0)
        {
            receipts = downSum;
            lastPayment = lastPaymentFromPayments;
            receiptCount = downSum > 0 ? 1 : 0;
        }
        else
        {
            receipts = PositiveOrZero(accountReceipts);
            lastPayment = accountLastPayment ?? lastPaymentFromPayments;
            receiptCount = accountReceiptCount;
        }

        return new SalesPaymentSummaryDTO
        {
            AmountTotalSales = total,
            ReceiptsTotal = receipts,
            AmountRemaining = Round3(total - receipts),
            LastPaymentDate = lastPayment,
            CountReceiptDevice = receiptCount
        };
    }

    private static double SumPositive(IEnumerable<double> values) =>
        values?.Where(v => v > 0).Sum() ?? 0;

    private static double PositiveOrZero(double? value) =>
        value is > 0 ? value.Value : 0;

    private static double Round3(double value) =>
        Math.Round(value, 3, MidpointRounding.AwayFromZero);
}
