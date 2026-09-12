using BE_Company.Sales.Filtering;
using BE_Company.Sales.Authorization;
using BE_Company.Sales.DTO;
using BE_Company.Sales.Models;
using BE_Company.Sales.Services;
using Xunit;

namespace BE_Company.Sales.Tests
{
    public sealed class FakeSalesFilterRepository : ISalesFilterRepository
    {
        public readonly HashSet<(int UserId, string CityValue)> Cities = new();
        public readonly List<SalesRequestDTO> Requests = [];
        public readonly List<SalesFilterHistoryDTO> History = [];
        public int NextHistoryId = 1;

        public Task EnsureSchemaAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task ReplaceUserCitiesAsync(int userId, IReadOnlyList<(string CityValue, string? CityName)> cities, CancellationToken ct = default)
        {
            Cities.RemoveWhere(c => c.UserId == userId);
            foreach (var c in cities)
                Cities.Add((userId, c.CityValue.Trim()));
            return Task.CompletedTask;
        }

        public Task ClearUserCitiesAsync(int userId, CancellationToken ct = default)
        {
            Cities.RemoveWhere(c => c.UserId == userId);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<SalesFilterCityDTO>> ListUserCitiesAsync(int userId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SalesFilterCityDTO>>(
                Cities.Where(c => c.UserId == userId)
                    .Select(c => new SalesFilterCityDTO { CityValue = c.CityValue, CityName = c.CityValue })
                    .ToList());

        public Task<bool> UserHasCityAsync(int userId, string cityValue, CancellationToken ct = default) =>
            Task.FromResult(Cities.Contains((userId, cityValue.Trim())));

        public Task<(IReadOnlyList<SalesFilterListItemDTO> Items, int Total)> ListRequestsAsync(
            IReadOnlyList<string> allowedCityValues,
            string? cityValue,
            string filterStatus,
            string? search,
            int page,
            int pageSize,
            CancellationToken ct = default)
        {
            var q = Requests.Where(r =>
                r.TargetEmployeeId > 0
                && string.Equals(r.FilterStatus, filterStatus, StringComparison.OrdinalIgnoreCase)
                && allowedCityValues.Any(c => string.Equals(c, r.CityValue, StringComparison.OrdinalIgnoreCase)));
            if (!string.IsNullOrWhiteSpace(cityValue))
                q = q.Where(r => string.Equals(r.CityValue, cityValue.Trim(), StringComparison.OrdinalIgnoreCase));
            var all = q.Select(MapList).ToList();
            return Task.FromResult(((IReadOnlyList<SalesFilterListItemDTO>)all, all.Count));
        }

        public Task<SalesFilterDetailDTO?> GetRequestAsync(int id, CancellationToken ct = default)
        {
            var r = Requests.FirstOrDefault(x => x.Id == id);
            return Task.FromResult(r == null ? null : MapDetail(r));
        }

        public Task<(bool Ok, string? CurrentStatus)> TryTransitionAsync(
            int id, string expectedStatus, string newStatus, int? userId, string? changedByUserName, string? note, string? reason, CancellationToken ct = default)
        {
            var r = Requests.FirstOrDefault(x => x.Id == id);
            if (r == null) return Task.FromResult<(bool, string?)>((false, null));
            if (!string.Equals(r.FilterStatus, expectedStatus, StringComparison.OrdinalIgnoreCase))
                return Task.FromResult<(bool, string?)>((false, r.FilterStatus));
            var previous = r.FilterStatus;
            r.FilterStatus = newStatus;
            r.FilteredByUserId = userId;
            r.FilterNote = note;
            if (newStatus == SalesFilterStatuses.Rejected) r.FilterRejectReason = reason;
            r.FilteredAtUtc = DateTime.UtcNow;
            if (newStatus == SalesFilterStatuses.ReadyForSale)
            {
                r.Status = SalesRequestStatuses.PreparedForSale;
                r.PreparedForSaleNote = note ?? "جاهز من فلترة المبيعات";
            }
            History.Add(new SalesFilterHistoryDTO
            {
                Id = NextHistoryId++,
                SaleRequestId = id,
                PreviousStatus = previous,
                NewStatus = newStatus,
                ChangedByUserId = userId,
                ChangedByUserName = changedByUserName,
                Note = note,
                Reason = reason,
                ChangedAtUtc = DateTime.UtcNow,
            });
            return Task.FromResult<(bool, string?)>((true, newStatus));
        }

        public Task<IReadOnlyList<SalesFilterHistoryDTO>> ListHistoryAsync(int saleRequestId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SalesFilterHistoryDTO>>(History.Where(h => h.SaleRequestId == saleRequestId).ToList());

        private static SalesFilterListItemDTO MapList(SalesRequestDTO r) => new()
        {
            Id = r.Id,
            CustomerName = r.CustomerName,
            CustomerPhone = r.CustomerPhone,
            CityValue = r.CityValue,
            CityName = r.CityName,
            CustomerProvince = r.CustomerProvince,
            CustomerAddress = r.CustomerAddress,
            WantedDescription = r.Notes,
            FilterStatus = r.FilterStatus ?? SalesFilterStatuses.PendingFilter,
            CreatedAtUtc = r.CreatedAtUtc,
            FilteredAtUtc = r.FilteredAtUtc,
        };

        private static SalesFilterDetailDTO MapDetail(SalesRequestDTO r) => new()
        {
            Id = r.Id,
            CustomerName = r.CustomerName,
            CustomerPhone = r.CustomerPhone,
            CityValue = r.CityValue,
            CityName = r.CityName,
            CustomerProvince = r.CustomerProvince,
            CustomerAddress = r.CustomerAddress,
            WantedDescription = r.Notes,
            FilterStatus = r.FilterStatus ?? SalesFilterStatuses.PendingFilter,
            CreatedAtUtc = r.CreatedAtUtc,
            FilteredAtUtc = r.FilteredAtUtc,
            FilterNote = r.FilterNote,
            RejectReason = r.FilterRejectReason,
            FilteredByUserId = r.FilteredByUserId,
            TargetEmployeeId = r.TargetEmployeeId,
            TargetEmployeeName = r.TargetEmployeeName,
        };
    }

    public sealed class SalesFilterTests
    {
        private static SalesIdentity FilterActor(int id = 9) => new()
        {
            EmployeeId = id,
            EmployeeName = "فلتر",
            UserType = SalesRoles.UserTypeSalesFilterEmployee,
            Role = SalesRoles.SalesFilterEmployee,
            BranchId = "baghdad-karkh",
            BranchName = "بغداد الكرخ",
        };

        [Fact]
        public async Task FilterEmployee_SeesOnlyAllowedCity()
        {
            var repo = new FakeSalesFilterRepository();
            repo.Cities.Add((9, "baghdad-karkh"));
            repo.Requests.Add(new SalesRequestDTO
            {
                Id = 1, TargetEmployeeId = 5, CityValue = "baghdad-karkh",
                CustomerName = "A", FilterStatus = SalesFilterStatuses.PendingFilter, CreatedAtUtc = DateTime.UtcNow,
            });
            repo.Requests.Add(new SalesRequestDTO
            {
                Id = 2, TargetEmployeeId = 5, CityValue = "najaf",
                CustomerName = "B", FilterStatus = SalesFilterStatuses.PendingFilter, CreatedAtUtc = DateTime.UtcNow,
            });
            var svc = new SalesFilterService(repo);
            var page = await svc.ListAsync(FilterActor(), null, SalesFilterStatuses.PendingFilter, null, 1, 30, default);
            Assert.Single(page.Items);
            Assert.Equal(1, page.Items[0].Id);
        }

        [Fact]
        public async Task FilterEmployee_ForbiddenCity_Throws403()
        {
            var repo = new FakeSalesFilterRepository();
            repo.Cities.Add((9, "baghdad-karkh"));
            var svc = new SalesFilterService(repo);
            var ex = await Assert.ThrowsAsync<SalesCompleteException>(() =>
                svc.ListAsync(FilterActor(), "najaf", SalesFilterStatuses.PendingFilter, null, 1, 30, default));
            Assert.Equal(403, ex.StatusCode);
        }

        [Fact]
        public async Task PendingFilter_NotVisibleToSalesEmployee()
        {
            var requests = new FakeRequestRepository();
            requests.Rows.Add(new SalesRequestDTO
            {
                Id = 1, TargetEmployeeId = 7, CityValue = "x",
                CustomerName = "C", Status = SalesRequestStatuses.Assigned,
                FilterStatus = SalesFilterStatuses.PendingFilter, CreatedAtUtc = DateTime.UtcNow,
            });
            var svc = new SalesRequestService(requests, new FakeClock { UtcNow = DateTime.UtcNow });
            var list = await svc.ListForEmployeeAsync(7, default);
            Assert.Empty(list);
        }

        [Fact]
        public async Task ReadyForSale_VisibleToSalesEmployee()
        {
            var requests = new FakeRequestRepository();
            requests.Rows.Add(new SalesRequestDTO
            {
                Id = 1, TargetEmployeeId = 7, CityValue = "x",
                CustomerName = "C", Status = SalesRequestStatuses.PreparedForSale,
                FilterStatus = SalesFilterStatuses.ReadyForSale, CreatedAtUtc = DateTime.UtcNow,
            });
            var svc = new SalesRequestService(requests, new FakeClock { UtcNow = DateTime.UtcNow });
            var list = await svc.ListForEmployeeAsync(7, default);
            Assert.Single(list);
        }

        [Fact]
        public async Task Reject_WithoutReason_Is400()
        {
            var repo = new FakeSalesFilterRepository();
            repo.Cities.Add((9, "baghdad-karkh"));
            repo.Requests.Add(new SalesRequestDTO
            {
                Id = 3, TargetEmployeeId = 5, CityValue = "baghdad-karkh",
                CustomerName = "R", FilterStatus = SalesFilterStatuses.PendingFilter, CreatedAtUtc = DateTime.UtcNow,
            });
            var svc = new SalesFilterService(repo);
            var ex = await Assert.ThrowsAsync<SalesCompleteException>(() =>
                svc.RejectAsync(FilterActor(), 3, "  ", null, default));
            Assert.Equal(400, ex.StatusCode);
        }

        [Fact]
        public async Task Reject_WithReason_SavesAndHistory()
        {
            var repo = new FakeSalesFilterRepository();
            repo.Cities.Add((9, "baghdad-karkh"));
            repo.Requests.Add(new SalesRequestDTO
            {
                Id = 3, TargetEmployeeId = 5, CityValue = "baghdad-karkh",
                CustomerName = "R", FilterStatus = SalesFilterStatuses.PendingFilter, CreatedAtUtc = DateTime.UtcNow,
            });
            var svc = new SalesFilterService(repo);
            var row = await svc.RejectAsync(FilterActor(), 3, "رقم وهمي", null, default);
            Assert.Equal(SalesFilterStatuses.Rejected, row.FilterStatus);
            Assert.Equal("رقم وهمي", row.RejectReason);
            Assert.Single(repo.History);
            Assert.Equal(SalesFilterStatuses.Rejected, repo.History[0].NewStatus);
        }

        [Fact]
        public async Task Ready_WritesHistory_AndPreparedStatus()
        {
            var repo = new FakeSalesFilterRepository();
            repo.Cities.Add((9, "baghdad-karkh"));
            repo.Requests.Add(new SalesRequestDTO
            {
                Id = 4, TargetEmployeeId = 5, CityValue = "baghdad-karkh",
                CustomerName = "R", Status = SalesRequestStatuses.Assigned,
                FilterStatus = SalesFilterStatuses.PendingFilter, CreatedAtUtc = DateTime.UtcNow,
            });
            var svc = new SalesFilterService(repo);
            await svc.ReadyAsync(FilterActor(), 4, "تم الاتصال", default);
            Assert.Equal(SalesFilterStatuses.ReadyForSale, repo.Requests[0].FilterStatus);
            Assert.Equal(SalesRequestStatuses.PreparedForSale, repo.Requests[0].Status);
            Assert.Single(repo.History);
        }

        [Fact]
        public async Task UnauthorizedUser_CannotList()
        {
            var svc = new SalesFilterService(new FakeSalesFilterRepository());
            var actor = new SalesIdentity
            {
                EmployeeId = 1,
                UserType = SalesRoles.UserTypeSalesEmployee,
                Role = SalesRoles.SalesEmployee,
            };
            var ex = await Assert.ThrowsAsync<SalesCompleteException>(() =>
                svc.ListAsync(actor, null, null, null, 1, 30, default));
            Assert.Equal(403, ex.StatusCode);
        }

        [Fact]
        public async Task ConflictTransition_Returns409()
        {
            var repo = new FakeSalesFilterRepository();
            repo.Cities.Add((9, "baghdad-karkh"));
            repo.Requests.Add(new SalesRequestDTO
            {
                Id = 5, TargetEmployeeId = 5, CityValue = "baghdad-karkh",
                CustomerName = "R", FilterStatus = SalesFilterStatuses.ReadyForSale, CreatedAtUtc = DateTime.UtcNow,
            });
            var svc = new SalesFilterService(repo);
            var ex = await Assert.ThrowsAsync<SalesCompleteException>(() =>
                svc.ReadyAsync(FilterActor(), 5, null, default));
            Assert.Equal(409, ex.StatusCode);
        }

        [Fact]
        public void LegacyBackfill_MarksAssignedAsReady()
        {
            // Documents the migration decision used in EnsureSchema / SQL migration.
            string? filterStatus = null;
            const int targetEmployeeId = 3;
            const string status = SalesRequestStatuses.Assigned;
            if (filterStatus == null && targetEmployeeId > 0
                && status is SalesRequestStatuses.Assigned or SalesRequestStatuses.PreparedForSale)
            {
                filterStatus = SalesFilterStatuses.ReadyForSale;
            }

            Assert.Equal(SalesFilterStatuses.ReadyForSale, filterStatus);
            Assert.True(SalesFilterStatuses.IsVisibleToSalesEmployee(filterStatus));
        }

        [Fact]
        public async Task Assign_SetsPendingFilter()
        {
            var requests = new FakeRequestRepository();
            requests.Rows.Add(new SalesRequestDTO
            {
                Id = 10, TargetEmployeeId = 0, Status = SalesRequestStatuses.New,
                CustomerName = "N", CreatedAtUtc = DateTime.UtcNow,
                FilterStatus = SalesFilterStatuses.PendingFilter,
            });
            var svc = new SalesRequestService(requests, new FakeClock { UtcNow = DateTime.UtcNow });
            var manager = new SalesIdentity
            {
                EmployeeId = 1,
                EmployeeName = "مدير",
                UserType = SalesRoles.UserTypeSalesManager,
                Role = SalesRoles.SalesManager,
            };
            var updated = await svc.AssignAsync(manager, 10, new SalesRequestAssignDTO
            {
                EmployeeId = 77,
                EmployeeName = "موظف",
                CityValue = "baghdad-karkh",
                CityName = "بغداد الكرخ",
            }, default);
            Assert.Equal(SalesFilterStatuses.PendingFilter, updated.FilterStatus);
            Assert.Equal(77, updated.TargetEmployeeId);
        }

        [Fact]
        public async Task GatewayFilter_Ready_KeepsLocalUserIdNull_AndStoresName()
        {
            var repo = new FakeSalesFilterRepository();
            repo.Requests.Add(new SalesRequestDTO
            {
                Id = 20, TargetEmployeeId = 5, CityValue = "najaf",
                CustomerName = "G", Status = SalesRequestStatuses.Assigned,
                FilterStatus = SalesFilterStatuses.PendingFilter, CreatedAtUtc = DateTime.UtcNow,
            });
            var svc = new SalesFilterService(repo);
            var actor = new SalesIdentity
            {
                EmployeeId = 0,
                EmployeeName = "فلتر-مركزي",
                UserType = SalesRoles.UserTypeSalesFilterEmployee,
                Role = SalesRoles.SalesFilterEmployee,
                IsGateway = true,
                ExternalUserId = "999",
            };
            await svc.ReadyAsync(actor, 20, "ok", default);
            Assert.Null(repo.Requests[0].FilteredByUserId);
            Assert.Null(repo.History[0].ChangedByUserId);
            Assert.Equal("فلتر-مركزي", repo.History[0].ChangedByUserName);
            Assert.Equal(SalesFilterStatuses.ReadyForSale, repo.Requests[0].FilterStatus);
        }

        [Fact]
        public async Task GatewayFilter_ListRequiresCity()
        {
            var svc = new SalesFilterService(new FakeSalesFilterRepository());
            var actor = new SalesIdentity
            {
                EmployeeId = 0,
                EmployeeName = "فلتر",
                UserType = SalesRoles.UserTypeSalesFilterEmployee,
                Role = SalesRoles.SalesFilterEmployee,
                IsGateway = true,
            };
            var ex = await Assert.ThrowsAsync<SalesCompleteException>(() =>
                svc.ListAsync(actor, null, SalesFilterStatuses.PendingFilter, null, 1, 30, default));
            Assert.Equal(400, ex.StatusCode);
        }
    }
}
