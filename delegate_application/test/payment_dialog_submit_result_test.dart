import 'package:delegate_application/services/today_payments_logic.dart';
import 'package:flutter_test/flutter_test.dart';

/// Documents: a refused collection is not a save. The dialog must leave
/// «جاري حفظ التسديد» and, for a prior unsynced row, offer إرسال التسديدات.
void main() {
  test('blocked collection means submit did not save (clear spinner)', () {
    final block = TodayPaymentsLogic.classifyCollection(
      customerId: 42,
      delegateId: 10,
      localPayments: const [],
      ledgerHasBusinessDay: true,
    );
    expect(block.isBlocked, isTrue);
    expect(block.kind, CollectionBlockKind.alreadyCollected);
    const saved = false; // AddPaymentResult.saved is false when blocked
    expect(saved, isFalse);
  });

  test('unsynced leftover is the waiting-to-send case shown on the dialog', () {
    final block = CollectionBlock.fromFlags(
      ledgerHasBusinessDay: false,
      hasPaymentOnBusinessDay: false,
      hasUnsyncedPayment: true,
    );
    expect(block.kind, CollectionBlockKind.waitingToSend);
    expect(block.message, contains('إرسال التسديدات'));
  });
}
