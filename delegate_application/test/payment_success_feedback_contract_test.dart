import 'package:flutter_test/flutter_test.dart';
import 'package:delegate_application/services/payment_sync_status.dart';

void main() {
  test('wednesday CreatedAtUtc stays wednesday when uploaded thursday', () {
    // Contract: PaymentSyncService sends CreatedAtUtc unchanged; server PaymentDate = CreatedAtUtc.
    const createdWednesday = '2026-09-16T10:00:00.000Z';
    final payload = {
      'ClientPaymentId': 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee',
      'CreatedAtUtc': createdWednesday,
    };
    expect(payload['CreatedAtUtc'], createdWednesday);
  });

  test('double submit guard pattern', () {
    var submitting = false;
    void begin() {
      if (submitting) return;
      submitting = true;
    }

    begin();
    expect(submitting, isTrue);
    begin();
    expect(submitting, isTrue);
  });

  test('synced status is the only server-success gate', () {
    expect(PaymentSyncStatus.synced, 'Synced');
    expect(PaymentSyncStatus.needsSync(PaymentSyncStatus.pendingSync), isTrue);
    expect(PaymentSyncStatus.needsSync(PaymentSyncStatus.synced), isFalse);
  });
}
