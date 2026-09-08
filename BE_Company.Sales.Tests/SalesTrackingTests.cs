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
        public readonly List<(int ShiftId, long Seq, DateTime Slot)> Points = [];
        public readonly List<DateTime> InsertedCapturedAt = [];
        public readonly List<DateTime> InsertedReceivedAt = [];
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
            foreach (var key in Live.Where(kv => kv.Value.ShiftId == shiftId).Select(kv => kv.Key).ToList())
            {
                Live.Remove(key);
            }
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

        public readonly Dictionary<int, SalesLiveLocationRequestDTO> Live = [];

        public Task<int> TryInsertPointAsync(int employeeId, int shiftId, SalesLocationPointRequestDTO point, DateTime receivedAtUtc, CancellationToken ct)
        {
            var slot = point.OfficialSlotUtc ?? point.CapturedAtUtc;
            if (Points.Any(p => p.ShiftId == shiftId && (p.Seq == point.DeviceSequence || p.Slot == slot)))
            {
                return Task.FromResult(0);
            }

            Points.Add((shiftId, point.DeviceSequence, slot));
            InsertedCapturedAt.Add(point.CapturedAtUtc);
            InsertedReceivedAt.Add(receivedAtUtc);
            return Task.FromResult(1);
        }

        public Task UpsertLiveLocationAsync(int employeeId, int shiftId, SalesLiveLocationRequestDTO point, DateTime updatedAtUtc, CancellationToken ct)
        {
            if (Live.TryGetValue(employeeId, out var existing)
                && existing.CapturedAtUtc > point.CapturedAtUtc)
            {
                return Task.CompletedTask;
            }

            Live[employeeId] = point;
            point.ShiftId = shiftId;
            return Task.CompletedTask;
        }

        public Task EndLiveLocationAsync(int employeeId, int shiftId, DateTime endedAtUtc, CancellationToken ct)
        {
            Live.Remove(employeeId);
            return Task.CompletedTask;
        }

        public Task EndLiveLocationsForClosedShiftsAsync(DateTime endedAtUtc, CancellationToken ct)
        {
            foreach (var closed in Shifts.Where(s => s.Status == SalesShiftStatuses.Closed).Select(s => s.EmployeeId).ToHashSet())
            {
                Live.Remove(closed);
            }

            return Task.CompletedTask;
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
        public async Task AccuracyAround50m_IsAccepted()
        {
            var (_, clock, shift, ingest) = await Ready();
            var point = ValidPoint(clock.UtcNow);
            point.Accuracy = 80;
            var result = await ingest.IngestBatchAsync(Id(), new() { ShiftId = shift.ShiftId, Points = [point] }, CancellationToken.None);
            Assert.Equal(1, result.Accepted);
            Assert.Equal(0, result.Rejected);
        }

        [Fact]
        public async Task CapturedTimestamp_IsNotReplacedByServerTime()
        {
            var (repo, clock, shift, ingest) = await Ready();
            var captured = new DateTime(2026, 9, 2, 6, 40, 0, DateTimeKind.Utc);
            clock.UtcNow = new DateTime(2026, 9, 2, 8, 0, 0, DateTimeKind.Utc);
            var point = ValidPoint(captured);
            await ingest.IngestBatchAsync(Id(), new() { ShiftId = shift.ShiftId, Points = [point] }, CancellationToken.None);
            Assert.Equal(captured, repo.InsertedCapturedAt.Single());
            Assert.Equal(clock.UtcNow, repo.InsertedReceivedAt.Single());
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
        public void OfficialSlot_DoesNotFloorToClockTenMinutes()
        {
            var start = new DateTime(2026, 9, 2, 17, 32, 0, DateTimeKind.Utc); // Iraq 20:32
            var due = OfficialSlot.DueSlots(start, null, start, start.AddHours(18));
            Assert.Equal(new[] { start }, due);
            Assert.NotEqual(OfficialSlot.FloorUtc(start), due[0]);
        }

        [Fact]
        public void OfficialSlot_CatchUpFromShiftStartEveryTenMinutes()
        {
            var start = new DateTime(2026, 9, 2, 17, 32, 0, DateTimeKind.Utc); // Iraq 20:32
            var now = start.AddMinutes(25);
            var cutoff = start.AddHours(6);
            var due = OfficialSlot.DueSlots(start, null, now, cutoff);
            Assert.Equal(
                new[]
                {
                    start,
                    start.AddMinutes(10),
                    start.AddMinutes(20),
                },
                due);
            Assert.DoesNotContain(new DateTime(2026, 9, 2, 17, 40, 0, DateTimeKind.Utc), due);
            Assert.DoesNotContain(new DateTime(2026, 9, 2, 17, 50, 0, DateTimeKind.Utc), due);
        }

        [Fact]
        public void OfficialSlot_CatchUpAfterFirstCapture()
        {
            var start = new DateTime(2026, 9, 2, 22, 0, 0, DateTimeKind.Utc);
            var last = start;
            var now = new DateTime(2026, 9, 2, 22, 34, 0, DateTimeKind.Utc);
            var cutoff = new DateTime(2026, 9, 3, 0, 0, 0, DateTimeKind.Utc);
            var due = OfficialSlot.DueSlots(start, last, now, cutoff);
            Assert.Equal(
                new[]
                {
                    new DateTime(2026, 9, 2, 22, 10, 0, DateTimeKind.Utc),
                    new DateTime(2026, 9, 2, 22, 20, 0, DateTimeKind.Utc),
                    new DateTime(2026, 9, 2, 22, 30, 0, DateTimeKind.Utc),
                },
                due);
        }

        [Fact]
        public async Task DuplicateDeviceSequence_Retry_NotInserted()
        {
            var (repo, clock, shift, ingest) = await Ready();
            var first = ValidPoint(new DateTime(2026, 9, 2, 8, 32, 0, DateTimeKind.Utc), 1);
            var retry = ValidPoint(new DateTime(2026, 9, 2, 8, 32, 0, DateTimeKind.Utc), 1);
            await ingest.IngestBatchAsync(Id(), new() { ShiftId = shift.ShiftId, Points = [first] }, CancellationToken.None);
            var result = await ingest.IngestBatchAsync(Id(), new() { ShiftId = shift.ShiftId, Points = [retry] }, CancellationToken.None);
            Assert.Equal(1, result.Duplicates);
            Assert.Equal(0, result.Accepted);
            Assert.Single(repo.Points);
            Assert.Equal(new DateTime(2026, 9, 2, 8, 32, 0, DateTimeKind.Utc), repo.InsertedCapturedAt.Single());
        }

        [Fact]
        public async Task AdjacentTenMinuteCaptures_AreEachAccepted()
        {
            var (repo, clock, shift, ingest) = await Ready();
            var start = new DateTime(2026, 9, 2, 8, 32, 0, DateTimeKind.Utc);
            var points = new[]
            {
                ValidPoint(start, 1),
                ValidPoint(start.AddMinutes(10), 2),
                ValidPoint(start.AddMinutes(20), 3),
            };
            var result = await ingest.IngestBatchAsync(Id(), new() { ShiftId = shift.ShiftId, Points = [..points] }, CancellationToken.None);
            Assert.Equal(3, result.Accepted);
            Assert.Equal(0, result.Duplicates);
            Assert.Equal(3, repo.Points.Count);
        }

        [Fact]
        public async Task CatchUpSlots_AreEachAcceptedOnce()
        {
            var (repo, clock, shift, ingest) = await Ready();
            var points = new[]
            {
                ValidPoint(new DateTime(2026, 9, 2, 8, 0, 0, DateTimeKind.Utc), 1),
                ValidPoint(new DateTime(2026, 9, 2, 8, 10, 0, DateTimeKind.Utc), 2),
                ValidPoint(new DateTime(2026, 9, 2, 8, 20, 0, DateTimeKind.Utc), 3),
                ValidPoint(new DateTime(2026, 9, 2, 8, 30, 0, DateTimeKind.Utc), 4),
            };
            var result = await ingest.IngestBatchAsync(Id(), new() { ShiftId = shift.ShiftId, Points = [..points] }, CancellationToken.None);
            Assert.Equal(4, result.Accepted);
            Assert.Equal(0, result.Duplicates);
            Assert.Equal(4, repo.Points.Count);
        }

        [Fact]
        public void Route_KeepsOfficialPointsWithoutClockBucketing()
        {
            var official = new List<SalesManagerRoutePointDTO>
            {
                new() { IsOfficial = true, CapturedAt = new DateTime(2026, 9, 2, 8, 32, 0, DateTimeKind.Utc), Latitude = 1, DeviceSequence = 1 },
                new() { IsOfficial = true, CapturedAt = new DateTime(2026, 9, 2, 8, 42, 0, DateTimeKind.Utc), Latitude = 2, DeviceSequence = 2 },
                new() { IsOfficial = false, CapturedAt = new DateTime(2026, 9, 2, 8, 35, 0, DateTimeKind.Utc), Latitude = 9, DeviceSequence = 9 },
            };
            var selected = OfficialSlot.SelectRoutePoints(official);
            Assert.Equal(2, selected.Count);
            Assert.Equal(1, selected[0].Latitude);
            Assert.Equal(new DateTime(2026, 9, 2, 8, 32, 0, DateTimeKind.Utc), selected[0].CapturedAt);
            Assert.Equal(2, selected[1].Latitude);

            var legacy = new List<SalesManagerRoutePointDTO>
            {
                new() { CapturedAt = new DateTime(2026, 9, 2, 8, 1, 0, DateTimeKind.Utc), Latitude = 1, DeviceSequence = 1 },
                new() { CapturedAt = new DateTime(2026, 9, 2, 8, 2, 0, DateTimeKind.Utc), Latitude = 2, DeviceSequence = 2 },
                new() { CapturedAt = new DateTime(2026, 9, 2, 8, 11, 0, DateTimeKind.Utc), Latitude = 3, DeviceSequence = 3 },
            };
            var bucketed = OfficialSlot.SelectRoutePoints(legacy);
            Assert.Equal(2, bucketed.Count);
            Assert.Equal(2, bucketed[0].Latitude);
            Assert.Equal(new DateTime(2026, 9, 2, 8, 0, 0, DateTimeKind.Utc), bucketed[0].CapturedAt);
            Assert.Equal(3, bucketed[1].Latitude);
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
        public async Task FirstCapture_AtShiftStart_IsAccepted()
        {
            var (repo, clock, shift, ingest) = await Ready();
            shift.StartedAtUtc = new DateTime(2026, 9, 2, 8, 32, 0, DateTimeKind.Utc);
            var result = await ingest.IngestBatchAsync(Id(), new()
            {
                ShiftId = shift.ShiftId,
                Points = [ValidPoint(shift.StartedAtUtc, 1)]
            }, CancellationToken.None);
            Assert.Equal(1, result.Accepted);
            Assert.Equal(0, result.Rejected);
            Assert.Equal(shift.StartedAtUtc, repo.InsertedCapturedAt.Single());
        }

        [Fact]
        public void IraqDisplay_Utc1840_IsBaghdad2140()
        {
            var utc = new DateTime(2026, 9, 2, 18, 40, 0, DateTimeKind.Utc);
            var iraq = IraqTimeService.ToIraq(utc);
            Assert.Equal(new DateTime(2026, 9, 2, 21, 40, 0), iraq);
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

        [Fact]
        public async Task LiveUpsert_DoesNotInsertRoutePoint()
        {
            var (repo, clock, shift, ingest) = await Ready();
            var live = await ingest.IngestLiveAsync(Id(), new SalesLiveLocationRequestDTO
            {
                ShiftId = shift.ShiftId,
                Latitude = 32.0375,
                Longitude = 44.4219,
                Accuracy = 18,
                CapturedAtUtc = clock.UtcNow
            }, CancellationToken.None);
            Assert.Equal(32.0375, live.Latitude);
            Assert.Equal(SalesLocationStatuses.Live, live.LocationStatus);
            Assert.Empty(repo.Points);
            Assert.Single(repo.Live);
        }

        [Fact]
        public async Task LiveUpsert_KeepsLatestDeviceTimestampOnly()
        {
            var (repo, clock, shift, ingest) = await Ready();
            await ingest.IngestLiveAsync(Id(), new SalesLiveLocationRequestDTO
            {
                ShiftId = shift.ShiftId,
                Latitude = 32.01,
                Longitude = 44.41,
                CapturedAtUtc = clock.UtcNow
            }, CancellationToken.None);
            await ingest.IngestLiveAsync(Id(), new SalesLiveLocationRequestDTO
            {
                ShiftId = shift.ShiftId,
                Latitude = 32.02,
                Longitude = 44.42,
                CapturedAtUtc = clock.UtcNow.AddSeconds(20)
            }, CancellationToken.None);
            await ingest.IngestLiveAsync(Id(), new SalesLiveLocationRequestDTO
            {
                ShiftId = shift.ShiftId,
                Latitude = 31.0,
                Longitude = 44.0,
                CapturedAtUtc = clock.UtcNow.AddSeconds(-30)
            }, CancellationToken.None);
            Assert.Single(repo.Live);
            Assert.Equal(32.02, repo.Live[1].Latitude);
        }

        [Fact]
        public async Task LiveUpload_RejectedAfterShiftEnd()
        {
            var (repo, clock, shift, ingest) = await Ready();
            var shifts = new SalesShiftService(repo, clock);
            await shifts.EndAsync(Id(), CancellationToken.None);
            var ex = await Assert.ThrowsAsync<SalesCompleteException>(() => ingest.IngestLiveAsync(Id(), new SalesLiveLocationRequestDTO
            {
                ShiftId = shift.ShiftId,
                Latitude = 32.0,
                Longitude = 44.3,
                CapturedAtUtc = clock.UtcNow
            }, CancellationToken.None));
            Assert.Equal(409, ex.StatusCode);
            Assert.Empty(repo.Live);
        }

        [Fact]
        public void StaleLiveLocation_AfterTwoToThreeMinutes()
        {
            var options = new SalesManagerTrackingOptions();
            var now = new DateTime(2026, 9, 2, 8, 0, 0, DateTimeKind.Utc);
            Assert.Equal(SalesLocationStatuses.Live, SalesLocationStatusService.Resolve(true, now.AddSeconds(-30), null, now, options));
            Assert.Equal(SalesLocationStatuses.Stale, SalesLocationStatusService.Resolve(true, now.AddMinutes(-3), null, now, options));
            Assert.Equal(150, options.LiveThresholdSeconds);
        }

        [Fact]
        public async Task BatchIngest_DoesNotUpsertLiveLocation()
        {
            var (repo, clock, shift, ingest) = await Ready();
            var result = await ingest.IngestBatchAsync(Id(), new SalesLocationBatchRequestDTO
            {
                ShiftId = shift.ShiftId,
                Points = [ValidPoint(clock.UtcNow)]
            }, CancellationToken.None);
            Assert.True(result.Accepted >= 1);
            Assert.Empty(repo.Live);
            Assert.Single(repo.Points);
        }
    }
}
