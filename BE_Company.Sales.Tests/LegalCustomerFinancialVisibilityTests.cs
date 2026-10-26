using BE_Company.Sales.Services;
using Xunit;

namespace BE_Company.Sales.Tests;

public sealed class LegalCustomerFinancialVisibilityTests
{
    [Fact]
    public void ShouldHide_Always_False()
    {
        Assert.False(LegalCustomerFinancialVisibility.ShouldHidePaidAndRemaining(true));
        Assert.False(LegalCustomerFinancialVisibility.ShouldHidePaidAndRemaining(false));
    }

    [Fact]
    public void MaskRatingCustomerCard_Keeps_Paid_And_Remaining_For_Legal()
    {
        var card = new EmployeeRatingCustomerCardDto
        {
            CustomerId = 1,
            AmountPaid = 50000,
            AmountRemaining = 120000,
            AmountTotal = 170000
        };

        LegalCustomerFinancialVisibility.MaskRatingCustomerCard(card, isLegal: true);

        Assert.True(card.IsLegal);
        Assert.False(card.HidePaidRemaining);
        Assert.Equal(50000, card.AmountPaid);
        Assert.Equal(120000, card.AmountRemaining);
        Assert.Equal(170000, card.AmountTotal);
    }

    [Fact]
    public void MaskRatingCustomerCard_NoOp_When_Not_Legal()
    {
        var card = new EmployeeRatingCustomerCardDto
        {
            AmountPaid = 10,
            AmountRemaining = 20
        };

        LegalCustomerFinancialVisibility.MaskRatingCustomerCard(card, isLegal: false);

        Assert.False(card.HidePaidRemaining);
        Assert.Equal(10, card.AmountPaid);
        Assert.Equal(20, card.AmountRemaining);
    }

    [Fact]
    public void NonLegalPredicate_Uses_IsLegal_Column()
    {
        var sql = LegalCustomerFinancialVisibility.NonLegalCustomerPredicate("c");
        Assert.Contains("IsLegal", sql, StringComparison.Ordinal);
        Assert.Contains("c.", sql, StringComparison.Ordinal);
    }
}
