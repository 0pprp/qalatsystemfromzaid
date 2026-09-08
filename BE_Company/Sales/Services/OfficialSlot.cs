using BE_Company.Sales.DTO;

namespace BE_Company.Sales.Services
{
    /// <summary>
    /// Official sales-manager route pins: first GPS after shift start, then every 10 minutes
    /// from that first capture (not clock-rounded :00/:10/:20 slots).
    /// CapturedAtUtc stays the real due/capture time; DB stores UTC.
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

        public static long Sequence(DateTime capturedUtc)
        {
            var utc = capturedUtc.Kind == DateTimeKind.Utc ? capturedUtc : DateTime.SpecifyKind(capturedUtc, DateTimeKind.Utc);
            var seq = utc.Subtract(DateTime.UnixEpoch).Ticks / Length.Ticks;
            return seq <= 0 ? 1 : seq;
        }

        /// <summary>
        /// Due route capture times relative to shift start / last capture — never clock-floored.
        /// First due = shift start when no prior capture; then last + 10 minutes repeatedly.
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
            DateTime cursor;
            if (lastOfficialSlotUtc is DateTime last && last != default)
            {
                cursor = Utc(last).Add(Length);
            }
            else
            {
                cursor = shiftStartUtc;
            }

            var slots = new List<DateTime>();
            while (cursor <= nowUtc && cursor < cutoffUtc)
            {
                slots.Add(cursor);
                cursor = cursor.Add(Length);
            }

            return slots;
        }

        public static SalesLocationPointRequestDTO SnapOfficial(SalesLocationPointRequestDTO point, DateTime? actualCapturedUtc = null)
        {
            var original = Utc(actualCapturedUtc ?? point.CapturedAtUtc);
            // Keep exact capture/due time — do not floor to clock slots.
            point.CapturedAtUtc = original;
            point.OfficialSlotUtc = original;
            point.ActualCapturedAtUtc = original;
            point.IsOfficial = true;
            if (point.DeviceSequence <= 0)
            {
                point.DeviceSequence = Sequence(original);
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
