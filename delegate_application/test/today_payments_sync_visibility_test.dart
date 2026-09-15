import 'package:delegate_application/services/today_payments_logic.dart';
import 'package:delegate_application/utils/iraq_date.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('TEST7: synced payment still counted in daily UI (visibility ≠ sync status)', () {
    final utcNow = DateTime.utc(2026, 9, 14, 14, 0); // 17:00 Baghdad
    final payments = [
      {
        'CustomerId': '1',
        'Amount': 5000,
        'SyncStatus': 'Synced',
        'CreatedAtUtc': '2026-09-14T07:00:00.000Z', // 10:00 Baghdad same calendar day
      },
    ];
    final row = TodayPaymentsLogic.todayPaymentForCustomer(
      customerId: 1,
      localPayments: payments,
      utcNow: utcNow,
    );
    expect(row, isNotNull);
    expect(row!['SyncStatus'], 'Synced');
    expect(
      IraqDate.isCreatedOnIraqCalendarDay(row['CreatedAtUtc']?.toString(), utcNow),
      isTrue,
    );
  });

  test('TEST8/9: 02:59 previous biz day; 03:00 new counters boundary (legacy)', () {
    final at0259 = DateTime.utc(2026, 9, 14, 23, 59); // Sep 15 02:59 Baghdad
    final at0300 = DateTime.utc(2026, 9, 15, 0, 0); // Sep 15 03:00 Baghdad
    expect(IraqDate.businessDateKey(at0259), '2026-09-14');
    expect(IraqDate.businessDateKey(at0300), '2026-09-15');
  });
}
