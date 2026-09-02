using System.Reflection;
using BE_Company.Sales.Authorization;
using BE_Company.Sales.Controllers;
using BE_Company.Sales.DTO;
using BE_Company.Sales.Models;
using BE_Company.Sales.Services;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace BE_Company.Sales.Tests
{
    public sealed class FakeClock : IIraqClock
    {
        public DateTime UtcNow { get; set; } = new(2026, 9, 2, 5, 15, 0, DateTimeKind.Utc);
    }

    public sealed class FakeTrackingRepository : ISalesTrackingRepository
    {
        public readonly List<SalesShiftDTO> Shifts = [];
        public readonly List<(int ShiftId, long Seq)> Points = [];
        public readonly List<string> Events = [];
        public int NextId = 1;

        public Task EnsureSchemaAsync(CancellationToken ct) => Task.CompletedTask;

        public Task<SalesShiftDTO?> GetActiveByEmployeeAsync(int employeeId, CancellationToken ct) =>
            Task.FromResult(Shifts.FirstOrDefault(s => s.EmployeeId == employeeId && s.Status == SalesShiftStatuses.Active));

        public Task<SalesShiftDTO?> GetByIdAsync(int shiftId, CancellationToken ct) =>
            Task.FromResult(Shifts.FirstOrDefault(s => s.ShiftId == shiftId));

        public Task<SalesShiftDTO> InsertActiveAsync(int employeeId, string employeeName, string cityValue, string cityName, DateTime startedAtUtc, DateTime startedAtIraq, DateTime cutoffAtUtc, CancellationToken ct)
        {
            var row = new SalesShiftDTO
            {
                ShiftId = NextId++,
                EmployeeId = employeeId,
                Status = SalesShiftStatuses.Active,
                StartedAtUtc = startedAtUtc,
                StartedAtIraq = startedAtIraq,
                CutoffAtUtc = cutoffAtUtc
            };
            Shifts.Add(row);
            return Task.FromResult(row);
        }

        public Task CloseAsync(int shiftId, DateTime closedAtUtc, string reason, CancellationToken ct)
        {
            var row = Shifts.First(s => s.ShiftId == shiftId);
            row.Status = SalesShiftStatuses.Closed;
            row.ClosedAtUtc = closedAtUtc;
            row.CloseReason = reason;
            return Task.CompletedTask;
        }

        public Task CloseExpiredAsync(DateTime utcNow, CancellationToken ct)
        {
            foreach (var row in Shifts.Where(s => s.Status == SalesShiftStatuses.Active && s.CutoffAtUtc <= utcNow))
            {
                row.Status = SalesShiftStatuses.Closed;
                row.ClosedAtUtc = utcNow;
                row.CloseReason = SalesShiftCloseReasons.AutomaticCutoff;
            }
            return Task.CompletedTask;
        }

        public Task<int> TryInsertPointAsync(int employeeId, int shiftId, SalesLocationPointRequestDTO point, DateTime receivedAtUtc, CancellationToken ct)
        {
            if (Points.Any(p => p.ShiftId == shiftId && p.Seq == point.DeviceSequence))
            {
                return Task.FromResult(0);
            }

            Points.Add((shiftId, point.DeviceSequence));
            return Task.FromResult(1);
        }

        public Task InsertEventAsync(int employeeId, int? shiftId, string eventType, DateTime occurredAtUtc, string? metadata, CancellationToken ct)
        {
            Events.Add(eventType);
            return Task.CompletedTask;
        }
    }

    public class SalesTrackingTests
    {
        private static SalesIdentity Id(int employee = 1) => new()
        {
            EmployeeId = employee,
            EmployeeName = "موظف",
            BranchId = "najaf-demo",
            BranchName = "النجف",
            Role = SalesRoles.SalesEmployee
        };

        [Fact]
        public void Cutoff_FromMorningStart_IsNextDay3amIraq()
        {
            var utc = new DateTime(2026, 9, 2, 5, 15, 0, DateTimeKind.Utc);
            var iraq = IraqTimeService.ToIraq(utc);
            Assert.Equal(new DateTime(2026, 9, 2, 8, 15, 0), iraq);
            Assert.Equal(new DateTime(2026, 9, 3, 3, 0, 0), IraqTimeService.CutoffIraq(iraq));
        }

        [Fact]
        public async Task StartShift_CreatesActive()
        {
            var repo = new FakeTrackingRepository();
            var clock = new FakeClock();
            var svc = new SalesShiftService(repo, clock);
            var shift = await svc.StartAsync(Id(), CancellationToken.None);
            Assert.True(shift.IsNew);
            Assert.Equal(SalesShiftStatuses.Active, shift.Status);
            Assert.Contains(SalesTrackingEventTypes.ShiftStarted, repo.Events);
        }

        [Fact]
        public async Task StartTwice_ReturnsSameActiveShift()
        {
            var repo = new FakeTrackingRepository();
            var svc = new SalesShiftService(repo, new FakeClock());
            var first = await svc.StartAsync(Id(), CancellationToken.None);
            var second = await svc.StartAsync(Id(), CancellationToken.None);
            Assert.Equal(first.ShiftId, second.ShiftId);
            Assert.False(second.IsNew);
            Assert.Single(repo.Shifts);
        }

        [Fact]
        public async Task EmployeeA_CannotUseBShift()
        {
            var repo = new FakeTrackingRepository();
            var clock = new FakeClock();
            var shifts = new SalesShiftService(repo, clock);
            var a = await shifts.StartAsync(Id(1), CancellationToken.None);
            var locations = new SalesLocationIngestService(repo, clock);
            var ex = await Assert.ThrowsAsync<SalesCompleteException>(() => locations.IngestBatchAsync(
                Id(2),
                new SalesLocationBatchRequestDTO { ShiftId = a.ShiftId, Points = [ValidPoint(clock.UtcNow)] },
                CancellationToken.None));
            Assert.Equal(403, ex.StatusCode);
        }

        [Fact]
        public async Task NormalBatch_Accepted()
        {
            var (repo, clock, shift, ingest) = await Ready();
            var result = await ingest.IngestBatchAsync(Id(), new SalesLocationBatchRequestDTO
            {
                ShiftId = shift.ShiftId,
                Points = [ValidPoint(clock.UtcNow)]
            }, CancellationToken.None);
            Assert.Equal(1, result.Accepted);
        }

        [Fact]
        public async Task DuplicateDeviceSequence_NotRepeated()
        {
            var (repo, clock, shift, ingest) = await Ready();
            var point = ValidPoint(clock.UtcNow);
            await ingest.IngestBatchAsync(Id(), new() { ShiftId = shift.ShiftId, Points = [point] }, CancellationToken.None);
            var second = await ingest.IngestBatchAsync(Id(), new() { ShiftId = shift.ShiftId, Points = [point] }, CancellationToken.None);
            Assert.Equal(1, second.Duplicates);
            Assert.Equal(0, second.Accepted);
            Assert.Single(repo.Points);
        }

        [Fact]
        public async Task BatchOver500_Rejected()
        {
            var (_, clock, shift, ingest) = await Ready();
            var points = Enumerable.Range(1, 501).Select(i => ValidPoint(clock.UtcNow, i)).ToList();
            var ex = await Assert.ThrowsAsync<SalesCompleteException>(() => ingest.IngestBatchAsync(
                Id(), new() { ShiftId = shift.ShiftId, Points = points }, CancellationToken.None));
            Assert.Equal(400, ex.StatusCode);
        }

        [Fact]
        public async Task InvalidCoordinates_Rejected()
        {
            var (_, clock, shift, ingest) = await Ready();
            var bad = ValidPoint(clock.UtcNow);
            bad.Latitude = 200;
            var result = await ingest.IngestBatchAsync(Id(), new() { ShiftId = shift.ShiftId, Points = [bad] }, CancellationToken.None);
            Assert.Equal(1, result.Rejected);
            Assert.Equal(0, result.Accepted);
        }

        [Fact]
        public async Task LateOfflinePoints_BeforeCutoff_Accepted()
        {
            var (repo, clock, shift, ingest) = await Ready();
            clock.UtcNow = shift.CutoffAtUtc.AddHours(5);
            var captured = shift.CutoffAtUtc.AddMinutes(-5);
            var result = await ingest.IngestBatchAsync(Id(), new()
            {
                ShiftId = shift.ShiftId,
                Points = [ValidPoint(captured)]
            }, CancellationToken.None);
            Assert.Equal(1, result.Accepted);
            Assert.Equal(SalesShiftStatuses.Closed, repo.Shifts[0].Status);
        }

        [Fact]
        public async Task PointsAfterCutoff_Rejected()
        {
            var (_, clock, shift, ingest) = await Ready();
            var result = await ingest.IngestBatchAsync(Id(), new()
            {
                ShiftId = shift.ShiftId,
                Points = [ValidPoint(shift.CutoffAtUtc.AddMinutes(1))]
            }, CancellationToken.None);
            Assert.Equal(1, result.Rejected);
        }

        [Fact]
        public void Employee_CannotReadGps_NoGetLocationEndpoint()
        {
            var gets = typeof(SalesController).GetMethods()
                .Where(m => m.GetCustomAttribute<HttpGetAttribute>() != null)
                .Select(m => m.GetCustomAttribute<HttpGetAttribute>()!.Template ?? m.Name)
                .ToList();
            Assert.DoesNotContain(gets, t => t != null && t.Contains("location", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task ShiftClosesAfterIraq3am()
        {
            var repo = new FakeTrackingRepository();
            var clock = new FakeClock();
            var svc = new SalesShiftService(repo, clock);
            await svc.StartAsync(Id(), CancellationToken.None);
            clock.UtcNow = repo.Shifts[0].CutoffAtUtc.AddMinutes(1);
            var current = await svc.GetCurrentAsync(1, CancellationToken.None);
            Assert.Null(current);
            Assert.Equal(SalesShiftStatuses.Closed, repo.Shifts[0].Status);
            Assert.Equal(SalesShiftCloseReasons.AutomaticCutoff, repo.Shifts[0].CloseReason);
        }

        [Fact]
        public async Task NewShiftAfterCutoff()
        {
            var repo = new FakeTrackingRepository();
            var clock = new FakeClock();
            var svc = new SalesShiftService(repo, clock);
            var first = await svc.StartAsync(Id(), CancellationToken.None);
            clock.UtcNow = first.CutoffAtUtc.AddMinutes(10);
            var second = await svc.StartAsync(Id(), CancellationToken.None);
            Assert.True(second.IsNew);
            Assert.NotEqual(first.ShiftId, second.ShiftId);
            Assert.Equal(SalesShiftStatuses.Closed, repo.Shifts.First(s => s.ShiftId == first.ShiftId).Status);
        }

        private static async Task<(FakeTrackingRepository repo, FakeClock clock, SalesShiftDTO shift, SalesLocationIngestService ingest)> Ready()
        {
            var repo = new FakeTrackingRepository();
            var clock = new FakeClock();
            var shifts = new SalesShiftService(repo, clock);
            var shift = await shifts.StartAsync(Id(), CancellationToken.None);
            return (repo, clock, shift, new SalesLocationIngestService(repo, clock));
        }

        private static SalesLocationPointRequestDTO ValidPoint(DateTime capturedUtc, long seq = 1) => new()
        {
            Latitude = 32.0,
            Longitude = 44.3,
            Accuracy = 12,
            CapturedAtUtc = DateTime.SpecifyKind(capturedUtc, DateTimeKind.Utc),
            DeviceSequence = seq
        };
    }
}
