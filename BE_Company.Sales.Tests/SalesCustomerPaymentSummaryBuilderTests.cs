using BE_Company.Sales.DTO;
using BE_Company.Sales.Services;
using Xunit;

namespace BE_Company.Sales.Tests;

public class SalesCustomerPaymentSummaryBuilderTests
{
    [Fact]
    public void Prefers_official_account_when_total_positive()
    {
        var summary = SalesCustomerPaymentSummaryBuilder.Build(
            accountTotal: 2_900_000,
            accountReceipts: 145_000,
            accountRemaining: 2_755_000,
            accountLastPayment: new DateTime(2026, 9, 20),
            accountReceiptCount: 1,
            officialSaleTotals: [0],
            paymentReceipts: 0,
            lastPaymentFromPayments: null,
            completedModuleSaleTotals: [2_900_000],
            moduleDownPayments: [145_000]);

        Assert.Equal(2_900_000, summary.AmountTotalSales);
        Assert.Equal(145_000, summary.ReceiptsTotal);
        Assert.Equal(2_755_000, summary.AmountRemaining);
        Assert.Equal(1, summary.CountReceiptDevice);
    }

    [Fact]
    public void Falls_back_to_official_sales_rows_when_account_total_is_zero()
    {
        var summary = SalesCustomerPaymentSummaryBuilder.Build(
            accountTotal: 0,
            accountReceipts: 0,
            accountRemaining: 0,
            accountLastPayment: null,
            accountReceiptCount: 0,
            officialSaleTotals: [2_900_000],
            paymentReceipts: 145_000,
            lastPaymentFromPayments: new DateTime(2026, 9, 19),
            completedModuleSaleTotals: [],
            moduleDownPayments: []);

        Assert.Equal(2_900_000, summary.AmountTotalSales);
        Assert.Equal(145_000, summary.ReceiptsTotal);
        Assert.Equal(2_755_000, summary.AmountRemaining);
        Assert.Equal(new DateTime(2026, 9, 19), summary.LastPaymentDate);
    }

    [Fact]
    public void Falls_back_to_completed_module_sale_when_official_account_is_zero()
    {
        // Reproduces profile bug: Details show FinalSalePrice 2,900,000 but PaymentSummary is 0.
        var summary = SalesCustomerPaymentSummaryBuilder.Build(
            accountTotal: 0,
            accountReceipts: 0,
            accountRemaining: 0,
            accountLastPayment: null,
            accountReceiptCount: 0,
            officialSaleTotals: [],
            paymentReceipts: 0,
            lastPaymentFromPayments: null,
            completedModuleSaleTotals: [2_900_000],
            moduleDownPayments: [145_000]);

        Assert.Equal(2_900_000, summary.AmountTotalSales);
        Assert.Equal(145_000, summary.ReceiptsTotal);
        Assert.Equal(2_755_000, summary.AmountRemaining);
        Assert.Equal(1, summary.CountReceiptDevice);
    }

    [Fact]
    public void Prefers_payment_rows_over_module_down_payment_for_receipts()
    {
        var summary = SalesCustomerPaymentSummaryBuilder.Build(
            accountTotal: 0,
            accountReceipts: 0,
            accountRemaining: 0,
            accountLastPayment: null,
            accountReceiptCount: 0,
            officialSaleTotals: [],
            paymentReceipts: 200_000,
            lastPaymentFromPayments: new DateTime(2026, 9, 21),
            completedModuleSaleTotals: [2_900_000],
            moduleDownPayments: [145_000]);

        Assert.Equal(2_900_000, summary.AmountTotalSales);
        Assert.Equal(200_000, summary.ReceiptsTotal);
        Assert.Equal(2_700_000, summary.AmountRemaining);
    }

    [Fact]
    public void Returns_null_when_no_amounts_anywhere()
    {
        var summary = SalesCustomerPaymentSummaryBuilder.Build(
            accountTotal: 0,
            accountReceipts: 0,
            accountRemaining: 0,
            accountLastPayment: null,
            accountReceiptCount: 0,
            officialSaleTotals: [],
            paymentReceipts: 0,
            lastPaymentFromPayments: null,
            completedModuleSaleTotals: [],
            moduleDownPayments: []);

        Assert.Null(summary);
    }
}
