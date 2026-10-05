import 'dart:async';
import 'dart:convert';

import 'package:connectivity_plus/connectivity_plus.dart';
import 'package:delegate_application/services/business_day_collection_store.dart';
import 'package:delegate_application/services/DatabaseHelper.dart';
import 'package:delegate_application/services/delegate_data_refresh_service.dart';
import 'package:delegate_application/services/manual_payment_sync_result.dart';
import 'package:delegate_application/services/payment_sync_scheduler.dart';
import 'package:delegate_application/services/payment_sync_status.dart';
import 'package:delegate_application/services/payment_validation.dart';
import 'package:delegate_application/today_payments_page.dart';
import 'package:delegate_application/utils/iraq_date.dart';
import 'package:flutter/widgets.dart';
import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';

/// Offline-first auto-sync for collection payments.
///
/// CRITICAL: Automatic HTTP upload is gated by [IraqDate.isPaymentSyncGateOpen]
/// — Baghdad window **[16:00 → next 02:59]** (after 4pm until business-day end).
/// Before the gate, payments stay `pending` locally.
///
/// User-initiated [sendPendingPaymentsNow] requires the same send window, plus
/// internet, and only uploads rows that are not already synced.
/// [flushPendingForBusinessDayRollover] bypasses the clock once at 03:00 for
/// leftovers that were already eligible after yesterday's 16:00.
/// On server success the local row is **deleted** (ledger keeps day lock).
class PaymentSyncService with WidgetsBindingObserver {
  PaymentSyncService._();
  static final PaymentSyncService instance = PaymentSyncService._();

  final Connectivity _connectivity = Connectivity();
  StreamSubscription<List<ConnectivityResult>>? _connectivitySub;
  Completer<void>? _syncLock;
  bool _started = false;
  bool _wasOffline = true;
  bool _uploadedAnyThisRun = false;
  int _uploadedCountThisRun = 0;

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
    // Background — never block callers waiting on start().
    unawaited(syncPendingPayments());
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

  Future<int> countPendingPayments() async {
    final db = await DatabaseHelper().database;
    final rows = await db.rawQuery('''
SELECT COUNT(1) AS c FROM CustomerPayment
WHERE (PermanentFailure IS NULL OR PermanentFailure = 0)
  AND (SyncStatus IS NULL OR SyncStatus = ? OR SyncStatus = ? OR SyncStatus = ?)
''', [
      PaymentSyncStatus.pendingSync,
      PaymentSyncStatus.syncFailed,
      PaymentSyncStatus.syncing,
    ]);
    return (rows.first['c'] as num?)?.toInt() ?? 0;
  }

  Future<int> countPermanentFailures() async {
    final db = await DatabaseHelper().database;
    final rows = await db.rawQuery('''
SELECT COUNT(1) AS c FROM CustomerPayment
WHERE PermanentFailure = 1
''');
    return (rows.first['c'] as num?)?.toInt() ?? 0;
  }

  /// Concurrent callers share one in-flight sync (mutex).
  ///
  /// Before the send window [16:00→02:59] Baghdad: SKIP — no HTTP.
  /// Inside the window: uploads all eligible pending rows (unsynced only).
  Future<void> syncPendingPayments({DateTime? utcNow}) async {
    await _runSync(utcNow: utcNow, bypassGate: false);
  }

  /// Last-chance flush at 03:00 rollover: previous-day leftovers that were
  /// already past their 16:00 window but stayed offline. Bypass clock gate once.
  Future<void> flushPendingForBusinessDayRollover({DateTime? utcNow}) async {
    await _runSync(utcNow: utcNow, bypassGate: true);
  }

