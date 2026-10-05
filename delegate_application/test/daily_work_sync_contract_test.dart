import 'package:delegate_application/services/payment_daily_work_debug.dart';
import 'package:delegate_application/services/today_payments_logic.dart';
import 'package:delegate_application/utils/iraq_date.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  group('send window [16:00 → 02:59] Baghdad — prevents past-day inflation', () {
    test('before 16:00 — CLOSED', () {
      expect(
        IraqDate.isPaymentSyncGateOpen(DateTime.utc(2026, 9, 14, 12, 59, 59)),
        isFalse,
      );
    });

    test('16:00 — OPEN', () {
      expect(
        IraqDate.isPaymentSyncGateOpen(DateTime.utc(2026, 9, 14, 13, 0)),
        isTrue,
      );
    });

    test('01:00 next calendar (still previous biz day) — OPEN (was the bug)', () {
      // Sep 15 01:00 Baghdad = Sep 14 22:00 UTC → business day still Sep 14
      final at0100 = DateTime.utc(2026, 9, 14, 22, 0);
      expect(IraqDate.businessDateKey(at0100), '2026-09-14');
      expect(IraqDate.isPaymentSyncGateOpen(at0100), isTrue);
      expect(PaymentDailyWorkDebug.midnightGapWouldBlockSend(at0100), isFalse);
    });

    test('02:59 — OPEN last minute of business day', () {
      final at0259 = DateTime.utc(2026, 9, 14, 23, 59);
      expect(IraqDate.businessDateKey(at0259), '2026-09-14');
      expect(IraqDate.isPaymentSyncGateOpen(at0259), isTrue);
    });

    test('03:00 new day — CLOSED until 16:00', () {
      final at0300 = DateTime.utc(2026, 9, 15, 0, 0);
      expect(IraqDate.businessDateKey(at0300), '2026-09-15');
      expect(IraqDate.isPaymentSyncGateOpen(at0300), isFalse);
    });
  });

  group('03:00 UI zero — prior day does not count as today', () {
    test('day-27 payment not in day-28 UI', () {
      final collected = DateTime.utc(2026, 9, 27, 10, 0);
      final day28 = DateTime.utc(2026, 9, 28, 0, 30);
      expect(
        PaymentDailyWorkDebug.countsInTodayUi(
          collectedUtc: collected,
          nowUtc: day28,
        ),
        isFalse,
      );
    });
  });

  group('local strip after sync wipe', () {
    test('ledger keeps paid print after queue delete', () {
      final utcNow = DateTime.utc(2026, 9, 14, 14, 0);
      final ok = PaymentDailyWorkDebug.paidStripSurvivesSyncWipe(
        queueAfterWipe: const [],
        ledger: [
          {
            'CustomerId': 1,
            'DelegateId': 2,
            'Amount': 800000,
            'CreatedAtUtc': '2026-09-14T08:00:00.000Z',
          },
        ],
        utcNow: utcNow,
      );
      expect(ok, isTrue);
      final merged = TodayPaymentsLogic.mergeLocalDayPayments(
        localPayments: const [],
        ledgerRows: [
          {
            'CustomerId': 1,
            'DelegateId': 2,
            'Amount': 800000,
            'CreatedAtUtc': '2026-09-14T08:00:00.000Z',
          },
        ],
        customerNames: {1: 'A'},
        utcNow: utcNow,
      );
      expect(merged.length, 1);
      expect(TodayPaymentsLogic.sumAmounts(merged), 800000);
    });
  });
}
