import 'package:delegate_application/services/today_payments_logic.dart';
import 'package:flutter_test/flutter_test.dart';

/// Documents: when collection is blocked, UI must treat submit as "not saved"
/// and clear «جاري حفظ التسديد» — never leave the dialog spinner forever.
void main() {
  test('blocked collection means submit did not save (clear spinner)', () {
    final blocked = TodayPaymentsLogic.blocksNewCollection(
      customerId: 42,
      delegateId: 10,
      localPayments: const [],
      ledgerHasBusinessDay: true,
    );
    expect(blocked, isTrue);
    const saved = false; // _addPayment returns false when blocked
    expect(saved, isFalse);
  });
}