  /// Manual «إرسال التسديدات»: requires send window (after 16:00 Baghdad) +
  /// internet; uploads only unsynced rows.
  Future<ManualPaymentSyncResult> sendPendingPaymentsNow({DateTime? utcNow}) async {
    if (!IraqDate.isPaymentSyncGateOpen(utcNow)) {
      return const ManualPaymentSyncResult(
        outcome: ManualPaymentSyncOutcome.gateClosed,
      );
    }
    if (!await isOnline) {
      return const ManualPaymentSyncResult(
        outcome: ManualPaymentSyncOutcome.offline,
      );
    }
    if (_syncLock != null) {
      return const ManualPaymentSyncResult(
        outcome: ManualPaymentSyncOutcome.busy,
      );
    }

    final pendingBefore = await countPendingPayments();
    final permanentBefore = await countPermanentFailures();
    if (pendingBefore == 0) {
      return ManualPaymentSyncResult(
        outcome: ManualPaymentSyncOutcome.nothingPending,
        permanentFailures: permanentBefore,
        remainingRetryable: 0,
        remainingPending: 0,
      );
    }

    await _runSync(utcNow: utcNow, bypassGate: false);

    final remaining = await countPendingPayments();
    final permanentAfter = await countPermanentFailures();
    if (_uploadedCountThisRun > 0 && remaining == 0 && permanentAfter == 0) {
      return ManualPaymentSyncResult(
        outcome: ManualPaymentSyncOutcome.uploaded,
        uploadedCount: _uploadedCountThisRun,
        remainingPending: 0,
        remainingRetryable: 0,
        permanentFailures: 0,
      );
    }
    if (_uploadedCountThisRun > 0) {
      return ManualPaymentSyncResult(
        outcome: ManualPaymentSyncOutcome.partial,
        uploadedCount: _uploadedCountThisRun,
        remainingPending: remaining,
        remainingRetryable: remaining,
        permanentFailures: permanentAfter,
      );
    }
    return ManualPaymentSyncResult(
      outcome: ManualPaymentSyncOutcome.failed,
      remainingPending: remaining,
      remainingRetryable: remaining,
      permanentFailures: permanentAfter,
    );
  }

  Future<void> _runSync({DateTime? utcNow, required bool bypassGate}) async {
    if (_syncLock != null) {
      return _syncLock!.future;
    }
    final lock = Completer<void>();
    _syncLock = lock;
    _uploadedAnyThisRun = false;
    _uploadedCountThisRun = 0;
    lastSkippedDueToGate = false;
    try {
      // PAYMENT SYNC GATE — [16:00 → 02:59] Baghdad (03:00 UI day is separate).
      if (!bypassGate && !IraqDate.isPaymentSyncGateOpen(utcNow)) {
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
      // Do NOT auto-clear PermanentFailure — permanent rejections stay visible
      // until an explicit reconciliation path. SESSION_EXPIRED stays blocked.

      final pending = await db.query(
        'CustomerPayment',
        where:
            '(PermanentFailure IS NULL OR PermanentFailure = 0) AND (SyncStatus IS NULL OR SyncStatus = ? OR SyncStatus = ? OR SyncStatus = ?)',
        whereArgs: [
          PaymentSyncStatus.pendingSync,
          PaymentSyncStatus.syncFailed,
          PaymentSyncStatus.syncing,
        ],
        orderBy: 'id ASC',
      );

      for (final row in pending) {
        // Re-check gate on live clock (not frozen utcNow) if midnight→03:00 crossed.
        if (!bypassGate && !IraqDate.isPaymentSyncGateOpen()) {
          lastSkippedDueToGate = true;
          try {
            await PaymentSyncScheduler.instance.ensureScheduled();
          } catch (_) {}
          return;
        }
        if (row['PermanentFailure'] == 1) {
          continue;
        }
        // Ownership: never upload a payment collected under another AsyncId.
        final owner = (row['OwnerAsyncId']?.toString() ?? '').trim();
        if (owner.isNotEmpty && owner != asyncId) {
          await db.update(
            'CustomerPayment',
            {
              'SyncStatus': PaymentSyncStatus.syncFailed,
              'SyncError': 'OWNER_MISMATCH',
              'PermanentFailure': 1,
            },
            where: 'id = ?',
            whereArgs: [row['id']],
          );
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
            _uploadedCountThisRun++;
            final rowId = int.tryParse(row['id']?.toString() ?? '') ?? 0;
            final cid = int.tryParse(row['CustomerId']?.toString() ?? '') ?? 0;
            final did = int.tryParse(row['DelegateId']?.toString() ?? '') ?? 0;
            final created = row['CreatedAtUtc']?.toString() ?? '';
            final clientPaymentId = row['ClientPaymentId']?.toString() ?? '';
            // Keep durable day lock before wiping the local queue row.
            await BusinessDayCollectionStore.record(
              customerId: cid,
              delegateId: did,
              amount: amount,
              clientPaymentId: clientPaymentId,
              createdAtUtc: created,
            );
            if (rowId > 0) {
              await DatabaseHelper().deleteCustomerPaymentByIds([rowId]);
            }
          } else if (response.statusCode == 401) {
            // Stale session — stop infinite retry until the delegate logs in again.
            await db.update(
              'CustomerPayment',
              {
                'SyncStatus': PaymentSyncStatus.syncFailed,
                'SyncError': 'SESSION_EXPIRED',
                'PermanentFailure': 1,
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
        TodayPaymentsRefresh.notify();
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
