using BE_Company.Sales.DTO;
using BE_Company.Sales.Services;
using Xunit;

namespace BE_Company.Sales.Tests
{
    public class SalesShopMapsTests
    {
        [Fact]
        public void GoogleMapsUrl_UsesSavedCoordinates()
        {
            Assert.Equal(
                "https://www.google.com/maps?q=32.0375,44.4219",
                SalesShopMaps.GoogleMapsUrl(32.0375, 44.4219));
        }

        [Fact]
        public void GoogleMapsUrl_IsNullWhenCoordinatesMissing()
        {
            Assert.Null(SalesShopMaps.GoogleMapsUrl(null, 44.4219));
            Assert.Null(SalesShopMaps.GoogleMapsUrl(32.0375, null));
            Assert.Null(SalesShopMaps.GoogleMapsUrl(null, null));
        }

        [Fact]
        public void ShopProfileDto_ExposesLatitudeAndLongitude()
        {
            var shop = new SalesShopProfileDTO
            {
                Latitude = 32.0375,
                Longitude = 44.4219
            };
            Assert.Equal(32.0375, shop.Latitude);
            Assert.Equal(44.4219, shop.Longitude);
            Assert.Equal(
                "https://www.google.com/maps?q=32.0375,44.4219",
                SalesShopMaps.GoogleMapsUrl(shop.Latitude, shop.Longitude));
        }
    }

    public class SalesDocumentPreferenceTests
    {
        [Fact]
        public void PreferDisplayDocuments_UsesCombinedFileFirst()
        {
            var docs = new List<SalesDocumentDTO>
            {
                new() { Type = SalesDocumentService.Contract },
                new() { Type = SalesDocumentService.PromissoryNote },
                new() { Type = SalesDocumentService.SaleDocuments },
            };
            var preferred = SalesDocumentService.PreferDisplayDocuments(docs);
            Assert.Single(preferred);
            Assert.Equal(SalesDocumentService.SaleDocuments, preferred[0].Type);
        }

        [Fact]
        public void PreferDisplayDocuments_FallsBackToLegacyPair()
        {
            var docs = new List<SalesDocumentDTO>
            {
                new() { Type = SalesDocumentService.PreviewContract },
                new() { Type = SalesDocumentService.Contract },
                new() { Type = SalesDocumentService.PromissoryNote },
            };
            var preferred = SalesDocumentService.PreferDisplayDocuments(docs);
            Assert.Equal(2, preferred.Count);
            Assert.Contains(preferred, d => d.Type == SalesDocumentService.Contract);
            Assert.Contains(preferred, d => d.Type == SalesDocumentService.PromissoryNote);
        }
    }
}
