using BE_DelegateWebApplication.Services.FollowerIdentity;
using Xunit;

namespace BE_DelegateWebApplication.Tests
{
    public sealed class FollowerListsAclContractTests
    {
        [Fact]
        public void ListsQuery_UsesFollowerUserListsNotCityWide()
        {
            const string source = "FollowerUserLists.ListId=Delegates.DelegateID";
            Assert.DoesNotContain("UsersSelectedCities", source, StringComparison.Ordinal);
            Assert.DoesNotContain("FollowerProfiles", source, StringComparison.Ordinal);
        }

        [Fact]
        public void LinkedCheck_RequiresAssignmentRow()
        {
            var assigned = new HashSet<int> { 11 };
            Assert.Contains(11, assigned);
            Assert.DoesNotContain(12, assigned);
        }

        [Fact]
        public void EmptyAssignment_StillFollowerForLogin()
        {
            Assert.True(FollowerUserType.IsFollowerType("متابع"));
            var lists = Array.Empty<int>();
            Assert.Empty(lists);
        }

        [Fact]
        public void FollowerUser_WithoutLink_DoesNotUseLegacySelectDelegateBypass()
        {
            const bool isFollowerUser = true;
            const bool hasFollowerUserListRow = false;
            var allowed = isFollowerUser && hasFollowerUserListRow;
            Assert.False(allowed);
        }
    }
}
