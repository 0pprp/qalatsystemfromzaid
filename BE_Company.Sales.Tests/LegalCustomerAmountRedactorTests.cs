using BE_Company.Sales.Services;
using Xunit;

namespace BE_Company.Sales.Tests;

public sealed class LegalCustomerAmountRedactorTests
{
    private sealed class CustomerRow
    {
        public bool IsLegal { get; set; }
        public double? ReceiptsTotal { get; set; }
        public double AmountRemaining { get; set; }
        public int ReceiptRateDevice { get; set; }
        public double? AmountTotalSales { get; set; }
        public bool HidePaidRemaining { get; set; }
    }

    private sealed class PaymentLine
    {
        public double? Amount { get; set; }
        public DateTime? PaymentDate { get; set; }
    }

    private sealed class SaleOrder
    {
        public decimal FinalSalePrice { get; set; }
        public decimal DownPayment { get; set; }
    }

    private sealed class Profile
    {
        public bool IsLegal { get; set; }
        public double? ReceiptsTotal { get; set; }
        public double? AmountRemaining { get; set; }
        public List<PaymentLine> Payments { get; set; } = [];
        public SaleOrder? Order { get; set; }
    }

    [Fact]
    public void Redact_Clears_Paid_And_Remaining_For_Legal_Customer()
    {
        var row = new CustomerRow
        {
            IsLegal = true,
            ReceiptsTotal = 50000,
            AmountRemaining = 120000,
            ReceiptRateDevice = 30,
            AmountTotalSales = 170000
        };

        LegalCustomerAmountRedactor.Redact(row);

        Assert.Null(row.ReceiptsTotal);
        Assert.Equal(0, row.AmountRemaining);
        Assert.Equal(0, row.ReceiptRateDevice);
        Assert.Equal(170000, row.AmountTotalSales);
        Assert.True(row.HidePaidRemaining);
    }

    [Fact]
    public void Redact_Keeps_Amounts_When_Not_Legal()
    {
        var row = new CustomerRow
        {
            IsLegal = false,
            ReceiptsTotal = 10,
            AmountRemaining = 20,
            ReceiptRateDevice = 5
        };

        LegalCustomerAmountRedactor.Redact(row);

        Assert.Equal(10, row.ReceiptsTotal);
        Assert.Equal(20, row.AmountRemaining);
        Assert.Equal(5, row.ReceiptRateDevice);
        Assert.False(row.HidePaidRemaining);
    }

    [Fact]
    public void Redact_Clears_Nested_Payment_History_And_Keeps_Sale_Order_Price()
    {
        var profile = new Profile
        {
            IsLegal = true,
            ReceiptsTotal = 8000,
            AmountRemaining = 2000,
            Payments = [new PaymentLine { Amount = 8000, PaymentDate = DateTime.UtcNow }],
            Order = new SaleOrder { FinalSalePrice = 10000, DownPayment = 1000 }
        };

        LegalCustomerAmountRedactor.Redact(profile);

        Assert.Null(profile.ReceiptsTotal);
        Assert.Null(profile.AmountRemaining);
        Assert.Null(profile.Payments[0].Amount);
        Assert.NotNull(profile.Payments[0].PaymentDate);
        Assert.Equal(10000, profile.Order!.FinalSalePrice);
        Assert.Equal(1000, profile.Order.DownPayment);
    }

    [Fact]
    public void MainAccountant_Name_Matches_Company_Role()
    {
        Assert.True(LegalCustomerAmountRedactor.IsMainAccountant("محاسب رئيسي"));
        Assert.False(LegalCustomerAmountRedactor.IsMainAccountant("مدير مبيعات"));
        Assert.False(LegalCustomerAmountRedactor.IsMainAccountant(null));
    }
}
