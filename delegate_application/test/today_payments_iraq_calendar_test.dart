import 'package:delegate_application/services/today_payments_logic.dart';
import 'package:delegate_application/utils/iraq_date.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  group('Iraq calendar day for Today Payments', () {
    test('00:05 Iraq is same calendar day', () {
      // 2026-09-15 00:05 Baghdad = 2026-09-14 21:05 UTC
      final utcNow = DateTime.utc(2026, 9, 14, 21, 5);
      expect(IraqDate.calendarDateKey(utcNow), '2026-09-15');
      expect(
        IraqDate.isCreatedOnIraqCalendarDay(
          '2026-09-14T21:10:00.000Z',
          utcNow,
        ),
        isTrue,
      );
    });

    test('midday Iraq', () {
      final utcNow = DateTime.utc(2026, 9, 15, 9, 0); // 12:00 Baghdad
      expect(IraqDate.calendarDateKey(utcNow), '2026-09-15');
      expect(
        IraqDate.isCreatedOnIraqCalendarDay(
          '2026-09-15T08:00:00.000Z',
          utcNow,
        ),
        isTrue,
      );
    });

    test('18:45 Iraq (observed bug window)', () {
      // 18:45 Baghdad = 15:45 UTC
      final utcNow = DateTime.utc(2026, 9, 15, 15, 45);
      expect(IraqDate.calendarDateKey(utcNow), '2026-09-15');
      expect(
        IraqDate.isCreatedOnIraqCalendarDay(
          '2026-09-15T15:45:00.000Z',
          utcNow,
        ),
        isTrue,
      );
    });

    test('23:59 Iraq still same calendar day', () {
      final utcNow = DateTime.utc(2026, 9, 15, 20, 59); // 23:59 Baghdad
      expect(IraqDate.calendarDateKey(utcNow), '2026-09-15');
      expect(
        IraqDate.isCreatedOnIraqCalendarDay(
          '2026-09-15T20:50:00.000Z',
          utcNow,
        ),
        isTrue,
      );
    });

    test('UTC date differs from Iraq date near midnight', () {
      // 2026-09-15 01:00 Baghdad = 2026-09-14 22:00 UTC (UTC still 14th)
      final utcNow = DateTime.utc(2026, 9, 14, 22, 0);
      expect(IraqDate.calendarDateKey(utcNow), '2026-09-15');
      expect(
        IraqDate.isCreatedOnIraqCalendarDay(
          '2026-09-14T22:30:00.000Z',
          utcNow,
        ),
        isTrue,
      );
      // Previous Iraq calendar day payment must not match.
      expect(
        IraqDate.isCreatedOnIraqCalendarDay(
          '2026-09-14T20:00:00.000Z', // 23:00 Baghdad on Sep 14
          utcNow,
        ),
        isFalse,
      );
    });

    test('iraqCalendarDayUtcRange is [00:00, next 00:00) Baghdad in UTC', () {
      final utcNow = DateTime.utc(2026, 9, 15, 15, 45);
      final (start, end) = IraqDate.iraqCalendarDayUtcRange(utcNow);
      expect(start, DateTime.utc(2026, 9, 14, 21, 0));
      expect(end, DateTime.utc(2026, 9, 15, 21, 0));
    });
  });

  group('TodayPaymentsLogic eligibility + visibility', () {
    test('fully paid customer with today payment remains visible after refresh', () {
      final utcNow = DateTime.utc(2026, 9, 15, 15, 45); // 18:45 Baghdad
      final customers = [
        {
          'CustomerId': '10',
          'CustomerName': 'عميل كامل',
          'DelegateId': '1',
          'AmountRemaining': 0,
          'AmountDaySales': 13000,
          'LastPaymentDate': '2026-09-15',
          'DateSaleDevice': '2026-01-01',
        },
      ];
      final payments = [
        {
          'CustomerId': '10',
          'Amount': 13000,
          'CreatedAtUtc': '2026-09-15T15:45:00.000Z',
          'SyncStatus': 'Synced',
        },
      ];

      final rows = TodayPaymentsLogic.buildRows(
        customers: customers,
        localPayments: payments,
        delegateNames: {1: 'قائمة أ'},
        utcNow: utcNow,
      );

      expect(rows, hasLength(1));
      expect(rows.first.status, TodayPaymentStatus.paid);
      expect(rows.first.paidAmount, 13000);
    });

    test('payment appears exactly once for customer', () {
      final utcNow = DateTime.utc(2026, 9, 15, 12, 0);
      final customers = [
        {
          'CustomerId': '5',
          'CustomerName': 'عميل',
          'DelegateId': '1',
          'AmountRemaining': 5000,
          'AmountDaySales': 5000,
          'LastPaymentDate': '2026-09-14',
          'DateSaleDevice': '2026-01-01',
        },
      ];
      final payments = [
        {
          'CustomerId': '5',
          'Amount': 3000,
          'CreatedAtUtc': '2026-09-15T08:00:00.000Z',
        },
        {
          'CustomerId': '5',
          'Amount': 5000,
          'CreatedAtUtc': '2026-09-15T10:00:00.000Z',
        },
      ];
      final rows = TodayPaymentsLogic.buildRows(
        customers: customers,
        localPayments: payments,
        delegateNames: {1: 'قائمة'},
        utcNow: utcNow,
      );
      expect(rows, hasLength(1));
      expect(rows.first.paidAmount, 5000); // latest
    });

    test('other delegate list name does not leak customer rows across filters', () {
      final utcNow = DateTime.utc(2026, 9, 15, 12, 0);
      final customers = [
        {
          'CustomerId': '1',
          'CustomerName': 'أ',
          'DelegateId': '7',
          'AmountRemaining': 1000,
          'AmountDaySales': 1000,
          'LastPaymentDate': '2026-09-10',
          'DateSaleDevice': '2026-01-01',
        },
        {
          'CustomerId': '2',
          'CustomerName': 'ب',
          'DelegateId': '8',
          'AmountRemaining': 1000,
          'AmountDaySales': 1000,
          'LastPaymentDate': '2026-09-10',
          'DateSaleDevice': '2026-01-01',
        },
      ];
      final rows = TodayPaymentsLogic.buildRows(
        customers: customers,
        localPayments: const [],
        delegateNames: {7: 'قائمة 7', 8: 'قائمة 8'},
        utcNow: utcNow,
      );
      expect(rows, hasLength(2));
      expect(rows.map((r) => r.listName).toSet(), {'قائمة 7', 'قائمة 8'});
      expect(rows.where((r) => r.customerId == 1).single.listName, 'قائمة 7');
    });

    test('zero remaining without today payment is not eligible', () {
      expect(
        TodayPaymentsLogic.isEligibleForTodayCollection({
          'AmountRemaining': 0,
          'LastPaymentDate': '2026-09-15',
          'DateSaleDevice': '2026-01-01',
        }),
        isFalse,
      );
    });
  });

  test('TEST7: synced payment still counted (calendar day)', () {
    final utcNow = DateTime.utc(2026, 9, 14, 14, 0); // 17:00 Baghdad
    final payments = [
      {
        'CustomerId': '1',
        'Amount': 5000,
        'SyncStatus': 'Synced',
        'CreatedAtUtc': '2026-09-14T07:00:00.000Z',
      },
    ];
    final row = TodayPaymentsLogic.todayPaymentForCustomer(
      customerId: 1,
      localPayments: payments,
      utcNow: utcNow,
    );
    expect(row, isNotNull);
    expect(
      IraqDate.isCreatedOnIraqCalendarDay(row!['CreatedAtUtc']?.toString(), utcNow),
      isTrue,
    );
  });
}
