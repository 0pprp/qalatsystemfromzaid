import 'package:delegate_application/utils/iraq_date.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  group('delayUntilNextBusinessDayStart (03:00 Baghdad)', () {
    test('at 02:30 Baghdad waits until same calendar 03:00', () {
      // 2026-09-15 02:30 Baghdad = 2026-09-14 23:30 UTC
      final utcNow = DateTime.utc(2026, 9, 14, 23, 30);
      final d = IraqDate.delayUntilNextBusinessDayStart(utcNow);
      expect(d, const Duration(minutes: 30));
    });

    test('exactly at 03:00 Baghdad waits a full day until next 03:00', () {
      // 2026-09-15 03:00 Baghdad = 2026-09-15 00:00 UTC
      final utcNow = DateTime.utc(2026, 9, 15);
      final d = IraqDate.delayUntilNextBusinessDayStart(utcNow);
      expect(d, const Duration(hours: 24));
    });

    test('at 10:00 Baghdad waits until next calendar 03:00', () {
      // 2026-09-15 10:00 Baghdad = 2026-09-15 07:00 UTC
      final utcNow = DateTime.utc(2026, 9, 15, 7);
      final d = IraqDate.delayUntilNextBusinessDayStart(utcNow);
      // until 2026-09-16 03:00 Baghdad = 2026-09-16 00:00 UTC → 17h
      expect(d, const Duration(hours: 17));
    });
  });

  group('stale in-memory day totals must clear after business-day roll', () {
    test('yesterday payment is excluded after 03:00 even if list never deleted', () {
      const paymentUtc = '2026-09-14T17:00:00.000Z'; // 20:00 Baghdad
      final before = DateTime.utc(2026, 9, 14, 23, 30); // 02:30 next cal day
      final after = DateTime.utc(2026, 9, 15); // 03:00 Baghdad
      final rows = [
        {'CustomerId': 1, 'DelegateId': 9, 'Amount': 40000, 'CreatedAtUtc': paymentUtc},
      ];

      expect(
        IraqDate.isCreatedOnIraqDay(paymentUtc, before),
        isTrue,
      );
      expect(
        IraqDate.isCreatedOnIraqDay(paymentUtc, after),
        isFalse,
      );

      final beforeSum = rows
          .where((p) => IraqDate.isCreatedOnIraqDay(p['CreatedAtUtc']?.toString(), before))
          .fold<double>(0, (s, p) => s + (p['Amount'] as num).toDouble());
      final afterSum = rows
          .where((p) => IraqDate.isCreatedOnIraqDay(p['CreatedAtUtc']?.toString(), after))
          .fold<double>(0, (s, p) => s + (p['Amount'] as num).toDouble());

      expect(beforeSum, 40000);
      expect(afterSum, 0);
    });
  });
}
