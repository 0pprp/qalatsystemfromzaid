import 'package:delegate_application/services/today_payments_logic.dart';
import 'package:delegate_application/utils/iraq_date.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  group('business-day reset @ 03:00 Baghdad for local payments', () {
    // Payment: 2026-09-14 20:00 Baghdad = 17:00 UTC
    const paymentUtc = '2026-09-14T17:00:00.000Z';

    test('before 03:00 still same business day — paid amount kept', () {
      // 2026-09-15 02:30 Baghdad = 2026-09-14 23:30 UTC
      final utcNow = DateTime.utc(2026, 9, 14, 23, 30);
      expect(IraqDate.businessDateKey(utcNow), '2026-09-14');
      expect(IraqDate.isCreatedOnIraqDay(paymentUtc, utcNow), isTrue);

      final row = TodayPaymentsLogic.todayPaymentForCustomer(
        customerId: 7,
        localPayments: [
          {'CustomerId': 7, 'Amount': 25000, 'CreatedAtUtc': paymentUtc},
        ],
        utcNow: utcNow,
      );
      expect(row, isNotNull);
      expect(row!['Amount'], 25000);
    });

    test('at 03:00 business day rolls — paid amount cleared for new day', () {
      // 2026-09-15 03:00 Baghdad = 2026-09-15 00:00 UTC
      final utcNow = DateTime.utc(2026, 9, 15, 0, 0);
      expect(IraqDate.businessDateKey(utcNow), '2026-09-15');
      expect(IraqDate.isCreatedOnIraqDay(paymentUtc, utcNow), isFalse);

      final row = TodayPaymentsLogic.todayPaymentForCustomer(
        customerId: 7,
        localPayments: [
          {'CustomerId': 7, 'Amount': 25000, 'CreatedAtUtc': paymentUtc},
        ],
        utcNow: utcNow,
      );
      expect(row, isNull);
    });

    test('same-customer lock is only for current business day', () {
      final utcNow = DateTime.utc(2026, 9, 15, 0, 0); // new biz day
      final existing = [
        {
          'CustomerId': 7,
          'DelegateId': 3,
          'Amount': 25000,
          'CreatedAtUtc': paymentUtc,
        },
        {
          'CustomerId': 7,
          'DelegateId': 3,
          'Amount': 1000,
          'CreatedAtUtc': '2026-09-15T01:00:00.000Z', // 04:00 Baghdad same new day
        },
      ];
      expect(
        TodayPaymentsLogic.hasPaymentOnBusinessDay(
          customerId: 7,
          delegateId: 3,
          localPayments: existing,
          utcNow: utcNow,
        ),
        isTrue,
      );
      expect(
        TodayPaymentsLogic.hasPaymentOnBusinessDay(
          customerId: 7,
          delegateId: 3,
          localPayments: [
            {
              'CustomerId': 7,
              'DelegateId': 3,
              'Amount': 25000,
              'CreatedAtUtc': paymentUtc,
            },
          ],
          utcNow: utcNow,
        ),
        isFalse,
      );
    });

    test('today totals use business day only (not lifetime sum)', () {
      final utcNow = DateTime.utc(2026, 9, 15, 10, 0); // 13:00 Baghdad
      final payments = [
        {
          'CustomerId': 1,
          'DelegateId': 3,
          'Amount': 25000,
          'CreatedAtUtc': paymentUtc, // previous biz day
        },
        {
          'CustomerId': 2,
          'DelegateId': 3,
          'Amount': 5000,
          'CreatedAtUtc': '2026-09-15T05:00:00.000Z', // today biz
        },
      ];
      final today = TodayPaymentsLogic.filterPaymentsOnBusinessDay(
        payments,
        utcNow: utcNow,
      );
      expect(today.length, 1);
      expect(today.first['Amount'], 5000);
      expect(
        TodayPaymentsLogic.sumAmounts(today),
        5000,
      );
    });
  });
}
