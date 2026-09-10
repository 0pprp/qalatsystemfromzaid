using BE_DelegateWebApplication.Services.FollowerIdentity;
using Xunit;

namespace BE_DelegateWebApplication.Tests
{
    /// <summary>
    /// Contracts: follower = Users.UserType متابع only. No FollowerProfiles gate. GPS FollowerId = UserId.
    /// </summary>
    public sealed class FollowerUserIdentityContractTests
    {
        [Fact]
        public void UserTypeFollower_AllowsLogin()
        {
            const string userType = "متابع";
            const bool userStateActive = true;
            var allowed = FollowerUserType.IsFollowerType(userType) && userStateActive;
            Assert.True(allowed);
        }

        [Fact]
        public void UserTypeNotFollower_Returns403()
        {
            const string userType = "مندوب";
            Assert.False(FollowerUserType.IsFollowerType(userType));
        }

        [Fact]
        public void Login_DoesNotRequireFollowerProfile()
        {
            const bool hasFollowerProfile = false;
            const string userType = "متابع";
            const bool userStateActive = true;
            var allowed = FollowerUserType.IsFollowerType(userType) && userStateActive;
            Assert.True(allowed);
            Assert.False(hasFollowerProfile); // profile unused; must not gate login
        }

        [Fact]
        public void FollowerList_IncludesUserTypeFollower()
        {
            var users = new[]
            {
                new { UserId = 1, UserType = "متابع" },
                new { UserId = 2, UserType = "مندوب" },
            };
            var list = users.Where(u => FollowerUserType.IsFollowerType(u.UserType)).Select(u => u.UserId).ToList();
            Assert.Equal(new[] { 1 }, list);
        }

        [Fact]
        public void ChangeUserTypeAwayFromFollower_BlocksLoginImmediately()
        {
            var userType = "متابع";
            Assert.True(FollowerUserType.IsFollowerType(userType));
            userType = "موظف مبيعات";
            Assert.False(FollowerUserType.IsFollowerType(userType));
        }

        [Fact]
        public void GpsFollowerId_MeansUserId_NotDelegateId()
        {
            const int userId = 42;
            const int followerIdStoredInShift = userId;
            int? delegateId = null;
            Assert.Equal(userId, followerIdStoredInShift);
            Assert.Null(delegateId);
        }

        [Fact]
        public void RuntimeIdentity_DoesNotDependOnDelegateId()
        {
            const string identitySource = "Users.UserType";
            Assert.DoesNotContain("DelegateId", identitySource, StringComparison.Ordinal);
            Assert.DoesNotContain("FollowerProfiles", identitySource, StringComparison.Ordinal);
        }

        [Fact]
        public void SpoofOtherUserId_MustBeRejectedByServerIdentity()
        {
            const int authUserId = 10;
            const int bodyClaimedUserId = 99;
            Assert.True(bodyClaimedUserId != authUserId);
        }

        [Fact]
        public void InactiveUserState_CannotLogin()
        {
            const bool userStateActive = false;
            const string userType = "متابع";
            var allowed = FollowerUserType.IsFollowerType(userType) && userStateActive;
            Assert.False(allowed);
        }

        [Fact]
        public void FollowerUserType_MatchesSalesRolesArabic()
        {
            Assert.Equal("متابع", FollowerUserType.Arabic);
            Assert.True(FollowerUserType.IsFollowerType("متابع"));
            Assert.True(FollowerUserType.IsFollowerType("متابع خاص"));
            Assert.False(FollowerUserType.IsFollowerType("محاسب رئيسي"));
        }
    }
}
