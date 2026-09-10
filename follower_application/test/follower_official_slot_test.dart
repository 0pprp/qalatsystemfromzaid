import 'package:flutter_test/flutter_test.dart';
import 'package:follower_application/tracking/official_slot.dart';
import 'package:follower_application/tracking/tracking_config.dart';

void main() {
  test('official tracking interval is 10 minutes', () {
    expect(TrackingConfig.officialInterval, const Duration(minutes: 10));
    expect(TrackingConfig.officialIntervalMs, 600000);
    expect(TrackingConfig.maxAcceptedAccuracyMeters, greaterThanOrEqualTo(50));
    expect(TrackingConfig.minimumDistanceMeters, 0);
    expect(TrackingConfig.debugIntervalMs, 0);
  });

  test('start shift: no point at start; first due at start+10min', () {
    final start = DateTime.utc(2026, 9, 2, 7, 13);
    final atStart = OfficialSlot.dueSlots(
      shiftStartUtc: start,
      lastOfficialSlotUtc: null,
      nowUtc: start,
      cutoffUtc: start.add(const Duration(hours: 18)),
    );
    expect(atStart, isEmpty);

    expect(
      OfficialSlot.dueSlots(
        shiftStartUtc: start,
        lastOfficialSlotUtc: null,
        nowUtc: start.add(const Duration(minutes: 8)),
        cutoffUtc: start.add(const Duration(hours: 18)),
      ),
      isEmpty,
    );
    expect(
      OfficialSlot.dueSlots(
        shiftStartUtc: start,
        lastOfficialSlotUtc: null,
        nowUtc: start.add(const Duration(minutes: 9, seconds: 59)),
        cutoffUtc: start.add(const Duration(hours: 18)),
      ),
      isEmpty,
    );
    expect(OfficialSlot.isExactOfficialSlot(start, start.add(const Duration(minutes: 8))), isFalse);
    expect(
      OfficialSlot.isExactOfficialSlot(start, start.add(const Duration(minutes: 9, seconds: 59))),
      isFalse,
    );

    final firstDue = OfficialSlot.dueSlots(
      shiftStartUtc: start,
      lastOfficialSlotUtc: null,
      nowUtc: start.add(const Duration(minutes: 10)),
      cutoffUtc: start.add(const Duration(hours: 18)),
    );
    expect(firstDue, [start.add(const Duration(minutes: 10))]);
    expect(OfficialSlot.isExactOfficialSlot(start, firstDue.first), isTrue);
    expect(firstDue.first, isNot(OfficialSlot.floorUtc(start)));
  });

  test('wake at 10:25 maps to official 10:23; next official 10:33', () {
    final start = DateTime.utc(2026, 9, 2, 7, 13);
    final due = OfficialSlot.dueSlots(
      shiftStartUtc: start,
      lastOfficialSlotUtc: null,
      nowUtc: start.add(const Duration(minutes: 12)),
      cutoffUtc: start.add(const Duration(hours: 18)),
    );
    expect(due, [start.add(const Duration(minutes: 10))]);
    expect(
      OfficialSlot.nextDueUtc(
        shiftStartUtc: start,
        lastOfficialSlotUtc: due.first,
        cutoffUtc: start.add(const Duration(hours: 18)),
      ),
      start.add(const Duration(minutes: 20)),
    );
  });

  test('ten minutes later a new route point is due without movement', () {
    final start = DateTime.utc(2026, 9, 2, 8, 0);
    final due = OfficialSlot.dueSlots(
      shiftStartUtc: start,
      lastOfficialSlotUtc: start.add(const Duration(minutes: 10)),
      nowUtc: start.add(const Duration(minutes: 20)),
      cutoffUtc: start.add(const Duration(hours: 18)),
    );
    expect(due, [DateTime.utc(2026, 9, 2, 8, 20)]);
  });

  test('end before first slot yields no due points', () {
    final start = DateTime.utc(2026, 9, 2, 7, 13);
    final due = OfficialSlot.dueSlots(
      shiftStartUtc: start,
      lastOfficialSlotUtc: null,
      nowUtc: start.add(const Duration(minutes: 5)),
      cutoffUtc: start.add(const Duration(minutes: 5)),
    );
    expect(due, isEmpty);
  });

  test('restart uses original shiftStart cadence not restart time', () {
    final start = DateTime.utc(2026, 9, 2, 7, 13);
    final last = start.add(const Duration(minutes: 10));
    final due = OfficialSlot.dueSlots(
      shiftStartUtc: start,
      lastOfficialSlotUtc: last,
      nowUtc: start.add(const Duration(minutes: 35)),
      cutoffUtc: start.add(const Duration(hours: 18)),
    );
    expect(due, [
      start.add(const Duration(minutes: 20)),
      start.add(const Duration(minutes: 30)),
    ]);
    expect(
      OfficialSlot.nextDueUtc(
        shiftStartUtc: start,
        lastOfficialSlotUtc: last,
        cutoffUtc: start.add(const Duration(hours: 18)),
      ),
      start.add(const Duration(minutes: 20)),
    );
  });

  test('Iraq display UTC 18:40 is Baghdad 21:40', () {
    final utc = DateTime.utc(2026, 9, 2, 18, 40);
    final iraq = utc.add(OfficialSlot.iraqOffset);
    expect(iraq.hour, 21);
    expect(iraq.minute, 40);
  });
}
