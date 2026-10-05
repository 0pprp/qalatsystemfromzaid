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
      IraqDate.isCreatedOnIraqDay(row['CreatedAtUtc']?.toString(), utcNow),
      isTrue,
    );
  });

  test('TEST8/9: 02:59 previous biz day; 03:00 new counters boundary (legacy)', () {
    final at0259 = DateTime.utc(2026, 9, 14, 23, 59); // Sep 15 02:59 Baghdad
    final at0300 = DateTime.utc(2026, 9, 15, 0, 0); // Sep 15 03:00 Baghdad
    expect(IraqDate.businessDateKey(at0259), '2026-09-14');
    expect(IraqDate.businessDateKey(at0300), '2026-09-15');
  });

  test('print strip uses local ledger after sync wipe (not server arrival)', () {
    final utcNow = DateTime.utc(2026, 9, 14, 14, 0);
    // Queue empty after successful upload deleted the row.
    final queue = <Map<String, dynamic>>[];
    final ledger = [
      {
        'CustomerId': 7,
        'DelegateId': 3,
        'Amount': 800000,
        'CreatedAtUtc': '2026-09-14T08:00:00.000Z',
        'ClientPaymentId': 'uuid-7',
      },
    ];
    final merged = TodayPaymentsLogic.mergeLocalDayPayments(
      localPayments: queue,
      ledgerRows: ledger,
      customerNames: {7: 'زبون تجريبي'},
      utcNow: utcNow,
    );
    expect(merged.length, 1);
    expect(merged.first['CustomerId'], 7);
    expect(merged.first['CustomerName'], 'زبون تجريبي');
    expect(merged.first['Amount'], 800000);
    expect(merged.first['SyncStatus'], 'Synced');
    expect(TodayPaymentsLogic.sumAmounts(merged), 800000);
  });

  test('merge prefers live queue row over duplicate ledger mark', () {
    final utcNow = DateTime.utc(2026, 9, 14, 14, 0);
    final queue = [
      {
        'CustomerId': 7,
        'DelegateId': 3,
        'Amount': 5000,
        'CustomerName': 'من الطابور',
        'SyncStatus': 'PendingSync',
        'CreatedAtUtc': '2026-09-14T08:00:00.000Z',
      },
    ];
    final ledger = [
      {
        'CustomerId': 7,
        'DelegateId': 3,
        'Amount': 5000,
        'CreatedAtUtc': '2026-09-14T08:00:00.000Z',
      },
    ];
    final merged = TodayPaymentsLogic.mergeLocalDayPayments(
      localPayments: queue,
      ledgerRows: ledger,
      utcNow: utcNow,
    );
    expect(merged.length, 1);
    expect(merged.first['CustomerName'], 'من الطابور');
    expect(merged.first['SyncStatus'], 'PendingSync');
  });
}
