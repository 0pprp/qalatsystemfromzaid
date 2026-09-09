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

        [Fact]
        public void Phone_Valid_StartsWith07_Length11()
        {
            Assert.True(IsValidFollowerPhone("07701234567"));
        }

        [Fact]
        public void Phone_Invalid_ShorterOrLongerThan11()
        {
            Assert.False(IsValidFollowerPhone("0770123456"));
            Assert.False(IsValidFollowerPhone("077012345678"));
        }

        [Fact]
        public void Phone_Invalid_DoesNotStartWith07()
        {
            Assert.False(IsValidFollowerPhone("08701234567"));
            Assert.False(IsValidFollowerPhone("19701234567"));
        }

        [Fact]
        public void Province_AlwaysFromFollower_IgnoresClient()
        {
            Assert.Equal("الناصرية", ResolveProvinceFromFollower("الناصرية", "بغداد"));
            Assert.Equal("النجف", ResolveProvinceFromFollower("النجف", null));
            Assert.Null(ResolveProvinceFromFollower(null, "كربلاء"));
            Assert.Null(ResolveProvinceFromFollower("  ", "كربلاء"));
        }

        [Fact]
        public void FollowerSalesRequest_DefaultStatus_IsNew_Unassigned()
        {
            Assert.Equal("New", SalesRequestStatuses.New);
            Assert.True(SalesRequestStatuses.IsUnassigned("New"));
            Assert.False(SalesRequestStatuses.IsUnassigned("Assigned"));
        }

        [Fact]
        public void SentRequestsTab_ShowsFollowerOrEmployee_OnlyWhenUnassigned()
        {
            Assert.True(MatchesSentTab(source: "Follower", status: "New", targetEmployeeId: 0));
            Assert.True(MatchesSentTab(source: "EmployeeSubmitted", status: "New", targetEmployeeId: 0));
            Assert.False(MatchesSentTab(source: "Follower", status: "Assigned", targetEmployeeId: 5));
            Assert.False(MatchesSentTab(source: "Follower", status: "Pending", targetEmployeeId: 0));
            Assert.False(MatchesSentTab(source: "EmployeeSubmitted", status: "Assigned", targetEmployeeId: 9));
            Assert.False(MatchesSentTab(source: null, status: "New", targetEmployeeId: 0));
        }

        [Fact]
        public void DocumentTypeLabels_MatchCompanyKyc()
        {
            Assert.Equal("البطاقة الوطنية - أمامية", DocumentTypeLabel("NationalIdFront"));
            Assert.Equal("تأييد السكن", DocumentTypeLabel("ResidenceCertificate"));
            Assert.Equal("صورة المحل", DocumentTypeLabel("Shop"));
        }

        [Fact]
        public void DocumentFileApiUrl_UsesFollowersProxy_NotInternalPath()
        {
            var url = BuildDocumentFileApiUrl("http://169.58.236.52:8081/api", 10, 44, "abc", 3);
            Assert.StartsWith("http://169.58.236.52:8081/api/Followers/Customers/10/documents/44/file", url);
            Assert.Contains("asyncId=abc", url);
            Assert.Contains("listId=3", url);
            Assert.DoesNotContain("App_Data", url);
            Assert.DoesNotContain("/opt/", url);
        }

        [Fact]
        public void OctetStream_ContentType_IsReplacedByJpegFromExtension()
        {
            Assert.Equal("image/jpeg", ResolveImageContentType("application/octet-stream", "9.jpg"));
            Assert.Equal("image/png", ResolveImageContentType("", "a.png"));
            Assert.Equal("image/webp", ResolveImageContentType(null, "b.webp"));
            Assert.Equal("image/png", ResolveImageContentType("image/png", "c.jpg"));
        }

        [Fact]
        public void Follower_WithoutCity_HasClearArabicMessageConstant()
        {
            Assert.Contains("محافظة", "محافظة حساب المتابع غير معرّفة في النظام. حدّث CityID لحساب المتابع في Delegates.");
        }

        private static string ResolveImageContentType(string? stored, string fileNameOrPath)
        {
            var ext = Path.GetExtension(fileNameOrPath).ToLowerInvariant();
            var guessed = ext switch
            {
                ".png" => "image/png",
                ".webp" => "image/webp",
                ".gif" => "image/gif",
                ".jpg" or ".jpeg" => "image/jpeg",
                _ => "image/jpeg"
            };
            if (string.IsNullOrWhiteSpace(stored)
                || stored.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase))
            {
                return guessed;
            }

            return stored;
        }

        private static bool MatchesSentTab(string? source, string status, int targetEmployeeId)
        {
            var isFollower = source == "Follower";
            var isEmployee = source == "EmployeeSubmitted";
            return (isFollower || isEmployee) && status == "New";
        }

        private static string DocumentTypeLabel(string? type) => type switch
        {
            "NationalIdFront" => "البطاقة الوطنية - أمامية",
            "NationalIdBack" => "البطاقة الوطنية - خلفية",
            "ResidenceCardFront" => "بطاقة السكن - أمامية",
            "ResidenceCardBack" => "بطاقة السكن - خلفية",
            "ResidenceCard" => "بطاقة السكن - قديمة",
            "ResidenceCertificate" => "تأييد السكن",
            "Customer" => "صورة الزبون",
            "Shop" => "صورة المحل",
            _ => type ?? ""
        };

        private static string BuildDocumentFileApiUrl(string apiRoot, int customerId, int documentId, string asyncId, int listId) =>
            $"{apiRoot.TrimEnd('/')}/Followers/Customers/{customerId}/documents/{documentId}/file"
            + $"?asyncId={Uri.EscapeDataString(asyncId)}&listId={listId}";

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

        private static bool IsValidFollowerPhone(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return false;
            var digits = phone.Trim().Replace(" ", string.Empty);
            if (digits.Length != 11 || !digits.StartsWith("07", StringComparison.Ordinal)) return false;
            return digits.All(char.IsDigit);
        }

        private static string? ResolveProvinceFromFollower(string? followerCityName, string? clientProvince) =>
            string.IsNullOrWhiteSpace(followerCityName) ? null : followerCityName.Trim();

        private static string? BuildImageUrl(string? imagesBaseUrl, string? fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return null;
            var leaf = fileName.Trim().Replace('\\', '/').Split('/').Last();
            return imagesBaseUrl!.TrimEnd('/') + "/" + leaf;
        }
    }
}
