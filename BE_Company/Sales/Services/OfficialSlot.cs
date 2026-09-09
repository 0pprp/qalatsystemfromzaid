using BE_Company.Sales.DTO;

namespace BE_Company.Sales.Services
{
    /// <summary>
    /// Official sales-manager route pins: deterministic slots from shift start.
    /// First due = shiftStart + 10 minutes; then +20, +30, … (no point at start).
    /// CapturedAtUtc / OfficialSlotUtc stay the logical slot time; DB stores UTC.
    /// </summary>
    public static class OfficialSlot
    {
        public static readonly TimeSpan Length = TimeSpan.FromMinutes(10);

        /// <summary>
        /// Legacy helper for old rows that used Iraq clock floors. New captures do not use this.
        /// </summary>
        public static DateTime FloorUtc(DateTime utc)
        {
            var iraq = IraqTimeService.ToIraq(utc);
            var minutes = iraq.Minute / 10 * 10;
            var slottedIraq = new DateTime(iraq.Year, iraq.Month, iraq.Day, iraq.Hour, minutes, 0, DateTimeKind.Unspecified);
            return IraqTimeService.ToUtcFromIraq(slottedIraq);
        }

        /// <summary>
        /// Slot index n where due = shiftStart + n * 10min (n starts at 1).
        /// </summary>
        public static long SlotIndex(DateTime shiftStartUtc, DateTime slotUtc)
        {
            var start = Utc(shiftStartUtc);
            var slot = Utc(slotUtc);
            var index = (slot - start).Ticks / Length.Ticks;
            return index <= 0 ? 1 : index;
        }

        /// <summary>
        /// True only when officialSlotUtc equals shiftStart + n*10 minutes for integer n &gt;= 1.
        /// No early skew — 10:21 / 10:22:59 are never valid official slots for start 10:13.
        /// </summary>
        public static bool IsExactOfficialSlot(DateTime shiftStartUtc, DateTime officialSlotUtc)
        {
            var start = Utc(shiftStartUtc);
            var slot = Utc(officialSlotUtc);
            var delta = slot - start;
            if (delta < Length)
            {
                return false;
            }

            return delta.Ticks % Length.Ticks == 0;
        }

        public static DateTime SlotUtc(DateTime shiftStartUtc, long index)
        {
            if (index < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return Utc(shiftStartUtc).AddMinutes(10 * index);
        }

        public static long Sequence(DateTime capturedUtc)
        {
            var utc = capturedUtc.Kind == DateTimeKind.Utc ? capturedUtc : DateTime.SpecifyKind(capturedUtc, DateTimeKind.Utc);
            var seq = utc.Subtract(DateTime.UnixEpoch).Ticks / Length.Ticks;
            return seq <= 0 ? 1 : seq;
        }

        /// <summary>
        /// Due route capture times: shiftStart + 10*n for n=1,2,… while due &lt;= now and &lt; cutoff.
        /// Skips slots already accepted (due &lt;= lastOfficialSlotUtc).
        /// </summary>
        public static IReadOnlyList<DateTime> DueSlots(
            DateTime shiftStartUtc,
            DateTime? lastOfficialSlotUtc,
            DateTime nowUtc,
            DateTime cutoffUtc)
        {
            shiftStartUtc = Utc(shiftStartUtc);
            nowUtc = Utc(nowUtc);
            cutoffUtc = Utc(cutoffUtc);
            DateTime? last = lastOfficialSlotUtc is DateTime prior && prior != default
                ? Utc(prior)
                : null;

            var slots = new List<DateTime>();
            for (var index = 1; index <= 2000; index++)
            {
                var due = shiftStartUtc.AddMinutes(10 * index);
                if (due >= cutoffUtc)
                {
                    break;
                }

                if (due > nowUtc)
                {
                    break;
                }

                if (last is null || due > last.Value)
                {
                    slots.Add(due);
                }
            }

            return slots;
        }

        public static SalesLocationPointRequestDTO SnapOfficial(SalesLocationPointRequestDTO point, DateTime? actualCapturedUtc = null)
        {
            // OfficialSlotUtc = logical due (start + n*10). ActualCapturedAtUtc = device capture (may be late).
            var slot = point.OfficialSlotUtc is DateTime official && official != default
                ? Utc(official)
                : Utc(point.CapturedAtUtc);
            var actual = actualCapturedUtc is DateTime forced
                ? Utc(forced)
                : point.ActualCapturedAtUtc is DateTime reported && reported != default
                    ? Utc(reported)
                    : slot;

            point.OfficialSlotUtc = slot;
            point.CapturedAtUtc = slot;
            point.ActualCapturedAtUtc = actual;
            point.IsOfficial = true;
            if (point.DeviceSequence <= 0)
            {
                point.DeviceSequence = Sequence(slot);
            }

            return point;
        }

        public static IReadOnlyList<SalesManagerRoutePointDTO> SelectRoutePoints(
            IEnumerable<SalesManagerRoutePointDTO> rows)
        {
            var list = rows.ToList();
            var official = list.Where(p => p.IsOfficial).ToList();
            if (official.Count > 0)
            {
                return official
                    .GroupBy(p => p.DeviceSequence > 0 ? p.DeviceSequence : Sequence(p.CapturedAt))
                    .Select(group => group
                        .OrderBy(p => p.CapturedAt)
                        .ThenBy(p => p.DeviceSequence)
                        .Last())
                    .OrderBy(p => p.CapturedAt)
                    .ThenBy(p => p.DeviceSequence)
                    .ToList();
            }

            // Legacy non-official rows: keep one pin per old 10-minute clock bucket.
            return list
                .GroupBy(p => FloorUtc(p.CapturedAt))
                .Select(group =>
                {
                    var chosen = group
                        .OrderBy(p => p.CapturedAt)
                        .ThenBy(p => p.DeviceSequence)
                        .Last();
                    chosen.CapturedAt = group.Key;
                    return chosen;
                })
                .OrderBy(p => p.CapturedAt)
                .ThenBy(p => p.DeviceSequence)
                .ToList();
        }

        private static DateTime Utc(DateTime value) =>
            value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }
}
