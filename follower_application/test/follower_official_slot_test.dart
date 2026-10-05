import 'package:follower_application/tracking/official_slot.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  group('OfficialSlot start+end', () {
    final start = DateTime.utc(2026, 9, 10, 7, 13, 27);
    final cutoff = start.add(const Duration(hours: 18));

    test('first due is start itself', () {
      final atStart = OfficialSlot.dueSlots(
        shiftStartUtc: start,
        lastOfficialSlotUtc: null,
        nowUtc: start,
        cutoffUtc: cutoff,
      );
      expect(atStart, [start]);
      expect(OfficialSlot.isExactOfficialSlot(start, start), isTrue);
      expect(
        OfficialSlot.isExactOfficialSlot(start, start.add(const Duration(minutes: 10))),
        isFalse,
      );
    });

    test('no second mid-shift slot after start recorded', () {
      final due = OfficialSlot.dueSlots(
        shiftStartUtc: start,
        lastOfficialSlotUtc: start,
        nowUtc: start.add(const Duration(minutes: 40)),
        cutoffUtc: cutoff,
      );
      expect(due, isEmpty);
      expect(
        OfficialSlot.nextDueUtc(
          shiftStartUtc: start,
          lastOfficialSlotUtc: start,
          cutoffUtc: cutoff,
        ),
        isNull,
      );
    });

    test('end slot matches closedAt', () {
      final closed = start.add(const Duration(hours: 8));
      expect(OfficialSlot.isEndSlot(closed, closed), isTrue);
      expect(
        OfficialSlot.isExactOfficialSlot(start, closed, closedAtUtc: closed),
        isTrue,
      );
      expect(
        OfficialSlot.isExactOfficialSlot(start, closed),
        isFalse,
      );
    });

    test('rejects old 10-minute mid slots', () {
      expect(
        OfficialSlot.isExactOfficialSlot(start, start.add(const Duration(minutes: 10))),
        isFalse,
      );
      expect(
        OfficialSlot.isExactOfficialSlot(start, start.add(const Duration(minutes: 20))),
        isFalse,
      );
    });
  });
}
