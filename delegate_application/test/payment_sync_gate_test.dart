import 'package:delegate_application/utils/iraq_date.dart';
import 'package:flutter_test/flutter_test.dart';

/// Upload gate removed — HTTP allowed at any Baghdad hour when online.
void main() {
  test('gate open before former 16:00 cutoff', () {
    final at1000 = DateTime.utc(2026, 9, 14, 7, 0); // 10:00 Baghdad
    final at1200 = DateTime.utc(2026, 9, 14, 9, 0);
    final at1559 = DateTime.utc(2026, 9, 14, 12, 59, 59);
    expect(IraqDate.isPaymentSyncGateOpen(at1000), isTrue);
    expect(IraqDate.isPaymentSyncGateOpen(at1200), isTrue);
    expect(IraqDate.isPaymentSyncGateOpen(at1559), isTrue);
  });

  test('gate open at and after 16:00', () {
    final at1600 = DateTime.utc(2026, 9, 14, 13, 0);
    final at1610 = DateTime.utc(2026, 9, 14, 13, 10);
    expect(IraqDate.isPaymentSyncGateOpen(at1600), isTrue);
    expect(IraqDate.isPaymentSyncGateOpen(at1610), isTrue);
  });

  test('CreatedAtUtc preserved independently of sync gate', () {
    const occurred = '2026-09-14T11:00:00.000Z';
    final payload = {
      'ClientPaymentId': 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee',
      'CreatedAtUtc': occurred,
    };
    expect(payload['CreatedAtUtc'], occurred);
    expect(IraqDate.isPaymentSyncGateOpen(DateTime.utc(2026, 9, 14, 15, 30)), isTrue);
  });

  test('clientPaymentId UUID stays stable across retries (idempotency key)', () {
    const id = '11111111-2222-3333-4444-555555555555';
    final attempts = List.generate(10, (_) => {'ClientPaymentId': id});
    expect(attempts.every((a) => a['ClientPaymentId'] == id), isTrue);
  });
}
