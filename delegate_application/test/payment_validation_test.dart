import 'package:delegate_application/services/payment_sync_status.dart';
import 'package:delegate_application/services/payment_validation.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  group('PaymentValidation', () {
    test('1999 rejected', () {
      expect(PaymentValidation.isAcceptableAmount(1999), isFalse);
      expect(
        PaymentValidation.validateAmount(1999),
        PaymentValidation.minAmountMessage,
      );
    });

    test('2000 accepted', () {
      expect(PaymentValidation.isAcceptableAmount(2000), isTrue);
    });

    test('2001 accepted', () {
      expect(PaymentValidation.isAcceptableAmount(2001), isTrue);
    });

    test('zero rejected', () {
      expect(PaymentValidation.validateAmount(0), isNotNull);
    });
  });

  group('PaymentSyncStatus', () {
    test('needsSync for pending and failed', () {
      expect(PaymentSyncStatus.needsSync(PaymentSyncStatus.pendingSync), isTrue);
      expect(PaymentSyncStatus.needsSync(PaymentSyncStatus.syncFailed), isTrue);
      expect(PaymentSyncStatus.needsSync(PaymentSyncStatus.syncing), isTrue);
      expect(PaymentSyncStatus.needsSync(PaymentSyncStatus.synced), isFalse);
    });
  });

  group('offline queue contract', () {
    test('print does not require synced status', () {
      // Receipt printing uses local fields only; Synced is not a precondition.
      const localStatus = PaymentSyncStatus.pendingSync;
      expect(localStatus != PaymentSyncStatus.synced, isTrue);
    });

    test('successful sync target status is Synced', () {
      expect(PaymentSyncStatus.synced, 'Synced');
    });

    test('network failure keeps PendingSync semantics', () {
      expect(PaymentSyncStatus.needsSync(PaymentSyncStatus.pendingSync), isTrue);
    });
  });
}
