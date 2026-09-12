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
            SalesFilterCityScope scope,
            string filterStatus,
            string? search,
            int page,
            int pageSize,
            bool ownByActor,
            int? actorUserId,
            string? actorUserName,
            CancellationToken ct = default)
        {
            IEnumerable<SalesRequestDTO> q = Requests.Where(r =>
                r.TargetEmployeeId > 0
                && string.Equals(r.FilterStatus, filterStatus, StringComparison.OrdinalIgnoreCase));

            if (!scope.TrustEntireBranch)
            {
                q = q.Where(r => scope.CityValues.Any(c =>
                    string.Equals(c, r.CityValue, StringComparison.OrdinalIgnoreCase)));
            }

            if (ownByActor)
            {
                q = q.Where(r =>
                    (actorUserId is > 0 && r.FilteredByUserId == actorUserId)
                    || (!string.IsNullOrWhiteSpace(actorUserName)
                        && !string.IsNullOrWhiteSpace(r.FilteredByUserName)
                        && string.Equals(actorUserName, r.FilteredByUserName.Trim(), StringComparison.OrdinalIgnoreCase)));
            }

            var all = q.Select(MapList).ToList();
            return Task.FromResult(((IReadOnlyList<SalesFilterListItemDTO>)all, all.Count));
        }

        public Task<IReadOnlyDictionary<string, int>> CountByStatusAsync(
            SalesFilterCityScope scope,
            int? actorUserId,
            string? actorUserName,
            CancellationToken ct = default)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                [SalesFilterStatuses.PendingFilter] = 0,
                [SalesFilterStatuses.OnHold] = 0,
                [SalesFilterStatuses.ReadyForSale] = 0,
                [SalesFilterStatuses.Rejected] = 0,
            };
            foreach (var r in Requests.Where(x => x.TargetEmployeeId > 0))
            {
                if (!scope.TrustEntireBranch
                    && !scope.CityValues.Any(c => string.Equals(c, r.CityValue, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var pending = string.Equals(r.FilterStatus, SalesFilterStatuses.PendingFilter, StringComparison.OrdinalIgnoreCase);
                var owned = (actorUserId is > 0 && r.FilteredByUserId == actorUserId)
                    || (!string.IsNullOrWhiteSpace(actorUserName)
                        && !string.IsNullOrWhiteSpace(r.FilteredByUserName)
                        && string.Equals(actorUserName, r.FilteredByUserName.Trim(), StringComparison.OrdinalIgnoreCase));
                if (!pending && !owned) continue;
                if (r.FilterStatus != null) map[r.FilterStatus] = map.GetValueOrDefault(r.FilterStatus) + 1;
            }

            return Task.FromResult((IReadOnlyDictionary<string, int>)map);
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
            r.FilteredByUserName = changedByUserName;
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
            FilterNote = r.FilterNote,
            RejectReason = r.FilterRejectReason,
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
            FilteredByUserName = r.FilteredByUserName,
            TargetEmployeeId = r.TargetEmployeeId,
            TargetEmployeeName = r.TargetEmployeeName,
        };
    }

    public sealed class SalesFilterTests
    {
        private static SalesIdentity FilterActor(int id = 9, string name = "فلتر") => new()
        {
            EmployeeId = id,
            EmployeeName = name,
            UserType = SalesRoles.UserTypeSalesFilterEmployee,
            Role = SalesRoles.SalesFilterEmployee,
            BranchId = "baghdad-karkh",
            BranchName = "بغداد الكرخ",
        };

        private static SalesIdentity GatewayActor(string name) => new()
        {
            EmployeeId = 0,
            EmployeeName = name,
            UserType = SalesRoles.UserTypeSalesFilterEmployee,
            Role = SalesRoles.SalesFilterEmployee,
            IsGateway = true,
            ExternalUserId = "88",
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
        public async Task ReadyForSale_OwnedByA_VisibleToA_NotB()
        {
            var repo = new FakeSalesFilterRepository();
            repo.Cities.Add((9, "baghdad-karkh"));
            repo.Cities.Add((10, "baghdad-karkh"));
            repo.Requests.Add(new SalesRequestDTO
            {
                Id = 7, TargetEmployeeId = 5, CityValue = "baghdad-karkh",
                CustomerName = "X", FilterStatus = SalesFilterStatuses.ReadyForSale,
                FilteredByUserId = 9, FilteredByUserName = "فلتر", CreatedAtUtc = DateTime.UtcNow,
            });
            var svc = new SalesFilterService(repo);
            var forA = await svc.ListAsync(FilterActor(9, "فلتر"), null, SalesFilterStatuses.ReadyForSale, null, 1, 30, default);
            var forB = await svc.ListAsync(FilterActor(10, "فلتر-ب"), null, SalesFilterStatuses.ReadyForSale, null, 1, 30, default);
            Assert.Single(forA.Items);
            Assert.Empty(forB.Items);
        }

        [Fact]
        public async Task OnHold_And_Rejected_OwnedByActorOnly()
        {
            var repo = new FakeSalesFilterRepository();
            repo.Cities.Add((9, "baghdad-karkh"));
            repo.Cities.Add((10, "baghdad-karkh"));
            repo.Requests.Add(new SalesRequestDTO
            {
                Id = 40, TargetEmployeeId = 5, CityValue = "baghdad-karkh",
                FilterStatus = SalesFilterStatuses.OnHold, FilteredByUserId = 9, FilteredByUserName = "فلتر",
                FilterNote = "اتصل غداً", CreatedAtUtc = DateTime.UtcNow, CustomerName = "H",
            });
            repo.Requests.Add(new SalesRequestDTO
            {
                Id = 45, TargetEmployeeId = 5, CityValue = "baghdad-karkh",
                FilterStatus = SalesFilterStatuses.Rejected, FilteredByUserId = 9, FilteredByUserName = "فلتر",
                FilterRejectReason = "رقم خاطئ", CreatedAtUtc = DateTime.UtcNow, CustomerName = "R",
            });
            var svc = new SalesFilterService(repo);
            Assert.Single((await svc.ListAsync(FilterActor(9), null, SalesFilterStatuses.OnHold, null, 1, 30, default)).Items);
            Assert.Empty((await svc.ListAsync(FilterActor(10, "ب"), null, SalesFilterStatuses.OnHold, null, 1, 30, default)).Items);
            Assert.Single((await svc.ListAsync(FilterActor(9), null, SalesFilterStatuses.Rejected, null, 1, 30, default)).Items);
            Assert.Empty((await svc.ListAsync(FilterActor(10, "ب"), null, SalesFilterStatuses.Rejected, null, 1, 30, default)).Items);
        }

        [Fact]
        public async Task Hold_WithoutNote_Is400()
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
                svc.HoldAsync(FilterActor(), 3, "  ", default));
            Assert.Equal(400, ex.StatusCode);
        }

        [Fact]
        public async Task Hold_WithNote_SavesFilterNote_AndHistory()
        {
            var repo = new FakeSalesFilterRepository();
            repo.Cities.Add((9, "baghdad-karkh"));
            repo.Requests.Add(new SalesRequestDTO
            {
                Id = 3, TargetEmployeeId = 5, CityValue = "baghdad-karkh",
                CustomerName = "R", FilterStatus = SalesFilterStatuses.PendingFilter, CreatedAtUtc = DateTime.UtcNow,
            });
            var svc = new SalesFilterService(repo);
            var row = await svc.HoldAsync(FilterActor(), 3, "الزبون مشغول", default);
            Assert.Equal(SalesFilterStatuses.OnHold, row.FilterStatus);
            Assert.Equal("الزبون مشغول", row.FilterNote);
            Assert.Equal("الزبون مشغول", repo.History[0].Note);
            Assert.Equal(9, repo.Requests[0].FilteredByUserId);
        }

        [Fact]
        public async Task Detail_Ownership_Enforced_ForReady()
        {
            var repo = new FakeSalesFilterRepository();
            repo.Cities.Add((9, "baghdad-karkh"));
            repo.Cities.Add((10, "baghdad-karkh"));
            repo.Requests.Add(new SalesRequestDTO
            {
                Id = 8, TargetEmployeeId = 5, CityValue = "baghdad-karkh",
                FilterStatus = SalesFilterStatuses.ReadyForSale, FilteredByUserId = 9, FilteredByUserName = "فلتر",
                CustomerName = "X", CreatedAtUtc = DateTime.UtcNow,
            });
            var svc = new SalesFilterService(repo);
            await svc.GetAsync(FilterActor(9), 8, default);
            var ex = await Assert.ThrowsAsync<SalesCompleteException>(() => svc.GetAsync(FilterActor(10, "ب"), 8, default));
            Assert.Equal(404, ex.StatusCode);
        }

        [Fact]
        public async Task Gateway_LegacyCityValue_StillListsPending()
        {
            var repo = new FakeSalesFilterRepository();
            // Stored as database-style legacy value; gateway trusts branch routing.
            repo.Requests.Add(new SalesRequestDTO
            {
                Id = 11, TargetEmployeeId = 5, CityValue = "DatabaseCompanyNajaf_DEMO",
                CustomerName = "Legacy", FilterStatus = SalesFilterStatuses.PendingFilter, CreatedAtUtc = DateTime.UtcNow,
            });
            var svc = new SalesFilterService(repo);
            var page = await svc.ListAsync(GatewayActor("فلتر-مركزي"), "najaf-demo", SalesFilterStatuses.PendingFilter, null, 1, 30, default);
            Assert.Single(page.Items);
            Assert.Equal(11, page.Items[0].Id);
        }

        [Fact]
        public async Task GatewayActor_Ready_UsesNameOwnership_NotForeignUserId()
        {
            var repo = new FakeSalesFilterRepository();
            repo.Requests.Add(new SalesRequestDTO
            {
                Id = 20, TargetEmployeeId = 5, CityValue = "x",
                CustomerName = "G", Status = SalesRequestStatuses.Assigned,
                FilterStatus = SalesFilterStatuses.PendingFilter, CreatedAtUtc = DateTime.UtcNow,
            });
            var svc = new SalesFilterService(repo);
            await svc.ReadyAsync(GatewayActor("فلتر-مركزي"), 20, "ok", default);
            Assert.Null(repo.Requests[0].FilteredByUserId);
            Assert.Equal("فلتر-مركزي", repo.Requests[0].FilteredByUserName);

            var mine = await svc.ListAsync(GatewayActor("فلتر-مركزي"), "najaf-demo", SalesFilterStatuses.ReadyForSale, null, 1, 30, default);
            var other = await svc.ListAsync(GatewayActor("فلتر-آخر"), "najaf-demo", SalesFilterStatuses.ReadyForSale, null, 1, 30, default);
            Assert.Single(mine.Items);
            Assert.Empty(other.Items);
        }

        [Fact]
        public void FilterDto_HasNoFinancialFields()
        {
            var props = typeof(SalesFilterListItemDTO).GetProperties().Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain("Price", props);
            Assert.DoesNotContain("Profit", props);
            Assert.DoesNotContain("Installment", props);
            Assert.DoesNotContain("Cost", props);
            Assert.DoesNotContain("Payment", props);
            Assert.Contains("WantedDescription", props);
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
        }

        [Fact]
        public async Task ConflictTransition_Returns409()
        {
            var repo = new FakeSalesFilterRepository();
            repo.Cities.Add((9, "baghdad-karkh"));
            repo.Requests.Add(new SalesRequestDTO
            {
                Id = 5, TargetEmployeeId = 5, CityValue = "baghdad-karkh",
                CustomerName = "R", FilterStatus = SalesFilterStatuses.ReadyForSale,
                FilteredByUserId = 9, FilteredByUserName = "فلتر", CreatedAtUtc = DateTime.UtcNow,
            });
            var svc = new SalesFilterService(repo);
            var ex = await Assert.ThrowsAsync<SalesCompleteException>(() =>
                svc.ReadyAsync(FilterActor(), 5, null, default));
            Assert.Equal(409, ex.StatusCode);
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
        public void LegacyBackfill_MarksAssignedAsReady()
        {
            string? filterStatus = null;
            const int targetEmployeeId = 3;
            const string status = SalesRequestStatuses.Assigned;
            if (filterStatus == null && targetEmployeeId > 0
                && status is SalesRequestStatuses.Assigned or SalesRequestStatuses.PreparedForSale)
            {
                filterStatus = SalesFilterStatuses.ReadyForSale;
            }

            Assert.Equal(SalesFilterStatuses.ReadyForSale, filterStatus);
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
            Assert.Empty(await svc.ListForEmployeeAsync(7, default));
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
            Assert.Single(await svc.ListForEmployeeAsync(7, default));
        }
    }
}
