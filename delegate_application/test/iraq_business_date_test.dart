import 'package:delegate_application/utils/iraq_date.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  group('IraqDate.businessDate', () {
    test('02:59 Baghdad stays previous calendar day', () {
      // 2026-09-17 02:59 Baghdad = 2026-09-16 23:59 UTC
      final utc = DateTime.utc(2026, 9, 16, 23, 59);
      expect(IraqDate.businessDateKey(utc), '2026-09-16');
    });

    test('03:00 Baghdad starts new business day', () {
      // 2026-09-17 03:00 Baghdad = 2026-09-17 00:00 UTC
      final utc = DateTime.utc(2026, 9, 17, 0, 0);
      expect(IraqDate.businessDateKey(utc), '2026-09-17');
    });

    test('03:01 Baghdad is new business day', () {
      final utc = DateTime.utc(2026, 9, 17, 0, 1);
      expect(IraqDate.businessDateKey(utc), '2026-09-17');
    });

    test('23:59 Baghdad same business day', () {
      // 2026-09-17 23:59 Baghdad = 2026-09-17 20:59 UTC
      final utc = DateTime.utc(2026, 9, 17, 20, 59);
      expect(IraqDate.businessDateKey(utc), '2026-09-17');
    });
  });

  group('IraqDate.isCreatedOnIraqDay business boundary', () {
    test('payment at 02:30 Thu counts as Wednesday business day', () {
      // Payment Wed business: Thu 02:30 Baghdad = Wed 23:30 UTC previous calendar
      final paymentUtc = DateTime.utc(2026, 9, 16, 23, 30); // Thu 02:30 Baghdad
      final nowUtc = DateTime.utc(2026, 9, 16, 22, 0); // Thu 01:00 Baghdad → still Wed biz
      expect(IraqDate.isCreatedOnIraqDay(paymentUtc.toIso8601String(), nowUtc), isTrue);
    });

    test('after 03:00 Thu, Wednesday payment is not today', () {
      final wednesdayPayment = DateTime.utc(2026, 9, 16, 10, 0); // Wed 13:00 Baghdad
      final thursdayMorning = DateTime.utc(2026, 9, 17, 0, 30); // Thu 03:30 Baghdad
      expect(
        IraqDate.isCreatedOnIraqDay(
            wednesdayPayment.toIso8601String(), thursdayMorning),
        isFalse,
      );
    });
  });

  group('rollover idempotency contract', () {
    test('same business key is stable across repeated reads', () {
      final utc = DateTime.utc(2026, 9, 17, 5, 0);
      expect(IraqDate.businessDateKey(utc), IraqDate.businessDateKey(utc));
    });
  });
}
