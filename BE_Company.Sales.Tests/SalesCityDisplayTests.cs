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

        [Theory]
        [InlineData("DatabaseCompanyBasra", "basra-demo", "البصرة")]
        [InlineData("DatabaseCompanyNajaf", "najaf-demo", "النجف")]
        [InlineData("DatabaseCompanyBaghdadKarak", "karkh-demo", "الكرخ")]
        [InlineData(null, "unknown-legacy-xyz", "غير متوفر")]
        public void FriendlyOrUnavailable_Maps_Legacy_Or_Unavailable(string? cityName, string? cityValue, string expected)
        {
            Assert.Equal(expected, SalesCityDisplay.FriendlyOrUnavailable(cityName, cityValue));
        }
    }
}
