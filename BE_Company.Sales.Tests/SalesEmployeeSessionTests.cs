using BE_Company.Services;
using Xunit;

namespace BE_Company.Sales.Tests
{
    public sealed class SalesEmployeeSessionTests
    {
        [Fact]
        public void AppliesOnlyToSalesEmployee()
        {
            Assert.True(SalesEmployeeSession.AppliesTo("موظف مبيعات"));
            Assert.False(SalesEmployeeSession.AppliesTo("مدير مبيعات"));
            Assert.False(SalesEmployeeSession.AppliesTo("مدير فرع"));
            Assert.False(SalesEmployeeSession.AppliesTo("محاسب فرعي"));
        }

        [Fact]
        public void MissingClaimMatchesUnbumpedVersion()
        {
            Assert.Equal(0, SalesEmployeeSession.ParseClaim(null));
            Assert.True(SalesEmployeeSession.IsCurrent(0, 0));
        }

        [Fact]
        public void NewLoginInvalidatesPreviousToken()
        {
            Assert.False(SalesEmployeeSession.IsCurrent(SalesEmployeeSession.ParseClaim(null), 1));
            Assert.False(SalesEmployeeSession.IsCurrent(1, 2));
            Assert.True(SalesEmployeeSession.IsCurrent(2, 2));
        }
    }
}
