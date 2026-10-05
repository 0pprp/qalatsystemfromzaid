using BE_Company.Sales.Services;
using Xunit;

namespace BE_Company.Sales.Tests;

public sealed class SalesCascadeVoidRulesTests
{
    [Fact]
    public void MissingOfficialSale_SkipsCompensatingDelete()
    {
        Assert.False(SalesCascadeVoidRules.ShouldDeleteOfficialSale(null));
        Assert.False(SalesCascadeVoidRules.ShouldDeleteOfficialSale(0));
        Assert.False(SalesCascadeVoidRules.ShouldDeleteOfficialSale(-1));
    }

    [Fact]
    public void ResolvedOfficialSale_AllowsCompensatingDelete()
    {
        Assert.True(SalesCascadeVoidRules.ShouldDeleteOfficialSale(55209));
    }

    [Fact]
    public void MissingOfficialSale_Warning_IsArabicAndNonBlocking()
    {
        Assert.Contains("المبيع الرسمي", SalesCascadeVoidRules.MissingOfficialSaleWarningAr);
        Assert.Contains("إبطال المسودة", SalesCascadeVoidRules.MissingOfficialSaleWarningAr);
    }

    [Fact]
    public void PhantomStoredCustomerSaleId_StopsGuessingAnotherSale()
    {
        Assert.True(SalesCascadeVoidRules.ShouldStopAfterPhantomStoredId(55211, storedIdExistsInOfficial: false));
        Assert.False(SalesCascadeVoidRules.ShouldStopAfterPhantomStoredId(55211, storedIdExistsInOfficial: true));
        Assert.False(SalesCascadeVoidRules.ShouldStopAfterPhantomStoredId(null, storedIdExistsInOfficial: false));
        Assert.False(SalesCascadeVoidRules.ShouldStopAfterPhantomStoredId(0, storedIdExistsInOfficial: false));
    }

    [Fact]
    public void ProvenLink_RequiresStoredIdAndExistence()
    {
        Assert.Equal(100, SalesCascadeVoidRules.ResolveProvenOfficialSaleId(100, storedIdExistsInOfficial: true));
        Assert.Null(SalesCascadeVoidRules.ResolveProvenOfficialSaleId(100, storedIdExistsInOfficial: false));
        Assert.Null(SalesCascadeVoidRules.ResolveProvenOfficialSaleId(null, storedIdExistsInOfficial: false));
        Assert.Null(SalesCascadeVoidRules.ResolveProvenOfficialSaleId(0, storedIdExistsInOfficial: true));
    }

    [Fact]
    public void PaymentDelete_OnlyWhenSoleOfficialSale()
    {
        Assert.True(SalesCascadeVoidRules.CanDeleteCustomerPaymentsForSale(55, officialSaleCountForCustomer: 1));
        Assert.False(SalesCascadeVoidRules.CanDeleteCustomerPaymentsForSale(55, officialSaleCountForCustomer: 2));
        Assert.False(SalesCascadeVoidRules.CanDeleteCustomerPaymentsForSale(null, officialSaleCountForCustomer: 1));
        Assert.False(SalesCascadeVoidRules.CanDeleteCustomerPaymentsForSale(55, officialSaleCountForCustomer: 0));
    }
}
