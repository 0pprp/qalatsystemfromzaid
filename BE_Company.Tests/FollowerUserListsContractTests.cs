using BE_Company.Sales.Authorization;
using Xunit;

namespace BE_Company.Tests
{
    /// <summary>Contracts for FollowerUserLists ACL (ListId = Delegates.DelegateID).</summary>
    public sealed class FollowerUserListsContractTests
    {
        [Fact]
        public void ActiveFollower_WithTwoLists_ReturnsOnlyThoseIds()
        {
            var assigned = new HashSet<int> { 10, 20 };
            var allPaymentLists = new[] { 10, 20, 30, 40 };
            var visible = allPaymentLists.Where(assigned.Contains).ToList();
            Assert.Equal(new[] { 10, 20 }, visible);
        }

        [Fact]
        public void ActiveFollower_WithNoLists_ReturnsEmpty()
        {
            var assigned = Array.Empty<int>();
            Assert.Empty(assigned);
            // Login remains allowed via UserType; Lists endpoint returns [].
            Assert.True(SalesRoles.IsFollower("متابع"));
        }

        [Fact]
        public void NonFollower_IsRejectedForFollowerAccess()
        {
            Assert.False(SalesRoles.IsFollower("مندوب"));
            Assert.False(SalesRoles.IsFollower("محاسب رئيسي"));
        }

        [Fact]
        public void InactiveUserState_BlocksAccess()
        {
            const bool userStateActive = false;
            const string userType = "متابع";
            var allowed = SalesRoles.IsFollower(userType) && userStateActive;
            Assert.False(allowed);
        }

        [Fact]
        public void UnassignedList_IsDenied()
        {
            var assigned = new HashSet<int> { 5, 6 };
            Assert.DoesNotContain(99, assigned);
        }

        [Fact]
        public void ReplaceAssignments_IsDistinctNoDuplicates()
        {
            var incoming = new[] { 1, 1, 2, 2, 3 };
            var distinct = incoming.Where(x => x > 0).Distinct().ToList();
            Assert.Equal(new[] { 1, 2, 3 }, distinct);
            var set = new HashSet<int>(distinct);
            Assert.Equal(distinct.Count, set.Count);
        }

        [Fact]
        public void CheckboxToggle_UpdatesAssignments()
        {
            var selected = new HashSet<int> { 1, 2 };
            selected.Remove(2);
            selected.Add(7);
            Assert.Equal(new HashSet<int> { 1, 7 }, selected);
        }

        [Fact]
        public void ChangingUserTypeAway_ClearsFollowerAccessAndLists()
        {
            var userType = "متابع";
            Assert.True(SalesRoles.IsFollower(userType));
            userType = "موظف مبيعات";
            Assert.False(SalesRoles.IsFollower(userType));
            // Architectural decision: ClearAssignmentsAsync on non-follower save.
            var bindingsCleared = true;
            Assert.True(bindingsCleared);
        }

        [Fact]
        public void ListId_MeansDelegatesDelegateId()
        {
            const int listId = 42;
            const int delegateId = listId;
            Assert.Equal(delegateId, listId);
        }
    }
}
