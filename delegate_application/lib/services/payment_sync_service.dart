import 'dart:async';
import 'dart:convert';

import 'package:connectivity_plus/connectivity_plus.dart';
import 'package:delegate_application/services/DatabaseHelper.dart';
import 'package:delegate_application/services/delegate_data_refresh_service.dart';
import 'package:delegate_application/services/payment_sync_scheduler.dart';
import 'package:delegate_application/services/payment_sync_status.dart';
import 'package:delegate_application/services/payment_validation.dart';
import 'package:delegate_application/utils/iraq_date.dart';
import 'package:flutter/widgets.dart';
import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';

/// Offline-first auto-sync for collection payments.
///
/// CRITICAL: HTTP upload is gated by [IraqDate.isPaymentSyncGateOpen]
/// (16:00 Asia/Baghdad). Before the gate, payments stay `pending` locally —
/// normal customer/data sync may still run elsewhere.
/// Never deletes a local row until the server acknowledges success.
class PaymentSyncService with WidgetsBindingObserver {
  PaymentSyncService._();
  static final PaymentSyncService instance = PaymentSyncService._();

  final Connectivity _connectivity = Connectivity();
  StreamSubscription<List<ConnectivityResult>>? _connectivitySub;
  Completer<void>? _syncLock;
  bool _started = false;
  bool _wasOffline = true;
  bool _uploadedAnyThisRun = false;

  /// Last sync outcome for diagnostics / UI (not persisted).
  bool lastSkippedDueToGate = false;

