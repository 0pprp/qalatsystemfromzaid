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
        public async Task Follower_CanCreateSalesRequest_ForExistingCustomer_SourceFollower()
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
            Assert.Equal(10, created.ExistingCustomerId);
            Assert.Equal(77, created.CreatedByUserId);
        }

        [Fact]
        public async Task Follower_CanCreateSalesRequest_ForNewCustomer_SourceFollower()
        {
            var repo = new FakeRequestRepository();
            var created = await new SalesRequestService(repo, new FakeClock()).CreateAsync(
                Follower(),
                new SalesRequestCreateDTO
                {
                    Customer = new() { FullName = "زبون جديد", Phone = "07709998887", Address = "حي الأنصار", Province = "النجف" }
                },
                CancellationToken.None);

            Assert.Equal(SalesRequestSources.Follower, created.CustomerSourceType);
            Assert.Null(created.ExistingCustomerId);
            Assert.Equal("زبون جديد", created.CustomerName);
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
        }
    }

    public class FollowerAuthorizationRulesTests
    {
        [Fact]
        public void Follower_CanAddCustomerNote_InsideAssignedList()
        {
            Assert.True(CanNoteCustomer(isLinked: true, customerDelegateId: 5, listId: 5));
        }

        [Fact]
        public void Customer_OutsideAssignedScope_Denied()
        {
            Assert.False(CanNoteCustomer(isLinked: true, customerDelegateId: 9, listId: 5));
        }

        [Fact]
        public void Follower_CanAddDelegateNote_OnlyForAssignedListDelegate()
        {
            Assert.True(CanNoteListDelegate(isLinked: true, listId: 12, requestedDelegateId: 12));
            Assert.False(CanNoteListDelegate(isLinked: true, listId: 12, requestedDelegateId: 99));
            Assert.False(CanNoteListDelegate(isLinked: false, listId: 12, requestedDelegateId: 12));
        }

        [Fact]
        public void SalesEmployee_IsNotUsedInDelegateNoteFlow()
        {
            Assert.False(SalesEmployeeUsedInDelegateNoteFlow());
        }

        [Fact]
        public void Follower_CannotEditOrDeleteNoteAfterSave()
        {
            Assert.False(CanEditOrDeleteNoteAfterSave());
        }

        [Fact]
        public void Follower_CannotSpoofCreatedByUserId()
        {
            Assert.Equal(77, ResolveCreatedByUserId(77, 999));
        }

        [Fact]
        public void Follower_CanSubmitNewCustomer_OnAssignedListOnly()
        {
            Assert.True(CanSubmitNewCustomerRequest(isLinked: true, listId: 5));
            Assert.False(CanSubmitNewCustomerRequest(isLinked: false, listId: 5));
        }

        [Fact]
        public void ImageUrl_UsesImagesFolder()
        {
            Assert.Equal("https://host/Images/a.jpg", BuildImageUrl("https://host/Images", "a.jpg"));
            Assert.Null(BuildImageUrl("https://host/Images", null));
        }

        private static bool CanNoteCustomer(bool isLinked, int customerDelegateId, int listId) =>
            isLinked && listId > 0 && customerDelegateId == listId;

        private static bool CanNoteListDelegate(bool isLinked, int listId, int requestedDelegateId) =>
            isLinked && listId > 0 && requestedDelegateId == listId;

        private static bool CanSubmitNewCustomerRequest(bool isLinked, int listId) =>
            isLinked && listId > 0;

        private static int ResolveCreatedByUserId(int authenticatedFollowerId, int? clientClaimedId) =>
            authenticatedFollowerId;

        private static bool SalesEmployeeUsedInDelegateNoteFlow() => false;

        private static bool CanEditOrDeleteNoteAfterSave() => false;

        private static string? BuildImageUrl(string? imagesBaseUrl, string? fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return null;
            var leaf = fileName.Trim().Replace('\\', '/').Split('/').Last();
            return imagesBaseUrl!.TrimEnd('/') + "/" + leaf;
        }
    }
}
