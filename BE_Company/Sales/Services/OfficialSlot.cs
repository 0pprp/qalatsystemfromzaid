using BE_Company.Sales.DTO;

namespace BE_Company.Sales.Services
{
    /// <summary>
    /// Official sales-manager pins: one 10-minute Iraq-local slot per shift.
    /// CapturedAt for an official point is the slot time, not GPS/sync time.
    /// </summary>
    public static class OfficialSlot
    {
        public static readonly TimeSpan Length = TimeSpan.FromMinutes(10);

        public static DateTime FloorUtc(DateTime utc)
        {
            var iraq = IraqTimeService.ToIraq(utc);
            var minutes = iraq.Minute / 10 * 10;
            var slottedIraq = new DateTime(iraq.Year, iraq.Month, iraq.Day, iraq.Hour, minutes, 0, DateTimeKind.Unspecified);
            return IraqTimeService.ToUtcFromIraq(slottedIraq);
        }

        public static long Sequence(DateTime slotUtc)
        {
            var utc = slotUtc.Kind == DateTimeKind.Utc ? slotUtc : DateTime.SpecifyKind(slotUtc, DateTimeKind.Utc);
            return utc.Subtract(DateTime.UnixEpoch).Ticks / Length.Ticks;
        }

        public static IReadOnlyList<DateTime> DueSlots(
            DateTime shiftStartUtc,
            DateTime? lastOfficialSlotUtc,
            DateTime nowUtc,
            DateTime cutoffUtc)
        {
            shiftStartUtc = Utc(shiftStartUtc);
            nowUtc = Utc(nowUtc);
            cutoffUtc = Utc(cutoffUtc);
            var first = FloorUtc(shiftStartUtc);
            DateTime cursor;
            if (lastOfficialSlotUtc is DateTime last && last != default)
            {
                cursor = FloorUtc(Utc(last)).Add(Length);
            }
            else
            {
                cursor = first;
            }

            if (cursor < first)
            {
                cursor = first;
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
            var slot = FloorUtc(original);
            point.CapturedAtUtc = slot;
            point.OfficialSlotUtc = slot;
            point.ActualCapturedAtUtc = original;
            point.IsOfficial = true;
            point.DeviceSequence = Sequence(slot);
            return point;
        }

        public static IReadOnlyList<SalesManagerRoutePointDTO> SelectRoutePoints(
            IEnumerable<SalesManagerRoutePointDTO> rows)
        {
            var list = rows.ToList();
            var official = list.Where(p => p.IsOfficial).ToList();
            var source = official.Count > 0 ? official : list;
            return source
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
