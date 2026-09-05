using BE_Company.Sales.Services;
using Xunit;

namespace BE_Company.Sales.Tests
{
    public class ActiveCustomerListsTests
    {
        [Fact]
        public void ActiveLists_ReturnAllBranchDelegates_Unfiltered()
        {
            var sql = SalesActiveCustomerListsQuery.Sql;
            Assert.Contains("FROM dbo.Delegates", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("DelegateID AS ListId", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("DelegateName AS ListName", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("ORDER BY d.DelegateName", sql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("View_CustomersPaymentsDelegate_Final", sql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("AmountDenar", sql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("PaymentDate", sql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("DateCreate", sql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("WHERE", sql, StringComparison.OrdinalIgnoreCase);
        }
    }
}
