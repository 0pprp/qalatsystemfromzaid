import 'package:delegate_application/services/manual_payment_sync_result.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('complete success only when no unresolved remain', () {
    const ok = ManualPaymentSyncResult(
      outcome: ManualPaymentSyncOutcome.uploaded,
      uploadedCount: 3,
    );
    expect(ok.isCompleteSuccess, isTrue);

    const partial = ManualPaymentSyncResult(
      outcome: ManualPaymentSyncOutcome.partial,
      uploadedCount: 2,
      remainingRetryable: 1,
      remainingPending: 1,
      permanentFailures: 1,
    );
    expect(partial.isCompleteSuccess, isFalse);
    expect(partial.summaryMessage(), contains('قابل لإعادة المحاولة'));
    expect(partial.summaryMessage(), contains('مرفوض نهائياً'));
  });

  test('nothingPending with permanent failures is not silent success text', () {
    const r = ManualPaymentSyncResult(
      outcome: ManualPaymentSyncOutcome.nothingPending,
      permanentFailures: 2,
    );
    expect(r.summaryMessage(), contains('مرفوضاً'));
  });
}
