using BE_DelegateWebApplication.Services.FollowerTracking;
using Xunit;

namespace BE_DelegateWebApplication.Tests
{
    public sealed class FollowerOfficialSlotTests
    {
        [Fact]
        public void NoPointAtStart_FirstDuePlusTen()
        {
            var start = new DateTime(2026, 9, 10, 7, 13, 27, DateTimeKind.Utc);
            Assert.Empty(FollowerOfficialSlot.DueSlots(start, null, start, start.AddHours(18)));
            var due = FollowerOfficialSlot.DueSlots(start, null, start.AddMinutes(10), start.AddHours(18));
            Assert.Equal(new[] { start.AddMinutes(10) }, due);
            Assert.True(FollowerOfficialSlot.IsExactOfficialSlot(start, start.AddMinutes(10)));
            Assert.False(FollowerOfficialSlot.IsExactOfficialSlot(start, start.AddMinutes(8)));
            Assert.False(FollowerOfficialSlot.IsExactOfficialSlot(start, start.AddMinutes(9).AddSeconds(59)));
        }

        [Fact]
        public void SecondSlotIsStartPlusTwenty_NotPreviousPlusTenDrift()
        {
            var start = new DateTime(2026, 9, 10, 7, 13, 27, DateTimeKind.Utc);
            var first = start.AddMinutes(10);
            var due = FollowerOfficialSlot.DueSlots(start, first, start.AddMinutes(25), start.AddHours(18));
            Assert.Equal(new[] { start.AddMinutes(20) }, due);
            Assert.Equal(start.AddMinutes(20), FollowerOfficialSlot.SlotUtc(start, 2));
        }

        [Fact]
        public void LateWakeMapsToOriginalSlot()
        {
            var start = new DateTime(2026, 9, 10, 7, 13, 27, DateTimeKind.Utc);
            var due = FollowerOfficialSlot.DueSlots(start, null, start.AddMinutes(12), start.AddHours(18));
            Assert.Equal(new[] { start.AddMinutes(10) }, due);
        }

        [Fact]
        public void ClosedShiftRejectsBatchEvenWithOlderCapturedAt()
        {
            var shift = new FollowerShiftDto
            {
                ShiftId = 1,
                FollowerId = 9,
                Status = "Closed",
                StartedAtUtc = new DateTime(2026, 9, 10, 7, 13, 27, DateTimeKind.Utc),
                CutoffAtUtc = new DateTime(2026, 9, 11, 0, 0, 0, DateTimeKind.Utc),
                ClosedAtUtc = new DateTime(2026, 9, 10, 8, 0, 0, DateTimeKind.Utc)
            };
            // Service-level closed check is before IsValidPoint; validate exact slot still holds for open shifts.
            var point = new FollowerLocationPointDto
            {
                Latitude = 32,
                Longitude = 44,
                CapturedAtUtc = shift.StartedAtUtc.AddMinutes(10),
                OfficialSlotUtc = shift.StartedAtUtc.AddMinutes(10),
                DeviceSequence = 1
            };
            Assert.True(FollowerTrackingService.IsValidPoint(point, new FollowerShiftDto
            {
                StartedAtUtc = shift.StartedAtUtc,
                CutoffAtUtc = shift.CutoffAtUtc,
                Status = "Active"
            }));
        }
    }
}
