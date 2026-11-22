import 'dart:async';
import 'dart:convert';

import 'package:connectivity_plus/connectivity_plus.dart';
import 'package:delegate_application/services/business_day_collection_store.dart';
import 'package:delegate_application/services/DatabaseHelper.dart';
import 'package:delegate_application/services/delegate_data_refresh_service.dart';
import 'package:delegate_application/services/device_identity_service.dart';
import 'package:delegate_application/services/manual_payment_sync_result.dart';
import 'package:delegate_application/services/payment_sync_scheduler.dart';
import 'package:delegate_application/services/payment_sync_status.dart';
import 'package:delegate_application/services/today_payments_logic.dart';
import 'package:delegate_application/services/payment_validation.dart';
import 'package:delegate_application/today_payments_page.dart';
import 'package:delegate_application/utils/iraq_date.dart';
import 'package:flutter/widgets.dart';
import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';
import 'package:uuid/uuid.dart';

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

  /// True when server said the same customer+amount is already posted today.
  /// Those rows must clear from the device queue (not stay as «مرفوض»).
  static bool isDuplicateAlreadyOnServerError(String? error) {
    if (error == null || error.isEmpty) return false;
    return error.contains('تكرار') ||
        error.contains('بنفس المبلغ') ||
        error.contains('مسجل مسبقاً بنفس المبلغ');
  }

  /// Rows that must never be uploaded again (wrong account, amount the server
  /// will always refuse, or a payment older than the server accepts).
  /// Everything else is retried on «إرسال التسديدات».
  static bool isHardPermanentError(String? error) =>
      TodayPaymentsLogic.isTerminalUnuploadableError(error);

  static final RegExp _uuidPattern = RegExp(
    r'^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$',
  );

  static bool isUuid(String? value) =>
      value != null && _uuidPattern.hasMatch(value.trim());

  /// Arabic line for the snackbar. Keeps the server sentence when it is already Arabic.
  static String describeSyncError(String? error) {
    final text = error?.trim() ?? '';
    if (text.isEmpty) return '';
    if (text == 'OWNER_MISMATCH') {
      return 'التسديد مسجّل بحساب دخول مختلف';
    }
    if (text == 'SESSION_EXPIRED') {
      return 'انتهت الجلسة — أعد تسجيل الدخول ثم أرسل';
    }
    if (text.startsWith('HTTP ')) return 'تعذر الاتصال بالخادم ($text)';
    if (text == 'network') return 'انقطع الاتصال أثناء الإرسال';
    if (text.contains('ClientPaymentId') || text.contains('UUID')) {
      return 'معرّف التسديد على الجهاز غير صالح — أُعيد تجهيزه للإرسال';
    }
    return text;
  }

  /// Re-queue ghost rows that were marked PermanentFailure only because the
  /// server already had the same-day same-amount payment (Rusafa الوزيرية etc.).
  Future<int> requeueDuplicatePermanentFailures() async {
    final db = await DatabaseHelper().database;
    final rows = await db.query(
      'CustomerPayment',
      columns: ['id', 'SyncError'],
      where: 'PermanentFailure = 1',
    );
    var n = 0;
    for (final row in rows) {
      if (!isDuplicateAlreadyOnServerError(row['SyncError']?.toString())) {
        continue;
      }
      final id = row['id'];
      if (id == null) continue;
      await db.update(
        'CustomerPayment',
        {
          'PermanentFailure': 0,
          'SyncStatus': PaymentSyncStatus.pendingSync,
          'SyncError': null,
        },
        where: 'id = ?',
        whereArgs: [id],
      );
      n++;
    }
    return n;
  }

  /// Manual send: put recoverable «مرفوض» rows back in the queue.
  /// Hard rejects (wrong account / amount below 2,000) stay blocked.
  /// Non-UUID ClientPaymentId values (legacy-local-*) are replaced so the
  /// server stops answering 400 and freezing the rest of the day.
  Future<int> requeueRecoverablePermanentFailures() async {
    final db = await DatabaseHelper().database;
    final rows = await db.query(
      'CustomerPayment',
      columns: ['id', 'SyncError', 'ClientPaymentId'],
      where: 'PermanentFailure = 1',
    );
    var n = 0;
    for (final row in rows) {
      if (isHardPermanentError(row['SyncError']?.toString())) continue;
      final id = row['id'];
      if (id == null) continue;
      final updates = <String, Object?>{
        'PermanentFailure': 0,
        'SyncStatus': PaymentSyncStatus.pendingSync,
        'SyncError': null,
      };
      final clientPaymentId = row['ClientPaymentId']?.toString();
      if (!isUuid(clientPaymentId)) {
        updates['ClientPaymentId'] = const Uuid().v4();
      }
      await db.update(
        'CustomerPayment',
        updates,
        where: 'id = ?',
        whereArgs: [id],
      );
      n++;
    }
    return n;
  }

  Future<String?> permanentFailureReason() async {
    final db = await DatabaseHelper().database;
    final rows = await db.query(
      'CustomerPayment',
      columns: ['SyncError'],
      where: 'PermanentFailure = 1',
      limit: 40,
    );
    final reasons = <String>[];
    for (final row in rows) {
      final text = describeSyncError(row['SyncError']?.toString());
      if (text.isEmpty || reasons.contains(text)) continue;
      reasons.add(text);
      if (reasons.length == 2) break;
    }
    if (reasons.isEmpty) return null;
    return reasons.join(' — ');
  }

  /// Concurrent callers share one in-flight sync (mutex).
  ///
  /// Uploads all eligible pending rows when online (no clock gate).
  Future<void> syncPendingPayments({DateTime? utcNow}) async {
    await _runSync(utcNow: utcNow, bypassGate: false);
  }

  /// Flush at 03:00 business-day rollover for leftovers that stayed offline.
  Future<void> flushPendingForBusinessDayRollover({DateTime? utcNow}) async {
    await _runSync(utcNow: utcNow, bypassGate: true);
  }

  /// Manual «إرسال التسديدات»: requires internet; uploads only unsynced rows.
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

    // Duplicates already on the server, plus every other recoverable reject
    // (follower-collection lock, missing list id, bad legacy id, session).
    // A previous send had marked them PermanentFailure, so the button did
    // nothing and the day stayed on the phone (الرستمية: 27 stuck, 2 sent).
    await requeueDuplicatePermanentFailures();
    await requeueRecoverablePermanentFailures();
    try {
      final prefs = await SharedPreferences.getInstance();
      await DatabaseHelper().releaseAwaitingSessionPayments(
        ownerAsyncId: prefs.getString('AsyncId') ?? '',
      );
    } catch (_) {}

    final pendingBefore = await countPendingPayments();
    final permanentBefore = await countPermanentFailures();
    if (pendingBefore == 0) {
      return ManualPaymentSyncResult(
        outcome: ManualPaymentSyncOutcome.nothingPending,
        permanentFailures: permanentBefore,
        remainingRetryable: 0,
        remainingPending: 0,
        rejectionReason: await permanentFailureReason(),
      );
    }

    await _runSync(utcNow: utcNow, bypassGate: false);

    final remaining = await countPendingPayments();
    final permanentAfter = await countPermanentFailures();
    final reason =
        permanentAfter > 0 ? await permanentFailureReason() : null;
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
        rejectionReason: reason,
      );
    }
    return ManualPaymentSyncResult(
      outcome: ManualPaymentSyncOutcome.failed,
      remainingPending: remaining,
      remainingRetryable: remaining,
      permanentFailures: permanentAfter,
      rejectionReason: reason,
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
      // Legacy clock gate (always open). Kept so older caches still schedule retries.
      if (!bypassGate && !IraqDate.isPaymentSyncGateOpen(utcNow)) {
        lastSkippedDueToGate = true;
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
      // Re-queue same-day-duplicate PermanentFailure ghosts (money already on server).
      // Other permanent failures (OWNER_MISMATCH / bad amount) stay blocked.
      await requeueDuplicatePermanentFailures();

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
        if (TodayPaymentsLogic.isBeyondUploadWindow(
          row['CreatedAtUtc']?.toString(),
        )) {
          await db.update(
            'CustomerPayment',
            {
              'SyncStatus': PaymentSyncStatus.syncFailed,
              'SyncError': TodayPaymentsLogic.tooOldToUploadMessage,
              'PermanentFailure': 1,
            },
            where: 'id = ?',
            whereArgs: [row['id']],
          );
          continue;
        }
        final rowIdForRepair = row['id'];
        var clientPaymentId = row['ClientPaymentId']?.toString().trim() ?? '';
        if (!isUuid(clientPaymentId) && rowIdForRepair != null) {
          clientPaymentId = const Uuid().v4();
          await db.update(
            'CustomerPayment',
            {
              'ClientPaymentId': clientPaymentId,
              'SyncStatus': PaymentSyncStatus.pendingSync,
              'SyncError': null,
              'PermanentFailure': 0,
            },
            where: 'id = ?',
            whereArgs: [rowIdForRepair],
          );
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
        final amount = double.tryParse('${row['Amount']}') ?? 0;
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

        final device = await DeviceIdentityService.instance.payloadFields();
        final customerId = int.tryParse('${row['CustomerId']}') ?? 0;
        final delegateId = int.tryParse('${row['DelegateId']}') ?? 0;
        final payload = {
          'CustomerId': customerId,
          'DelegateId': delegateId,
          'Amount': amount,
          'Location': row['Location'],
          'ClientPaymentId': clientPaymentId,
          'CreatedAtUtc': row['CreatedAtUtc'],
          'ReceiptNumber': row['ReceiptNumber'],
          'AsyncId': asyncId,
          ...device,
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

          final bodyMsg = _messageFromBody(response.body);
          final okStatus =
              response.statusCode == 200 || response.statusCode == 201;
          // Legacy servers returned HTTP 400 for same-day duplicates; treat as synced.
          final duplicateAck = !okStatus &&
              (response.statusCode == 400 ||
                  response.statusCode == 403 ||
                  response.statusCode == 422) &&
              isDuplicateAlreadyOnServerError(bodyMsg);

          if (okStatus || duplicateAck) {
            _uploadedAnyThisRun = true;
            _uploadedCountThisRun++;
            final rowId = int.tryParse(row['id']?.toString() ?? '') ?? 0;
            final cid = int.tryParse(row['CustomerId']?.toString() ?? '') ?? 0;
            final did = int.tryParse(row['DelegateId']?.toString() ?? '') ?? 0;
            final created = row['CreatedAtUtc']?.toString() ?? '';
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
                'SyncError': bodyMsg,
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
