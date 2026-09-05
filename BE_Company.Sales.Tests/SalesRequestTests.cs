using System.Reflection;
using BE_Company.Sales.Authorization;
using BE_Company.Sales.Controllers;
using BE_Company.Sales.DTO;
using BE_Company.Sales.Models;
using BE_Company.Sales.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BE_Company.Sales.Tests
{
    public sealed class FakeRequestRepository : ISalesRequestRepository
    {
        public readonly List<SalesRequestDTO> Rows = [];
        public readonly List<SalesRequestHistoryDTO> History = [];
        public int NextId = 1;
        public int NextHistoryId = 1;

        public Task EnsureSchemaAsync(CancellationToken ct) => Task.CompletedTask;

        public Task<SalesRequestDTO> InsertAsync(SalesRequestDTO row, CancellationToken ct)
        {
            row.Id = NextId++;
            Rows.Add(row);
            return Task.FromResult(row);
        }

        public Task<SalesRequestDTO?> GetByIdAsync(int id, CancellationToken ct) =>
            Task.FromResult(Rows.FirstOrDefault(r => r.Id == id));

        public Task<IReadOnlyList<SalesRequestDTO>> ListAsync(int? targetEmployeeId, string? status, DateTime? fromUtc, DateTime? toUtc, CancellationToken ct)
        {
            IEnumerable<SalesRequestDTO> q = Rows;
            if (targetEmployeeId != null) q = q.Where(r => r.TargetEmployeeId == targetEmployeeId);
            if (!string.IsNullOrWhiteSpace(status)) q = q.Where(r => r.Status == status);
            if (fromUtc != null) q = q.Where(r => r.CreatedAtUtc >= fromUtc);
            if (toUtc != null) q = q.Where(r => r.CreatedAtUtc <= toUtc);
            return Task.FromResult<IReadOnlyList<SalesRequestDTO>>(q.ToList());
        }

        public Task UpdateAsync(SalesRequestDTO row, CancellationToken ct) => Task.CompletedTask;

        public Task<int> CountByStatusAsync(string status, CancellationToken ct) =>
            Task.FromResult(Rows.Count(r => r.Status == status));

        public Task InsertHistoryAsync(SalesRequestHistoryDTO row, CancellationToken ct)
        {
            row.Id = NextHistoryId++;
            History.Add(row);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<SalesRequestHistoryDTO>> ListHistoryAsync(int requestId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<SalesRequestHistoryDTO>>(History.Where(h => h.RequestId == requestId).ToList());
    }

    public sealed class FakeManagerRead : ISalesManagerReadRepository
    {
        public List<SalesManagerEmployeeRow> Employees { get; } = [new() { EmployeeId = 1, EmployeeName = "أحمد" }, new() { EmployeeId = 2, EmployeeName = "علي" }];
        public Dictionary<int, SalesManagerLocationPointDTO> Points { get; } = [];
        public Dictionary<int, SalesManagerTrackingEventDTO> Events { get; } = [];
        public List<SalesShiftDTO> Shifts { get; } = [];
        public List<SalesDraftDTO> Sales { get; } = [];
        public List<SalesManagerRoutePointDTO> Route { get; } = [];

        public Task<IReadOnlyList<SalesManagerEmployeeRow>> ListEmployeesAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<SalesManagerEmployeeRow>>(Employees);

        public Task<SalesManagerLocationPointDTO?> GetLatestPointAsync(int employeeId, CancellationToken ct) =>
            Task.FromResult(Points.GetValueOrDefault(employeeId));

        public Task<SalesManagerTrackingEventDTO?> GetLatestEventAsync(int employeeId, CancellationToken ct) =>
            Task.FromResult(Events.GetValueOrDefault(employeeId));

        public Task<SalesShiftDTO?> GetShiftForBusinessDateAsync(int employeeId, DateTime businessDateIraq, CancellationToken ct) =>
            Task.FromResult(Shifts.Where(s => s.EmployeeId == employeeId)
                .OrderByDescending(s => s.StartedAtUtc)
                .FirstOrDefault(s => IraqTimeService.BusinessDateIraq(s.StartedAtIraq).Date == businessDateIraq.Date)
                ?? Shifts.FirstOrDefault(s => s.EmployeeId == employeeId && s.Status == SalesShiftStatuses.Active));

        public Task<IReadOnlyList<SalesManagerRoutePointDTO>> GetRouteAsync(int employeeId, DateTime fromUtc, DateTime toUtc, int maxPoints, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<SalesManagerRoutePointDTO>>(
                Route.Where(p => p.CapturedAt >= fromUtc && p.CapturedAt <= toUtc).OrderBy(p => p.CapturedAt).Take(maxPoints).ToList());

        public Task<int> CountRouteAsync(int employeeId, DateTime fromUtc, DateTime toUtc, CancellationToken ct) =>
            Task.FromResult(Route.Count(p => p.CapturedAt >= fromUtc && p.CapturedAt <= toUtc));

        public Task<IReadOnlyList<SalesManagerTrackingEventDTO>> GetEventsAsync(int employeeId, DateTime fromUtc, DateTime toUtc, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<SalesManagerTrackingEventDTO>>(
                Events.Values.Where(e => e.OccurredAt >= fromUtc && e.OccurredAt <= toUtc).ToList());

        public Task<IReadOnlyList<SalesDraftDTO>> ListSalesAsync(int? employeeId, string? status, DateTime? fromUtc, DateTime? toUtc, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<SalesDraftDTO>>(Sales);

        public Task<SalesDraftDTO?> GetSaleAsync(int saleId, CancellationToken ct) =>
            Task.FromResult(Sales.FirstOrDefault(s => s.SaleId == saleId));

        public Task<int> CountSalesTodayAsync(int employeeId, DateTime fromUtc, DateTime toUtc, CancellationToken ct) =>
            Task.FromResult(Sales.Count(s => s.EmployeeId == employeeId));

        public Task<int> CountPendingSalesAsync(int? employeeId, CancellationToken ct) =>
            Task.FromResult(Sales.Count(s => s.Status == SalesStatuses.Pending && (employeeId == null || s.EmployeeId == employeeId)));
    }

    public class SalesRequestTests
    {
        private static SalesIdentity Manager() => new()
        {
            EmployeeId = 90,
            EmployeeName = "مدير",
            BranchId = "najaf-demo",
            BranchName = "النجف",
            Role = SalesRoles.SalesManager,
            UserType = SalesRoles.UserTypeSalesManager
        };

        private static SalesIdentity Employee(int id) => new()
        {
            EmployeeId = id,
            EmployeeName = "موظف",
            BranchId = "najaf-demo",
            BranchName = "النجف",
            Role = SalesRoles.SalesEmployee,
            UserType = SalesRoles.UserTypeSalesEmployee
        };

        private static SalesIdentity BranchManager() => new()
        {
            EmployeeId = 12,
            EmployeeName = "مدير فرع",
            BranchId = "najaf-demo",
            BranchName = "النجف",
            Role = SalesRoles.RequestCreator,
            UserType = SalesRoles.UserTypeBranchManager
        };

        private static SalesRequestService Svc(FakeRequestRepository repo, FakeClock? clock = null) =>
            new(repo, clock ?? new FakeClock());

        private static async Task<SalesRequestDTO> AssignedAsync(SalesRequestService svc, int employeeId = 1, string name = "أ")
        {
            var created = await svc.CreateAsync(Manager(), new() { Customer = new() { FullName = name } }, CancellationToken.None);
            return await svc.AssignAsync(Manager(), created.Id, new SalesRequestAssignDTO { EmployeeId = employeeId }, CancellationToken.None);
        }

        [Fact]
        public async Task Manager_CreatesRequest_Unassigned()
        {
            var repo = new FakeRequestRepository();
            var created = await Svc(repo).CreateAsync(Manager(), new SalesRequestCreateDTO
            {
                TargetEmployeeId = 45,
                Customer = new SalesRequestCustomerDTO { FullName = "زبون", Phone = "0770" }
            }, CancellationToken.None);
            Assert.Equal(SalesRequestStatuses.New, created.Status);
            Assert.Equal(0, created.TargetEmployeeId);
            Assert.Contains(created.History, h => h.Event == SalesRequestEvents.Created);
        }

        [Fact]
        public async Task BranchManager_CreatesRequest_Unassigned()
        {
            var created = await Svc(new FakeRequestRepository()).CreateAsync(BranchManager(), new SalesRequestCreateDTO
            {
                TargetEmployeeId = 9,
                Customer = new() { FullName = "زبون" }
            }, CancellationToken.None);
            Assert.Equal(SalesRequestStatuses.New, created.Status);
            Assert.Equal(0, created.TargetEmployeeId);
        }

        [Fact]
        public async Task SalesEmployee_CannotCreateManagerRequest()
        {
            var ex = await Assert.ThrowsAsync<SalesCompleteException>(() => Svc(new FakeRequestRepository()).CreateAsync(
                Employee(1), new SalesRequestCreateDTO { TargetEmployeeId = 1, Customer = new() { FullName = "أ" } }, CancellationToken.None));
            Assert.Equal(403, ex.StatusCode);
        }

        [Fact]
        public async Task EmployeeA_SeesOnlyAssigned()
        {
            var repo = new FakeRequestRepository();
            var svc = Svc(repo);
            await AssignedAsync(svc, 1, "أ");
            await AssignedAsync(svc, 2, "ب");
            var mine = await svc.ListForEmployeeAsync(1, CancellationToken.None);
            Assert.Single(mine);
            Assert.Equal(1, mine[0].TargetEmployeeId);
            Assert.Equal(SalesRequestStatuses.Assigned, mine[0].Status);
        }

        [Fact]
        public async Task Employee_DoesNotSeeUnassigned()
        {
            var repo = new FakeRequestRepository();
            var svc = Svc(repo);
            await svc.CreateAsync(Manager(), new() { Customer = new() { FullName = "أ" } }, CancellationToken.None);
            var mine = await svc.ListForEmployeeAsync(1, CancellationToken.None);
            Assert.Empty(mine);
        }

        [Fact]
        public async Task EmployeeB_CannotReadARequest()
        {
            var repo = new FakeRequestRepository();
            var svc = Svc(repo);
            var created = await AssignedAsync(svc, 1);
            var ex = await Assert.ThrowsAsync<SalesCompleteException>(() => svc.GetForEmployeeAsync(created.Id, 2, CancellationToken.None));
            Assert.Equal(403, ex.StatusCode);
        }

        [Fact]
        public async Task View_IsIdempotent()
        {
            var repo = new FakeRequestRepository();
            var svc = Svc(repo);
            var created = await AssignedAsync(svc, 1);
            var first = await svc.ViewAsync(created.Id, 1, CancellationToken.None);
            var second = await svc.ViewAsync(created.Id, 1, CancellationToken.None);
            Assert.Equal(SalesRequestStatuses.Assigned, first.Status);
            Assert.Equal(SalesRequestStatuses.Assigned, second.Status);
            Assert.Equal(first.ViewedAtUtc, second.ViewedAtUtc);
        }

        [Fact]
        public async Task StartProcessing_PreparesForSale()
        {
            var repo = new FakeRequestRepository();
            var svc = Svc(repo);
            var created = await AssignedAsync(svc, 1);
            var started = await svc.StartProcessingAsync(created.Id, 1, CancellationToken.None);
            Assert.Equal(SalesRequestStatuses.PreparedForSale, started.Status);
        }

        [Fact]
        public async Task Pending_RequiresNote_KeepsEmployee()
        {
            var repo = new FakeRequestRepository();
            var svc = Svc(repo);
            var created = await AssignedAsync(svc, 1);
            var ex = await Assert.ThrowsAsync<SalesCompleteException>(() => svc.PendAsync(created.Id, 1, "  ", CancellationToken.None));
            Assert.Equal(400, ex.StatusCode);
            var pending = await svc.PendAsync(created.Id, 1, "بانتظار الزبون", CancellationToken.None);
            Assert.Equal(SalesRequestStatuses.Pending, pending.Status);
            Assert.Equal(1, pending.TargetEmployeeId);
            Assert.Equal("بانتظار الزبون", pending.PendingNote);
        }

        [Fact]
        public async Task Reject_RequiresReason()
        {
            var repo = new FakeRequestRepository();
            var svc = Svc(repo);
            var created = await AssignedAsync(svc, 1);
            var ex = await Assert.ThrowsAsync<SalesCompleteException>(() => svc.RejectAsync(created.Id, 1, "  ", CancellationToken.None));
            Assert.Equal(400, ex.StatusCode);
        }

        [Fact]
        public async Task Rejected_CanConvertToSale()
        {
            var repo = new FakeRequestRepository();
            var svc = Svc(repo);
            var created = await AssignedAsync(svc, 1);
            await svc.RejectAsync(created.Id, 1, "زبون غير موجود", CancellationToken.None);
            await svc.MarkConvertedAsync(created.Id, 1, 99, DateTime.UtcNow, CancellationToken.None);
            var row = await svc.GetForEmployeeAsync(created.Id, 1, CancellationToken.None);
            Assert.Equal(SalesRequestStatuses.ConvertedToSale, row.Status);
            Assert.Equal(99, row.ConvertedToSaleId);
            Assert.Contains(row.History, h => h.Event == SalesRequestEvents.ConvertedToSale && h.PreviousStatus == SalesRequestStatuses.Rejected);
        }

        [Fact]
        public async Task Completed_CannotPendOrReject()
        {
            var repo = new FakeRequestRepository();
            var svc = Svc(repo);
            var created = await AssignedAsync(svc, 1);
            await svc.MarkConvertedAsync(created.Id, 1, 77, DateTime.UtcNow, CancellationToken.None);
            await svc.MarkCompletedBySaleIdAsync(77, DateTime.UtcNow, CancellationToken.None);
            var pend = await Assert.ThrowsAsync<SalesCompleteException>(() =>
                svc.PendAsync(created.Id, 1, "ملاحظة", CancellationToken.None));
            Assert.Equal(409, pend.StatusCode);
            var reject = await Assert.ThrowsAsync<SalesCompleteException>(() =>
                svc.RejectAsync(created.Id, 1, "سبب", CancellationToken.None));
            Assert.Equal(409, reject.StatusCode);
        }

        [Fact]
        public async Task Return_KeepsEmployeeAndRejectionReason()
        {
            var repo = new FakeRequestRepository();
            var svc = Svc(repo);
            var created = await AssignedAsync(svc, 1);
            await svc.RejectAsync(created.Id, 1, "زبون غير موجود", CancellationToken.None);
            var returned = await svc.ReturnAsync(Manager(), created.Id, "أعد المتابعة", CancellationToken.None);
            Assert.Equal(SalesRequestStatuses.Returned, returned.Status);
            Assert.Equal(1, returned.TargetEmployeeId);
            Assert.Equal("زبون غير موجود", returned.RejectionReason);
            Assert.Equal("أعد المتابعة", returned.ReturnNote);
            var prepared = await svc.PrepareForSaleAsync(created.Id, 1, CancellationToken.None);
            Assert.Equal(SalesRequestStatuses.PreparedForSale, prepared.Status);
        }

        [Fact]
        public async Task History_IsAppendOnly()
        {
            var repo = new FakeRequestRepository();
            var svc = Svc(repo);
            var created = await AssignedAsync(svc, 1);
            await svc.PendAsync(created.Id, 1, "تعليق", CancellationToken.None);
            await svc.PrepareForSaleAsync(created.Id, 1, CancellationToken.None);
            var row = await svc.GetForManagerAsync(created.Id, CancellationToken.None);
            Assert.Contains(row!.History, h => h.Event == SalesRequestEvents.Created);
            Assert.Contains(row.History, h => h.Event == SalesRequestEvents.Assigned);
            Assert.Contains(row.History, h => h.Event == SalesRequestEvents.Pending);
            Assert.Contains(row.History, h => h.Event == SalesRequestEvents.PendingNote);
            Assert.Contains(row.History, h => h.Event == SalesRequestEvents.PreparedForSale);
            Assert.True(row.History.Count >= 5);
        }

        [Fact]
        public async Task Convert_LinksSaleId()
        {
            var repo = new FakeRequestRepository();
            var svc = Svc(repo);
            var created = await AssignedAsync(svc, 1);
            await svc.PrepareForSaleAsync(created.Id, 1, CancellationToken.None);
            await svc.MarkConvertedAsync(created.Id, 1, 77, DateTime.UtcNow, CancellationToken.None);
            var row = await svc.GetForEmployeeAsync(created.Id, 1, CancellationToken.None);
            Assert.Equal(SalesRequestStatuses.ConvertedToSale, row.Status);
            Assert.Equal(77, row.ConvertedToSaleId);
        }

        [Fact]
        public async Task CompleteSale_MarksRequestCompleted()
        {
            var repo = new FakeRequestRepository();
            var svc = Svc(repo);
            var created = await AssignedAsync(svc, 1);
            await svc.PrepareForSaleAsync(created.Id, 1, CancellationToken.None);
            await svc.MarkConvertedAsync(created.Id, 1, 77, DateTime.UtcNow, CancellationToken.None);
            await svc.MarkCompletedBySaleIdAsync(77, DateTime.UtcNow, CancellationToken.None);
            Assert.Equal(SalesRequestStatuses.Completed, repo.Rows[0].Status);
        }

        [Fact]
        public async Task DuplicateConversion_Prevented()
        {
            var repo = new FakeRequestRepository();
            var svc = Svc(repo);
            var created = await AssignedAsync(svc, 1);
            await svc.PrepareForSaleAsync(created.Id, 1, CancellationToken.None);
            await svc.MarkConvertedAsync(created.Id, 1, 77, DateTime.UtcNow, CancellationToken.None);
            var ex = await Assert.ThrowsAsync<SalesCompleteException>(() =>
                svc.MarkConvertedAsync(created.Id, 1, 88, DateTime.UtcNow, CancellationToken.None));
            Assert.Equal(409, ex.StatusCode);
        }

        [Fact]
        public void ManagerController_RequiresSalesManagerPolicy()
        {
            var attr = typeof(SalesManagerController).GetCustomAttribute<AuthorizeAttribute>();
            Assert.Equal(SalesPolicies.SalesManager, attr?.Policy);
        }

        [Fact]
        public void EmployeeController_HasNoGpsGet()
        {
            var gets = typeof(SalesController).GetMethods()
                .Where(m => m.GetCustomAttribute<HttpGetAttribute>() != null)
                .Select(m => m.GetCustomAttribute<HttpGetAttribute>()!.Template ?? m.Name);
            Assert.DoesNotContain(gets, t => t != null && t.Contains("location", StringComparison.OrdinalIgnoreCase));
        }
    }

    public class SalesManagerTrackingTests
    {
        [Fact]
        public void LatestLocation_IsNewest()
        {
            var older = DateTime.UtcNow.AddMinutes(-10);
            var newer = DateTime.UtcNow;
            Assert.True(LiveLocationGate.ShouldMoveMarker(older, newer));
            Assert.False(LiveLocationGate.ShouldMoveMarker(newer, older));
        }

        [Fact]
        public void OldOfflinePoint_DoesNotReplaceLive()
        {
            var live = new DateTime(2026, 9, 2, 8, 0, 0, DateTimeKind.Utc);
            var offline = new DateTime(2026, 9, 2, 2, 55, 0, DateTimeKind.Utc);
            Assert.False(LiveLocationGate.ShouldMoveMarker(live, offline));
        }

        [Fact]
        public void SignalR_StaleEventIgnored()
        {
            var current = DateTime.UtcNow;
            Assert.False(LiveLocationGate.ShouldMoveMarker(current, current.AddSeconds(-30)));
        }

        [Fact]
        public async Task EmployeeList_AllowedForLogic()
        {
            var read = new FakeManagerRead();
            read.Shifts.Add(new SalesShiftDTO
            {
                ShiftId = 1,
                EmployeeId = 1,
                Status = SalesShiftStatuses.Active,
                StartedAtUtc = new DateTime(2026, 9, 2, 5, 15, 0, DateTimeKind.Utc),
                StartedAtIraq = new DateTime(2026, 9, 2, 8, 15, 0),
                CutoffAtUtc = new DateTime(2026, 9, 3, 0, 0, 0, DateTimeKind.Utc)
            });
            read.Points[1] = new SalesManagerLocationPointDTO
            {
                EmployeeId = 1,
                CapturedAtUtc = new DateTime(2026, 9, 2, 5, 20, 0, DateTimeKind.Utc),
                Latitude = 32,
                Longitude = 44
            };
            var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SalesManagement:BranchId"] = "najaf-demo",
                ["SalesManagement:BranchName"] = "النجف"
            }).Build();
            var svc = new SalesManagerQueryService(read, new FakeRequestRepository(), new FakeClock(), new SalesManagerTrackingOptions(), cfg);
            var list = await svc.ListEmployeesAsync(null, null, CancellationToken.None);
            Assert.Contains(list, e => e.EmployeeId == 1 && e.LocationStatus == SalesLocationStatuses.Live);
        }

        [Fact]
        public async Task Route_IsAscending()
        {
            var read = new FakeManagerRead();
            read.Route.Add(new SalesManagerRoutePointDTO { CapturedAt = new DateTime(2026, 9, 2, 6, 0, 0, DateTimeKind.Utc), Latitude = 1, Longitude = 1 });
            read.Route.Add(new SalesManagerRoutePointDTO { CapturedAt = new DateTime(2026, 9, 2, 5, 0, 0, DateTimeKind.Utc), Latitude = 2, Longitude = 2 });
            var cfg = new ConfigurationBuilder().AddInMemoryCollection().Build();
            var svc = new SalesManagerQueryService(read, new FakeRequestRepository(), new FakeClock(), new SalesManagerTrackingOptions(), cfg);
            var route = await svc.GetRouteAsync(1, new DateTime(2026, 9, 2), CancellationToken.None);
            Assert.True(route.Points[0].CapturedAt <= route.Points[1].CapturedAt);
        }

        [Fact]
        public void IraqWorkday_Uses3am()
        {
            var iraq = new DateTime(2026, 9, 2, 8, 15, 0);
            Assert.Equal(new DateTime(2026, 9, 3, 3, 0, 0), IraqTimeService.CutoffIraq(iraq));
        }
    }
}