  Future<void> start() async {
    if (_started) return;
    _started = true;
    WidgetsBinding.instance.addObserver(this);
    try {
      await PaymentSyncScheduler.instance.initialize();
    } catch (_) {
      // Scheduler failure must never block app start; in-process triggers remain.
    }
    _connectivitySub =
        _connectivity.onConnectivityChanged.listen((results) async {
      final online = results.any((r) => r != ConnectivityResult.none);
      if (online && _wasOffline) {
        await syncPendingPayments();
      }
      _wasOffline = !online;
    });
    final current = await _connectivity.checkConnectivity();
    _wasOffline = current.every((r) => r == ConnectivityResult.none);
    await syncPendingPayments();
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed) {
      unawaited(syncPendingPayments());
      unawaited(PaymentSyncScheduler.instance.ensureScheduled());
    }
  }

  Future<void> dispose() async {
    WidgetsBinding.instance.removeObserver(this);
    await _connectivitySub?.cancel();
    _connectivitySub = null;
    _started = false;
  }

  Future<bool> get isOnline async {
    final results = await _connectivity.checkConnectivity();
    return results.any((r) => r != ConnectivityResult.none);
  }

  /// Concurrent callers share one in-flight sync (mutex).
  ///
  /// Before 16:00 Baghdad: SKIP — no HTTP, status stays pending.
  /// After 16:00: uploads all eligible pending rows (including older days).
  Future<void> syncPendingPayments({DateTime? utcNow}) async {
    if (_syncLock != null) {
      return _syncLock!.future;
    }
    final lock = Completer<void>();
    _syncLock = lock;
    _uploadedAnyThisRun = false;
    lastSkippedDueToGate = false;
    try {
      // PAYMENT SYNC GATE — independent of 03:00 UI business day.
      if (!IraqDate.isPaymentSyncGateOpen(utcNow)) {
        lastSkippedDueToGate = true;
        // Keep status pending; schedule WorkManager for next 16:00.
        try {
          await PaymentSyncScheduler.instance.ensureScheduled(utcNow: utcNow);
        } catch (_) {}
        return;
      }

      if (!await isOnline) {
        return;
      }
      final prefs = await SharedPreferences.getInstance();
      final linkDelegate = prefs.getString('LinkDelegate') ?? '';
      final asyncId = prefs.getString('AsyncId') ?? '';
      if (linkDelegate.isEmpty || asyncId.isEmpty) {
        return;
      }

      final db = await DatabaseHelper().database;
      final pending = await db.query(
        'CustomerPayment',
        where:
            'SyncStatus IS NULL OR SyncStatus = ? OR SyncStatus = ? OR SyncStatus = ?',
        whereArgs: [
          PaymentSyncStatus.pendingSync,
          PaymentSyncStatus.syncFailed,
          PaymentSyncStatus.syncing,
        ],
        orderBy: 'id ASC',
      );

      for (final row in pending) {
        // Re-check gate each row in case midnight crossed mid-batch (unlikely).
        if (!IraqDate.isPaymentSyncGateOpen(utcNow)) {
          lastSkippedDueToGate = true;
          return;
        }
        if (row['PermanentFailure'] == 1) {
          continue;
        }
        final amount = (row['Amount'] as num?)?.toDouble() ?? 0;
        if (!PaymentValidation.isAcceptableAmount(amount)) {
          await db.update(
            'CustomerPayment',
            {
              'SyncStatus': PaymentSyncStatus.syncFailed,
              'SyncError': PaymentValidation.minAmountMessage,
              'PermanentFailure': 1,
            },
            where: 'id = ?',
            whereArgs: [row['id']],
          );
          continue;
        }

        await db.update(
          'CustomerPayment',
          {
            'SyncStatus': PaymentSyncStatus.syncing,
            'SyncError': null,
          },
          where: 'id = ?',
          whereArgs: [row['id']],
        );

        final payload = {
          'CustomerId': row['CustomerId'],
          'DelegateId': row['DelegateId'],
          'Amount': amount,
          'Location': row['Location'],
          'ClientPaymentId': row['ClientPaymentId'],
          'CreatedAtUtc': row['CreatedAtUtc'],
          'ReceiptNumber': row['ReceiptNumber'],
          'AsyncId': asyncId,
        };

        try {
          final response = await http
              .post(
                Uri.parse(
                    '${linkDelegate}CustomersPaymentsRequests/PostPaymentIdempotent'),
                headers: {'Content-Type': 'application/json'},
                body: jsonEncode(payload),
              )
              .timeout(const Duration(seconds: 45));

          if (response.statusCode == 200 || response.statusCode == 201) {
            _uploadedAnyThisRun = true;
            await db.update(
              'CustomerPayment',
              {
                'SyncStatus': PaymentSyncStatus.synced,
                'SyncError': null,
                'PermanentFailure': 0,
              },
              where: 'id = ?',
              whereArgs: [row['id']],
            );
          } else if (response.statusCode == 400 ||
              response.statusCode == 403 ||
              response.statusCode == 422) {
            await db.update(
              'CustomerPayment',
              {
                'SyncStatus': PaymentSyncStatus.syncFailed,
                'SyncError': _messageFromBody(response.body),
                'PermanentFailure': 1,
              },
              where: 'id = ?',
              whereArgs: [row['id']],
            );
          } else {
            await db.update(
              'CustomerPayment',
              {
                'SyncStatus': PaymentSyncStatus.pendingSync,
                'SyncError': 'HTTP ${response.statusCode}',
                'PermanentFailure': 0,
              },
              where: 'id = ?',
              whereArgs: [row['id']],
            );
          }
        } catch (_) {
          await db.update(
            'CustomerPayment',
            {
              'SyncStatus': PaymentSyncStatus.pendingSync,
              'SyncError': 'network',
              'PermanentFailure': 0,
            },
            where: 'id = ?',
            whereArgs: [row['id']],
          );
        }
      }

      if (_uploadedAnyThisRun) {
        await DelegateDataRefreshService.instance.refreshIfPossible();
      }
    } finally {
      _syncLock = null;
      if (!lock.isCompleted) {
        lock.complete();
      }
    }
  }

  String _messageFromBody(String body) {
    try {
      final decoded = jsonDecode(body);
      if (decoded is Map && decoded['message'] != null) {
        return decoded['message'].toString();
      }
    } catch (_) {}
    if (body.trim().isNotEmpty && body.length < 200) {
      return body;
    }
    return 'فشل التحقق من التسديد';
  }
}
