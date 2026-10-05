import 'package:delegate_application/services/payment_local_retention.dart';
import 'package:delegate_application/services/payment_sync_status.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  // Yesterday evening Baghdad ≈ 2026-09-14 20:00 = 17:00 UTC
  const yesterdaySyncedUtc = '2026-09-14T17:00:00.000Z';
  // Today after 03:00 Baghdad ≈ 2026-09-15 10:00 = 07:00 UTC
  const todayPendingUtc = '2026-09-15T07:00:00.000Z';
  // New business day @ 03:00 Baghdad = 2026-09-15 00:00 UTC
  final utcNow = DateTime.utc(2026, 9, 15, 0, 0);

  group('synced purge at 03:00', () {
    test('deletes Synced outside current business day', () {
      final rows = [
        {
          'id': 1,
          'CustomerId': 7,
          'Amount': 25000,
          'CreatedAtUtc': yesterdaySyncedUtc,
          'SyncStatus': PaymentSyncStatus.synced,
          'ClientPaymentId': 'old-synced',
        },
      ];
      expect(
        PaymentLocalRetention.syncedOutsideBusinessDayRowIdsToDelete(
          rows,
          utcNow: utcNow,
        ),
        [1],
      );
    });

    test('keeps PendingSync from previous business day', () {
      final rows = [
        {
          'id': 2,
          'CustomerId': 7,
          'Amount': 25000,
          'CreatedAtUtc': yesterdaySyncedUtc,
          'SyncStatus': PaymentSyncStatus.pendingSync,
          'ClientPaymentId': 'old-pending',
        },
      ];
      expect(
        PaymentLocalRetention.syncedOutsideBusinessDayRowIdsToDelete(
          rows,
          utcNow: utcNow,
        ),
        isEmpty,
      );
    });

    test('keeps SyncFailed from previous business day', () {
      final rows = [
        {
          'id': 3,
          'CustomerId': 8,
          'Amount': 10000,
          'CreatedAtUtc': yesterdaySyncedUtc,
          'SyncStatus': PaymentSyncStatus.syncFailed,
          'ClientPaymentId': 'old-failed',
        },
      ];
      expect(
        PaymentLocalRetention.syncedOutsideBusinessDayRowIdsToDelete(
          rows,
          utcNow: utcNow,
        ),
        isEmpty,
      );
    });

    test('keeps Synced on current business day', () {
      final rows = [
        {
          'id': 4,
          'CustomerId': 9,
          'Amount': 25000,
          'CreatedAtUtc': todayPendingUtc,
          'SyncStatus': PaymentSyncStatus.synced,
          'ClientPaymentId': 'today-synced',
        },
      ];
      // At 03:00 new day — todayPendingUtc is same new business day.
      expect(
        PaymentLocalRetention.syncedOutsideBusinessDayRowIdsToDelete(
          rows,
          utcNow: DateTime.utc(2026, 9, 15, 8, 0),
        ),
        isEmpty,
      );
    });
  });

  group('ClientPaymentId dedup (safe)', () {
    test('same Amount different ClientPaymentId are NOT deleted', () {
      final rows = [
        {
          'id': 10,
          'CustomerId': 7,
          'CustomerName': 'A',
          'DelegateId': 3,
          'DelegateName': 'D',
          'Amount': 25000,
          'CreatedAtUtc': yesterdaySyncedUtc,
          'ClientPaymentId': 'uuid-old',
        },
        {
          'id': 11,
          'CustomerId': 7,
          'CustomerName': 'A',
          'DelegateId': 3,
          'DelegateName': 'D',
          'Amount': 25000,
          'CreatedAtUtc': todayPendingUtc,
          'ClientPaymentId': 'uuid-new',
        },
      ];
      // Legacy Amount GROUP BY would have deleted id 11 — must not happen.
      expect(
        PaymentLocalRetention.duplicateClientPaymentIdRowIdsToDelete(rows),
        isEmpty,
      );
    });

    test('exact duplicate ClientPaymentId keeps min id', () {
      final rows = [
        {
          'id': 20,
          'ClientPaymentId': 'same-uuid',
          'Amount': 25000,
        },
        {
          'id': 21,
          'ClientPaymentId': 'same-uuid',
          'Amount': 25000,
        },
      ];
      expect(
        PaymentLocalRetention.duplicateClientPaymentIdRowIdsToDelete(rows),
        [21],
      );
    });
  });
}
