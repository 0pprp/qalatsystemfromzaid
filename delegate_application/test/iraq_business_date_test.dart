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

  group('IraqDate payment sync gate 16:00 (independent of 03:00)', () {
    test('15:59:59 Baghdad — gate CLOSED (no upload)', () {
      // 15:59:59 Baghdad = 12:59:59 UTC
      final utc = DateTime.utc(2026, 9, 14, 12, 59, 59);
      expect(IraqDate.isPaymentSyncGateOpen(utc), isFalse);
    });

    test('16:00:00 Baghdad — gate OPEN', () {
      // 16:00 Baghdad = 13:00 UTC
      final utc = DateTime.utc(2026, 9, 14, 13, 0, 0);
      expect(IraqDate.isPaymentSyncGateOpen(utc), isTrue);
    });

    test('10:00 Baghdad — gate CLOSED', () {
      final utc = DateTime.utc(2026, 9, 14, 7, 0);
      expect(IraqDate.isPaymentSyncGateOpen(utc), isFalse);
    });

    test('02:30 Baghdad — gate CLOSED (even though previous business day)', () {
      // 02:30 Baghdad next calendar = previous biz day, but sync gate still closed
      final utc = DateTime.utc(2026, 9, 14, 23, 30); // Sep 15 02:30 Baghdad
      expect(IraqDate.isPaymentSyncGateOpen(utc), isFalse);
      expect(IraqDate.businessDateKey(utc), '2026-09-14');
    });

    test('delayUntilPaymentSyncGate is zero when open', () {
      final utc = DateTime.utc(2026, 9, 14, 14, 0); // 17:00 Baghdad
      expect(IraqDate.delayUntilPaymentSyncGate(utc), Duration.zero);
    });

    test('delayUntilPaymentSyncGate positive before 16:00', () {
      final utc = DateTime.utc(2026, 9, 14, 10, 0); // 13:00 Baghdad
      final d = IraqDate.delayUntilPaymentSyncGate(utc);
      expect(d.inHours, 3);
    });
  });

  group('IraqDate.isCreatedOnIraqDay business boundary', () {
    test('payment at 02:30 Thu counts as Wednesday business day', () {
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
