/// Official follower route pins: shift start + shift end only (no 10-minute mid-shift slots).
class OfficialSlot {
  static const iraqOffset = Duration(hours: 3);
  static const matchTolerance = Duration(seconds: 5);

  static DateTime floorUtc(DateTime utc) {
    final iraq = utc.toUtc().add(iraqOffset);
    final slottedIraq = DateTime.utc(
      iraq.year,
      iraq.month,
      iraq.day,
      iraq.hour,
      iraq.minute,
      iraq.second,
    );
    return slottedIraq.subtract(iraqOffset);
  }

  static bool isStartSlot(DateTime shiftStartUtc, DateTime officialSlotUtc) {
    final delta = officialSlotUtc.toUtc().difference(shiftStartUtc.toUtc()).abs();
    return delta <= matchTolerance;
  }

  static bool isEndSlot(DateTime closedAtUtc, DateTime officialSlotUtc) {
    final delta = officialSlotUtc.toUtc().difference(closedAtUtc.toUtc()).abs();
    return delta <= matchTolerance;
  }

  static int slotIndex(DateTime shiftStartUtc, DateTime slotUtc) =>
      isStartSlot(shiftStartUtc, slotUtc) ? 1 : 2;

  static bool isExactOfficialSlot(
    DateTime shiftStartUtc,
    DateTime officialSlotUtc, {
    DateTime? closedAtUtc,
  }) {
    if (isStartSlot(shiftStartUtc, officialSlotUtc)) return true;
    if (closedAtUtc != null && isEndSlot(closedAtUtc, officialSlotUtc)) return true;
    return false;
  }

  static int sequence(DateTime capturedUtc) {
    final seq = capturedUtc.toUtc().millisecondsSinceEpoch ~/ 60000;
    return seq <= 0 ? 1 : seq;
  }

  /// While active: only the start pin once. End pin is posted on shift close.
  static List<DateTime> dueSlots({
    required DateTime shiftStartUtc,
    DateTime? lastOfficialSlotUtc,
    required DateTime nowUtc,
    required DateTime cutoffUtc,
  }) {
    final start = shiftStartUtc.toUtc();
    final now = nowUtc.toUtc();
    final cutoff = cutoffUtc.toUtc();
    if (!start.isBefore(cutoff) || now.isBefore(start)) return const [];
    final last = lastOfficialSlotUtc?.toUtc();
    if (last != null && (isStartSlot(start, last) || !last.isBefore(start))) {
      return const [];
    }
    return [start];
  }

  static DateTime? nextDueUtc({
    required DateTime shiftStartUtc,
    DateTime? lastOfficialSlotUtc,
    required DateTime cutoffUtc,
  }) {
    final due = dueSlots(
      shiftStartUtc: shiftStartUtc,
      lastOfficialSlotUtc: lastOfficialSlotUtc,
      nowUtc: DateTime.now().toUtc().add(const Duration(days: 3650)),
      cutoffUtc: cutoffUtc,
    );
    return due.isEmpty ? null : due.first;
  }
}
