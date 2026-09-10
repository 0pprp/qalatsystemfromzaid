using BE_Company.Sales.Services;
using Xunit;

namespace BE_Company.Sales.Tests
{
    public sealed class DelegateSaleRequestTypeTests
    {
        [Fact]
        public void ArabicLabels_NewAndOld()
        {
            Assert.Equal("مبيع جديد", SaleRequestTypes.ArabicLabel("New"));
            Assert.Equal("مبيع قديم", SaleRequestTypes.ArabicLabel("Old"));
        }

        [Fact]
        public void DelegateSource_IsRecognized()
        {
            Assert.True(SalesRequestSources.IsDelegate("Delegate"));
            Assert.False(SalesRequestSources.IsDelegate("Follower"));
        }
    }
}
