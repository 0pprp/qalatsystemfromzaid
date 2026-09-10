using BE_Company.Sales.Authorization;
using Xunit;

namespace BE_Company.Tests
{
    public sealed class FollowerAdminContractTests
    {
        [Fact]
        public void UserTypeFollower_IsRecognized()
        {
            Assert.True(SalesRoles.IsFollower("متابع"));
            Assert.False(SalesRoles.IsFollower("مندوب"));
            Assert.False(SalesRoles.IsFollower("موظف مبيعات"));
        }

        [Fact]
        public void AccountantPolicy_StillMainAccountantOnly()
        {
            Assert.Equal("محاسب رئيسي", SalesRoles.UserTypeMainAccountant);
        }
    }
}
