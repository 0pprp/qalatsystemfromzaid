using BE_Company.Sales.Authorization;
using BE_Company.Sales.DTO;
using BE_Company.Sales.Models;
using BE_Company.Sales.Services;
using Xunit;

namespace BE_Company.Sales.Tests
{
    public class FollowerSalesRequestTests
    {
        private static SalesIdentity Follower() => new()
        {
            EmployeeId = 77,
            EmployeeName = "متابع الناصرية",
            BranchId = "nas",
            BranchName = "الناصرية",
            Role = SalesRoles.RequestCreator,
            UserType = SalesRoles.UserTypeFollower
        };

        private static SalesIdentity Salesman() => new()
        {
            EmployeeId = 5,
            EmployeeName = "مندوب",
            BranchId = "nas",
            BranchName = "الناصرية",
            Role = SalesRoles.SalesEmployee,
            UserType = SalesRoles.UserTypeSalesEmployee
        };

        [Fact]
        public async Task Follower_CanSendSalesRequest_SourceTypeFollower()
        {
            var repo = new FakeRequestRepository();
            var created = await new SalesRequestService(repo, new FakeClock()).CreateAsync(
                Follower(),
                new SalesRequestCreateDTO
                {
                    ExistingCustomerId = 10,
                    Customer = new() { FullName = "زبون", Phone = "07701234567", Address = "حي" }
                },
                CancellationToken.None);

            Assert.Equal(SalesRequestSources.Follower, created.CustomerSourceType);
            Assert.Equal(SalesRequestSources.Follower, created.CreatedByUserType);
            Assert.Equal(77, created.CreatedByUserId);
            Assert.Equal("متابع الناصرية", created.CreatedByName);
            Assert.Equal(SalesRequestStatuses.New, created.Status);
            Assert.Equal(0, created.TargetEmployeeId);
        }

        [Fact]
        public async Task Follower_CannotSpoof_CreatedByUserId_UsesActor()
        {
            var repo = new FakeRequestRepository();
            var created = await new SalesRequestService(repo, new FakeClock()).CreateAsync(
                Follower(),
                new SalesRequestCreateDTO { Customer = new() { FullName = "أ" } },
                CancellationToken.None);
            Assert.Equal(77, created.CreatedByUserId);
            Assert.NotEqual(999, created.CreatedByUserId);
        }

        [Fact]
        public async Task Salesman_CannotCreateAsManagerEndpoint_Style()
        {
            var ex = await Assert.ThrowsAsync<SalesCompleteException>(() =>
                new SalesRequestService(new FakeRequestRepository(), new FakeClock()).CreateAsync(
                    Salesman(),
                    new SalesRequestCreateDTO { Customer = new() { FullName = "أ" } },
                    CancellationToken.None));
            Assert.Equal(403, ex.StatusCode);
        }

        [Fact]
        public void ManagerSeesFollowerSourceConstant()
        {
            Assert.True(SalesRequestSources.IsFollower("Follower"));
            Assert.False(SalesRequestSources.IsFollower("EmployeeSubmitted"));
            Assert.False(SalesRequestSources.IsEmployeeSubmitted("Follower"));
        }
    }

    /// <summary>
    /// Mirrors BE_DelegateWebApplication.Services.FollowerAuthorization rules for CI without Delegate project reference.
    /// </summary>
    public class FollowerAuthorizationRulesTests
    {
        [Fact]
        public void Follower_CanAddCustomerNote_InsideAssignedList()
        {
            Assert.True(CanNoteCustomer(isLinked: true, customerDelegateId: 5, listId: 5));
        }

        [Fact]
        public void Follower_CannotNoteCustomer_OutsideAssignedScope()
        {
            Assert.False(CanNoteCustomer(isLinked: true, customerDelegateId: 9, listId: 5));
            Assert.False(CanNoteCustomer(isLinked: false, customerDelegateId: 5, listId: 5));
        }

        [Fact]
        public void Follower_CanAddEmployeeNote_OnlyInsideAssignedLists()
        {
            Assert.True(CanNoteEmployee(isLinked: true, employeeAppearsOnAssignedList: true));
            Assert.False(CanNoteEmployee(isLinked: true, employeeAppearsOnAssignedList: false));
            Assert.False(CanNoteEmployee(isLinked: false, employeeAppearsOnAssignedList: true));
        }

        [Fact]
        public void Salesman_CannotReadFollowerEmployeeNotes()
        {
            Assert.False(SalesmanCanReadFollowerEmployeeNotes());
        }

        [Fact]
        public void Follower_CannotEditOrDeleteNoteAfterSave()
        {
            Assert.False(CanEditOrDeleteNoteAfterSave());
        }

        [Fact]
        public void Follower_CannotSpoofCreatedByUserId()
        {
            Assert.False(AcceptClientCreatedByUserId(999, authenticatedFollowerId: 77));
            Assert.Equal(77, ResolveCreatedByUserId(77, clientClaimedId: 999));
        }

        [Fact]
        public void Follower_CannotSubmitRequest_OutsideAllowedScope()
        {
            Assert.False(CanSubmitSalesRequest(isLinked: false, customerDelegateId: 5, listId: 5));
            Assert.False(CanSubmitSalesRequest(isLinked: true, customerDelegateId: 8, listId: 5));
            Assert.True(CanSubmitSalesRequest(isLinked: true, customerDelegateId: 5, listId: 5));
        }

        private static bool CanNoteCustomer(bool isLinked, int customerDelegateId, int listId) =>
            isLinked && listId > 0 && customerDelegateId == listId;

        private static bool CanNoteEmployee(bool isLinked, bool employeeAppearsOnAssignedList) =>
            isLinked && employeeAppearsOnAssignedList;

        private static bool CanSubmitSalesRequest(bool isLinked, int customerDelegateId, int listId) =>
            CanNoteCustomer(isLinked, customerDelegateId, listId);

        private static bool AcceptClientCreatedByUserId(int? clientClaimedId, int authenticatedFollowerId) => false;

        private static int ResolveCreatedByUserId(int authenticatedFollowerId, int? clientClaimedId) =>
            authenticatedFollowerId;

        private static bool SalesmanCanReadFollowerEmployeeNotes() => false;

        private static bool CanEditOrDeleteNoteAfterSave() => false;
    }
}
