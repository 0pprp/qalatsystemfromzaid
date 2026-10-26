import 'package:delegate_application/services/payment_sync_service.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';

/// Exercises the real PaymentSyncService mutex (not a local fake).
void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  setUp(() async {
    SharedPreferences.setMockInitialValues({
      'LinkDelegate': 'http://example.test/api/',
      'AsyncId': 'test-async',
      'DelegateID': '1',
    });
    PaymentSyncService.instance.resetSyncTestHooks();
    PaymentSyncService.instance.isOnlineOverride = () async => false;
  });

  tearDown(() {
    PaymentSyncService.instance.resetSyncTestHooks();
  });

  test('concurrent syncPendingPayments enter sync body once', () async {
    // Gate open: 17:00 Baghdad = 14:00 UTC.
    final utcNow = DateTime.utc(2026, 10, 5, 14, 0, 0);

    await Future.wait([
      PaymentSyncService.instance.syncPendingPayments(utcNow: utcNow),
      PaymentSyncService.instance.syncPendingPayments(utcNow: utcNow),
      PaymentSyncService.instance.syncPendingPayments(utcNow: utcNow),
    ]);

    expect(PaymentSyncService.instance.syncBodyEnterCount, 1);
  });
}
