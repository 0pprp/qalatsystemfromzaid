/// Official manager route pins: first GPS after shift start, then every 10 minutes
/// from that capture — not Iraq clock floors (:00/:10/:20).
class OfficialSlot {
  static const iraqOffset = Duration(hours: 3);
  static const length = Duration(minutes: 10);

  /// Legacy only — new route timing does not floor to clock slots.
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

  static int sequence(DateTime capturedUtc) {
    final seq = capturedUtc.toUtc().millisecondsSinceEpoch ~/ length.inMilliseconds;
    return seq <= 0 ? 1 : seq;
  }

  /// Due capture times from shift start / last capture (fixed 10-minute interval).
  static List<DateTime> dueSlots({
    required DateTime shiftStartUtc,
    DateTime? lastOfficialSlotUtc,
    required DateTime nowUtc,
    required DateTime cutoffUtc,
  }) {
    final start = shiftStartUtc.toUtc();
    final now = nowUtc.toUtc();
    final cutoff = cutoffUtc.toUtc();
    var cursor = lastOfficialSlotUtc != null
        ? lastOfficialSlotUtc.toUtc().add(length)
        : start;

    final slots = <DateTime>[];
    while (!cursor.isAfter(now) && cursor.isBefore(cutoff)) {
      slots.add(cursor);
      cursor = cursor.add(length);
    }
    return slots;
  }
}
