using BE_Company.Sales.Services;
using Xunit;

namespace BE_Company.Sales.Tests
{
    public class ActiveCustomerListsTests
    {
        [Fact]
        public void ActiveLists_UsePaymentReceipts_NotCustomerOrSaleDateCreate()
        {
            var sql = SalesActiveCustomerListsQuery.Sql;
            Assert.Contains("View_CustomersPaymentsDelegate_Final", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("AmountDenar", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("PaymentDate", sql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Customers.DateCreate", sql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("CustomersSales.DateCreate", sql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("FROM dbo.Customers ", sql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("FROM dbo.CustomersSales", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(new DateTime(2026, 7, 1), SalesActiveCustomerListsQuery.FromDate);
        }

        [Fact]
        public void ActiveLists_ThresholdIncludesOneDinar()
        {
            Assert.Contains("> 0", SalesActiveCustomerListsQuery.Sql, StringComparison.Ordinal);
            Assert.DoesNotContain(">= 1000", SalesActiveCustomerListsQuery.Sql, StringComparison.Ordinal);
            Assert.DoesNotContain("> 1", SalesActiveCustomerListsQuery.Sql, StringComparison.Ordinal);
        }
    }
}
