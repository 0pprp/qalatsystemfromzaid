import 'package:delegate_application/services/today_payments_logic.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  group('blocksNewCollection', () {
    test('blocks when unsynced leftover from prior day exists', () {
      final existing = [
        {
          'CustomerId': 7,
          'DelegateId': 3,
          'Amount': 25000,
          'CreatedAtUtc': '2026-09-14T17:00:00.000Z',
          'SyncStatus': 'PendingSync',
        },
      ];
      // New business day 2026-09-15 13:00 Baghdad = 10:00 UTC
      final utcNow = DateTime.utc(2026, 9, 15, 10, 0);
      expect(
        TodayPaymentsLogic.blocksNewCollection(
          customerId: 7,
          delegateId: 3,
          localPayments: existing,
          utcNow: utcNow,
        ),
        isTrue,
      );
    });

    test('blocks when ledger already has business day', () {
      expect(
        TodayPaymentsLogic.blocksNewCollection(
          customerId: 7,
          delegateId: 3,
          localPayments: const [],
          ledgerHasBusinessDay: true,
        ),
        isTrue,
      );
    });

    test('allows when no local and no ledger', () {
      expect(
        TodayPaymentsLogic.blocksNewCollection(
          customerId: 7,
          delegateId: 3,
          localPayments: const [],
          ledgerHasBusinessDay: false,
        ),
        isFalse,
      );
    });
  });
}
