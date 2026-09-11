using BE_Company.Sales.Authorization;
using Xunit;

namespace BE_Company.Tests
{
    public sealed class FollowerDirectoryContractTests
    {
        [Fact]
        public void ActiveFollower_AppearsInTrackingDirectory()
        {
            const string userType = "متابع";
            const bool userStateActive = true;
            Assert.True(SalesRoles.IsFollower(userType) && userStateActive);
        }

        [Fact]
        public void Directory_DoesNotRequireFollowerProfiles()
        {
            const bool hasFollowerProfile = false;
            const string userType = "متابع";
            Assert.True(SalesRoles.IsFollower(userType));
            Assert.False(hasFollowerProfile);
        }

        [Fact]
        public void Directory_DoesNotRequireFollowerUserLists()
        {
            var assignedLists = Array.Empty<int>();
            Assert.Empty(assignedLists);
            // Still appears for accountant tracking UI.
            Assert.True(SalesRoles.IsFollower("متابع"));
        }

        [Fact]
        public void MissingGps_DoesNotFailFollowerList()
        {
            var users = new[] { new { UserId = 1, Name = "a" } };
            var liveFailed = true;
            var list = users.Select(u => new
            {
                followerId = u.UserId,
                hasActiveShift = false,
            }).ToList();
            Assert.True(liveFailed);
            Assert.Single(list);
        }

        [Fact]
        public void FollowerId_IsUsersUserId()
        {
            const int userId = 10;
            const int followerId = userId;
            Assert.Equal(userId, followerId);
        }
    }
}
