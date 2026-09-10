using BE_Company.Sales.Authorization;
using Xunit;

namespace BE_Company.Tests
{
    /// <summary>
    /// Follower management is Users.UserType only (no FollowerAdmin / FollowerProfiles gate).
    /// </summary>
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
        public void ChangingTypeAway_RemovesFollowerAccess()
        {
            Assert.True(SalesRoles.IsFollower(SalesRoles.UserTypeFollower));
            Assert.False(SalesRoles.IsFollower("مندوب"));
        }

        [Fact]
        public void AccountantPolicy_StillMainAccountantOnly()
        {
            Assert.Equal("محاسب رئيسي", SalesRoles.UserTypeMainAccountant);
        }
    }
}
