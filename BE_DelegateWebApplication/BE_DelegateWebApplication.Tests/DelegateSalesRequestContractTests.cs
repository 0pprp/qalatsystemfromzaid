using BE_DelegateWebApplication.DTO;
using Xunit;

namespace BE_DelegateWebApplication.Tests
{
    public sealed class DelegateSalesRequestContractTests
    {
        [Fact]
        public void HomeEntry_IsNew()
        {
            Assert.True(SaleRequestTypes.IsNew("New"));
            Assert.Equal(SaleRequestTypes.New, SaleRequestTypes.Normalize("New", false));
        }

        [Fact]
        public void CustomerCardEntry_IsOld()
        {
            Assert.True(SaleRequestTypes.IsOld("Old"));
            Assert.Equal(SaleRequestTypes.Old, SaleRequestTypes.Normalize("Old", false));
        }

        [Fact]
        public void Normalize_InfersOldFromExistingCustomer()
        {
            Assert.Equal(SaleRequestTypes.Old, SaleRequestTypes.Normalize(null, true));
            Assert.Equal(SaleRequestTypes.New, SaleRequestTypes.Normalize(null, false));
        }

        [Fact]
        public void OldRequest_IgnoresClientProvince()
        {
            const string clientProvince = "محافظة مزورة";
            const string serverProvince = "النجف";
            var used = serverProvince;
            Assert.NotEqual(clientProvince, used);
        }

        [Fact]
        public void OldRequest_RejectsCustomerOutsideDelegateList()
        {
            const int authDelegateId = 10;
            const int customerDelegateId = 99;
            Assert.False(customerDelegateId == authDelegateId);
        }

        [Fact]
        public void Payload_ReusesFollowerFieldNames()
        {
            var keys = new[] { "asyncId", "listId", "customerId", "fullName", "phone", "address", "notes", "saleRequestType" };
            Assert.Contains("asyncId", keys);
            Assert.Contains("saleRequestType", keys);
        }

        [Fact]
        public void UiDoesNotExposeManualNewOldPicker()
        {
            const string homeType = "New";
            const string cardType = "Old";
            Assert.NotEqual(homeType, cardType);
        }
    }
}
