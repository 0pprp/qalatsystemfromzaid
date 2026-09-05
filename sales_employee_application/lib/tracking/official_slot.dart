/// Official manager pins: one 10-minute Iraq-local slot. Timestamp is the slot, not GPS time.
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

  static int sequence(DateTime slotUtc) =>
      slotUtc.toUtc().millisecondsSinceEpoch ~/ length.inMilliseconds;

  static List<DateTime> dueSlots({
    required DateTime shiftStartUtc,
    DateTime? lastOfficialSlotUtc,
    required DateTime nowUtc,
    required DateTime cutoffUtc,
  }) {
    final start = shiftStartUtc.toUtc();
    final now = nowUtc.toUtc();
    final cutoff = cutoffUtc.toUtc();
    final first = floorUtc(start);
    var cursor = lastOfficialSlotUtc != null
        ? floorUtc(lastOfficialSlotUtc.toUtc()).add(length)
        : first;
    if (cursor.isBefore(first)) {
      cursor = first;
    }

    final slots = <DateTime>[];
    while (!cursor.isAfter(now) && cursor.isBefore(cutoff)) {
      slots.add(cursor);
      cursor = cursor.add(length);
    }
    return slots;
  }
}
