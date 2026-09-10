/// Official follower route pins: deterministic slots from shift start.
/// First due = shiftStart + 10 minutes; then +20, +30, … (no point at start).
class OfficialSlot {
  static const iraqOffset = Duration(hours: 3);
  static const length = Duration(minutes: 10);

  static DateTime floorUtc(DateTime utc) {
    final iraq = utc.toUtc().add(iraqOffset);
    final slottedIraq = DateTime.utc(
      iraq.year,
      iraq.month,
      iraq.day,
      iraq.hour,
      (iraq.minute ~/ 10) * 10,
    );
    return slottedIraq.subtract(iraqOffset);
  }

  static int slotIndex(DateTime shiftStartUtc, DateTime slotUtc) {
    final start = shiftStartUtc.toUtc();
    final slot = slotUtc.toUtc();
    final index = slot.difference(start).inMilliseconds ~/ length.inMilliseconds;
    return index <= 0 ? 1 : index;
  }

  static bool isExactOfficialSlot(DateTime shiftStartUtc, DateTime officialSlotUtc) {
    final start = shiftStartUtc.toUtc();
    final slot = officialSlotUtc.toUtc();
    final delta = slot.difference(start);
    if (delta < length) return false;
    return delta.inMilliseconds % length.inMilliseconds == 0;
  }

  static int sequence(DateTime capturedUtc) {
    final seq = capturedUtc.toUtc().millisecondsSinceEpoch ~/ length.inMilliseconds;
    return seq <= 0 ? 1 : seq;
  }

  static List<DateTime> dueSlots({
    required DateTime shiftStartUtc,
    DateTime? lastOfficialSlotUtc,
    required DateTime nowUtc,
    required DateTime cutoffUtc,
  }) {
    final start = shiftStartUtc.toUtc();
    final now = nowUtc.toUtc();
    final cutoff = cutoffUtc.toUtc();
    final last = lastOfficialSlotUtc?.toUtc();

    final slots = <DateTime>[];
    var index = 1;
    while (true) {
      final due = start.add(Duration(minutes: 10 * index));
      if (!due.isBefore(cutoff)) break;
      if (due.isAfter(now)) break;
      if (last == null || due.isAfter(last)) {
        slots.add(due);
      }
      index += 1;
      if (index > 2000) break;
    }
    return slots;
  }

  static DateTime? nextDueUtc({
    required DateTime shiftStartUtc,
    DateTime? lastOfficialSlotUtc,
    required DateTime cutoffUtc,
  }) {
    final start = shiftStartUtc.toUtc();
    final cutoff = cutoffUtc.toUtc();
    final last = lastOfficialSlotUtc?.toUtc();
    var index = 1;
    while (true) {
      final due = start.add(Duration(minutes: 10 * index));
      if (!due.isBefore(cutoff)) return null;
      if (last == null || due.isAfter(last)) return due;
      index += 1;
      if (index > 2000) return null;
    }
  }
}
