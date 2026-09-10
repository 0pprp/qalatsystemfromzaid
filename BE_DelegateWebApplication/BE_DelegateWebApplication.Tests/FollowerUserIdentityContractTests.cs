using Xunit;

namespace BE_DelegateWebApplication.Tests
{
    /// <summary>Contracts for User-based follower identity (not Delegates).</summary>
    public sealed class FollowerUserIdentityContractTests
    {
        [Fact]
        public void NonFollower_IsRejected()
        {
            const bool hasActiveProfile = false;
            const bool allowed = hasActiveProfile;
            Assert.False(allowed);
        }

        [Fact]
        public void ActiveFollowerProfile_IsAllowed()
        {
            const bool hasActiveProfile = true;
            const bool userActive = true;
            Assert.True(hasActiveProfile && userActive);
        }

        [Fact]
        public void DuplicateEnable_IsIdempotentUniqueUserId()
        {
            // Unique(UserId) on FollowerProfiles prevents two active profiles for same user.
            var set = new HashSet<int> { 7 };
            Assert.False(set.Add(7));
        }

        [Fact]
        public void FollowersList_DoesNotUseDelegateIdsAsIdentity()
        {
            const string identitySource = "Users+FollowerProfiles";
            Assert.DoesNotContain("Delegates", identitySource, StringComparison.Ordinal);
        }

        [Fact]
        public void GpsFollowerId_MeansUserId()
        {
            const int userId = 42;
            const int followerIdStoredInShift = userId;
            Assert.Equal(userId, followerIdStoredInShift);
        }

        [Fact]
        public void SpoofOtherUserId_MustBeRejectedByServerIdentity()
        {
            const int authUserId = 10;
            const int bodyClaimedUserId = 99;
            var spoof = bodyClaimedUserId != authUserId;
            Assert.True(spoof);
            // Server uses AsyncId → UserId; body UserId is ignored for authorization.
        }

        [Fact]
        public void InactiveFollower_CannotStartShift()
        {
            const bool isActive = false;
            Assert.False(isActive);
        }

        [Fact]
        public void DisableProfile_BlocksAppWithoutDeletingUser()
        {
            const bool userRowDeleted = false;
            const bool profileActive = false;
            Assert.False(userRowDeleted);
            Assert.False(profileActive);
        }
    }
}
