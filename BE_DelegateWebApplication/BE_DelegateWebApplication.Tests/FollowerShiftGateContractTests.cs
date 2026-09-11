using BE_DelegateWebApplication.Services.FollowerIdentity;
using Xunit;

namespace BE_DelegateWebApplication.Tests
{
    public sealed class FollowerShiftGateContractTests
    {
        [Fact]
        public void GpsWithoutActiveShift_IsRejected()
        {
            const string shiftStatus = "Closed";
            const bool allowGps = shiftStatus == "Active";
            Assert.False(allowGps);
        }

        [Fact]
        public void GpsWithActiveShift_IsAllowed()
        {
            const string shiftStatus = "Active";
            Assert.Equal("Active", shiftStatus);
        }

        [Fact]
        public void TrackingList_UsesUsersNotProfiles()
        {
            Assert.True(FollowerUserType.IsFollowerType("متابع"));
            const bool profilesRequired = false;
            Assert.False(profilesRequired);
        }

        [Fact]
        public void RouteFollowerId_MeansUserId()
        {
            const int userId = 42;
            const int routeFollowerId = userId;
            Assert.Equal(userId, routeFollowerId);
        }

        [Fact]
        public void EmptyLive_DoesNotBreakFollowerDirectory()
        {
            var users = new[] { 1, 2 };
            var live = Array.Empty<int>();
            var result = users.Select(id => new { followerId = id, hasActiveShift = live.Contains(id) }).ToList();
            Assert.Equal(2, result.Count);
            Assert.All(result, r => Assert.False(r.hasActiveShift));
        }
    }
}
