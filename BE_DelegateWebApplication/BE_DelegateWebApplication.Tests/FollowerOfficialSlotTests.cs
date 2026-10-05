using BE_DelegateWebApplication.Services.FollowerTracking;
using Xunit;

namespace BE_DelegateWebApplication.Tests
{
    public sealed class FollowerOfficialSlotTests
    {
        [Fact]
        public void FirstDueIsStart_NoTenMinuteMidSlots()
        {
            var start = new DateTime(2026, 9, 10, 7, 13, 27, DateTimeKind.Utc);
            var due = FollowerOfficialSlot.DueSlots(start, null, start, start.AddHours(18));
            Assert.Equal(new[] { start }, due);
            Assert.True(FollowerOfficialSlot.IsExactOfficialSlot(start, start));
            Assert.False(FollowerOfficialSlot.IsExactOfficialSlot(start, start.AddMinutes(10)));
            Assert.False(FollowerOfficialSlot.IsExactOfficialSlot(start, start.AddMinutes(8)));
        }

        [Fact]
        public void AfterStartRecorded_NoFurtherDueSlots()
        {
            var start = new DateTime(2026, 9, 10, 7, 13, 27, DateTimeKind.Utc);
            var due = FollowerOfficialSlot.DueSlots(start, start, start.AddMinutes(40), start.AddHours(18));
            Assert.Empty(due);
        }

        [Fact]
        public void EndSlotMatchesClosedAt()
        {
            var start = new DateTime(2026, 9, 10, 7, 13, 27, DateTimeKind.Utc);
            var closed = start.AddHours(8);
            Assert.True(FollowerOfficialSlot.IsEndSlot(closed, closed));
            Assert.True(FollowerOfficialSlot.IsExactOfficialSlot(start, closed, closed));
            Assert.False(FollowerOfficialSlot.IsExactOfficialSlot(start, closed));
        }

        [Fact]
        public void ActiveBatchAcceptsStartPinOnly()
        {
            var shift = new FollowerShiftDto
            {
                ShiftId = 1,
                FollowerId = 9,
                Status = "Active",
                StartedAtUtc = new DateTime(2026, 9, 10, 7, 13, 27, DateTimeKind.Utc),
                CutoffAtUtc = new DateTime(2026, 9, 11, 0, 0, 0, DateTimeKind.Utc),
            };
            var startPoint = new FollowerLocationPointDto
            {
                Latitude = 32,
                Longitude = 44,
                CapturedAtUtc = shift.StartedAtUtc,
                OfficialSlotUtc = shift.StartedAtUtc,
                DeviceSequence = 1,
                IsOfficial = true
            };
            Assert.True(FollowerTrackingService.IsValidPoint(startPoint, shift));

            var midPoint = new FollowerLocationPointDto
            {
                Latitude = 32,
                Longitude = 44,
                CapturedAtUtc = shift.StartedAtUtc.AddMinutes(10),
                OfficialSlotUtc = shift.StartedAtUtc.AddMinutes(10),
                DeviceSequence = 2,
                IsOfficial = true
            };
            Assert.False(FollowerTrackingService.IsValidPoint(midPoint, shift));
        }
    }
}
