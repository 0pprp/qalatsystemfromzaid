using BE_Company.Sales.Services;
using Xunit;

namespace BE_Company.Sales.Tests
{
    public sealed class SalesCityDisplayTests
    {
        [Fact]
        public void DatabaseCatalogIsInternal()
        {
            Assert.True(SalesCityDisplay.IsInternalKey("DatabaseCompanyNajaf_DEMO"));
            Assert.True(SalesCityDisplay.IsInternalKey("najaf-demo", "najaf-demo"));
            Assert.False(SalesCityDisplay.IsInternalKey("النجف - DEMO"));
        }

        [Fact]
        public void HumanNamePrefersReadableFallback()
        {
            Assert.Equal("النجف - DEMO", SalesCityDisplay.HumanName("DatabaseCompanyNajaf_DEMO", "النجف - DEMO", "najaf-demo"));
            Assert.Equal("النجف", SalesCityDisplay.HumanName("النجف", "النجف - DEMO", "najaf-demo"));
        }
    }
}
