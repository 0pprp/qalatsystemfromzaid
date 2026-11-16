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

    test('prior-day pending is waiting-to-send, not already collected', () {
      final existing = [
        {
          'CustomerId': 7,
          'DelegateId': 3,
          'Amount': 20000,
          'CreatedAtUtc': '2026-09-14T17:00:00.000Z',
          'SyncStatus': 'PendingSync',
        },
      ];
      final utcNow = DateTime.utc(2026, 9, 15, 10, 0);
      final block = TodayPaymentsLogic.classifyCollection(
        customerId: 7,
        delegateId: 3,
        localPayments: existing,
        utcNow: utcNow,
      );
      expect(block.kind, CollectionBlockKind.waitingToSend);
      expect(block.message, contains(CollectionBlock.waitingToSendMessage));
      expect(block.message, contains('20,000'));
      expect(block.message, contains('PendingSync'));
      expect(block.message, contains('بانتظار الإرسال'));
      expect(block.message, contains('2026-09-14T17:00:00.000Z'));
    });

    test('payment older than 30 days does not block a new collection', () {
      final existing = [
        {
          'CustomerId': 7,
          'DelegateId': 3,
          'Amount': 20000,
          'CreatedAtUtc': '2026-08-01T10:00:00.000Z',
          'SyncStatus': 'PendingSync',
        },
      ];
      final utcNow = DateTime.utc(2026, 9, 15, 10, 0);
      final block = TodayPaymentsLogic.classifyCollection(
        customerId: 7,
        delegateId: 3,
        localPayments: existing,
        utcNow: utcNow,
      );
      expect(block.kind, CollectionBlockKind.none);
      expect(block.isBlocked, isFalse);
    });

    test('terminal reject does not block even when the row is recent', () {
      final existing = [
        {
          'CustomerId': 7,
          'DelegateId': 3,
          'Amount': 20000,
          'CreatedAtUtc': '2026-09-14T17:00:00.000Z',
          'SyncStatus': 'SyncFailed',
          'PermanentFailure': 1,
          'SyncError': 'وقت إنشاء التسديد قديم جدًا',
        },
      ];
      final utcNow = DateTime.utc(2026, 9, 15, 10, 0);
      expect(
        TodayPaymentsLogic.blocksNewCollection(
          customerId: 7,
          delegateId: 3,
          localPayments: existing,
          utcNow: utcNow,
        ),
        isFalse,
      );
    });

    test('ledger hit is already collected even if an unsynced row exists', () {
      final block = CollectionBlock.fromFlags(
        ledgerHasBusinessDay: true,
        hasPaymentOnBusinessDay: false,
        hasUnsyncedPayment: true,
      );
      expect(block.kind, CollectionBlockKind.alreadyCollected);
      expect(block.isBlocked, isTrue);
    });
  });
}
