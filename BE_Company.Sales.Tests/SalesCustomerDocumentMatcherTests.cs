using BE_Company.Sales.DTO;
using BE_Company.Sales.Services;
using Xunit;

namespace BE_Company.Sales.Tests
{
    public class SalesCustomerDocumentMatcherTests
    {
        [Fact]
        public void EmployeeDoc_MatchesManagerProfile_BySaleId()
        {
            var row = new SalesCustomerDocumentDTO
            {
                Id = 3,
                SaleId = 10,
                CustomerId = null,
                CustomerName = "أحمد علي",
                CustomerPhone = "07701234567",
                DocumentType = SalesCustomerDocumentTypes.NationalIdFront
            };

            Assert.True(SalesCustomerDocumentMatcher.Matches(
                row, customerId: 88, customerName: "اسم الحساب", phone: "07709999999", saleIds: [10, 11]));
        }

        [Fact]
        public void EmployeeDoc_Matches_ByNormalizedPhone_WhenCustomerIdMissing()
        {
            var row = new SalesCustomerDocumentDTO
            {
                SaleId = 4,
                CustomerId = null,
                CustomerName = "أحمد  علي",
                CustomerPhone = "+964 7701234567"
            };

            Assert.True(SalesCustomerDocumentMatcher.Matches(
                row, 99, "أحمد علي", "07701234567", saleIds: []));
        }

        [Fact]
        public void UnrelatedDoc_DoesNotMatch()
        {
            var row = new SalesCustomerDocumentDTO
            {
                SaleId = 1,
                CustomerId = 2,
                CustomerName = "زبون آخر",
                CustomerPhone = "07801111111"
            };

            Assert.False(SalesCustomerDocumentMatcher.Matches(
                row, 88, "أحمد علي", "07701234567", saleIds: [10]));
        }

        [Fact]
        public void KnownDocumentTypes_AreTheFourOptionalKycTypes()
        {
            Assert.Equal(4, SalesCustomerDocumentTypes.All.Length);
            Assert.True(SalesCustomerDocumentTypes.IsKnown("NationalIdFront"));
            Assert.True(SalesCustomerDocumentTypes.IsKnown("nationalidback"));
            Assert.Equal("البطاقة الوطنية - أمامية", SalesCustomerDocumentTypes.Label("NationalIdFront"));
            Assert.Equal("تأييد السكن", SalesCustomerDocumentTypes.Label("ResidenceCertificate"));
        }
    }

    public class SalesCustomerListMappingTests
    {
        [Fact]
        public void ProfileDoesNotTreatCityAsDelegateList()
        {
            var sql = SalesActiveCustomerListsQuery.Sql;
            Assert.Contains("DelegateID AS ListId", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("DelegateName AS ListName", sql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("CityName", sql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Province", sql, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void DraftDtoKeepsListIndependentFromCity()
        {
            var draft = new SalesDraftDTO
            {
                CityName = "النجف",
                Province = "النجف",
                CustomerListId = 17,
                CustomerListName = "قائمة الكوفة"
            };
            Assert.Equal(17, draft.CustomerListId);
            Assert.Equal("قائمة الكوفة", draft.CustomerListName);
            Assert.Equal("النجف", draft.CityName);
            Assert.NotEqual(draft.CustomerListName, draft.CityName);
        }
    }
}
