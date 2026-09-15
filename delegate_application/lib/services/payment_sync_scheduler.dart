import 'package:delegate_application/services/payment_sync_service.dart';
import 'package:delegate_application/utils/iraq_date.dart';
import 'package:flutter/widgets.dart';
import 'package:workmanager/workmanager.dart';

/// Android WorkManager bridge for payment upload after the 16:00 Baghdad gate.
///
/// Does not rely on an in-process Timer — works when the app is killed.
/// Network constraint is always required.
const String kPaymentSyncOneOffTask = 'delegate_payment_sync_gate';
const String kPaymentSyncPeriodicTask = 'delegate_payment_sync_periodic';
const String kPaymentSyncOneOffUnique = 'delegate_payment_sync_gate_unique';
const String kPaymentSyncPeriodicUnique = 'delegate_payment_sync_periodic_unique';

@pragma('vm:entry-point')
void paymentSyncCallbackDispatcher() {
  Workmanager().executeTask((task, inputData) async {
    WidgetsFlutterBinding.ensureInitialized();
    // Gate closed → leave pending untouched; no HTTP.
    if (!IraqDate.isPaymentSyncGateOpen()) {
      return true;
    }
    await PaymentSyncService.instance.syncPendingPayments();
    return true;
  });
}

class PaymentSyncScheduler {
  PaymentSyncScheduler._();
  static final PaymentSyncScheduler instance = PaymentSyncScheduler._();

  bool _initialized = false;

  Future<void> initialize() async {
    if (_initialized) return;
    await Workmanager().initialize(
      paymentSyncCallbackDispatcher,
      isInDebugMode: false,
    );
    _initialized = true;
    await ensureScheduled();
  }

  /// Schedule next 16:00 one-off + periodic retry with network required.
  Future<void> ensureScheduled({DateTime? utcNow}) async {
    if (!_initialized) {
      await initialize();
      return;
    }

    final delay = IraqDate.delayUntilPaymentSyncGate(utcNow);
    // When gate already open, nudge soon so pending flush after resume/boot.
    final oneOffDelay =
        delay == Duration.zero ? const Duration(seconds: 5) : delay;

    await Workmanager().registerOneOffTask(
      kPaymentSyncOneOffUnique,
      kPaymentSyncOneOffTask,
      initialDelay: oneOffDelay,
      constraints: Constraints(networkType: NetworkType.connected),
      existingWorkPolicy: ExistingWorkPolicy.replace,
      backoffPolicy: BackoffPolicy.exponential,
      backoffPolicyDelay: const Duration(minutes: 5),
    );

    await Workmanager().registerPeriodicTask(
      kPaymentSyncPeriodicUnique,
      kPaymentSyncPeriodicTask,
      frequency: const Duration(minutes: 15),
      constraints: Constraints(networkType: NetworkType.connected),
      existingWorkPolicy: ExistingWorkPolicy.keep,
      backoffPolicy: BackoffPolicy.exponential,
      backoffPolicyDelay: const Duration(minutes: 5),
    );
  }
}
