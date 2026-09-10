import 'dart:async';
import 'dart:convert';

import 'package:connectivity_plus/connectivity_plus.dart';
import 'package:delegate_application/services/DatabaseHelper.dart';
import 'package:delegate_application/services/payment_sync_status.dart';
import 'package:delegate_application/services/payment_validation.dart';
import 'package:flutter/widgets.dart';
import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';

/// Offline-first auto-sync for collection payments.
/// Never deletes a local row until the server acknowledges success.
class PaymentSyncService with WidgetsBindingObserver {
  PaymentSyncService._();
  static final PaymentSyncService instance = PaymentSyncService._();

  final Connectivity _connectivity = Connectivity();
  StreamSubscription<List<ConnectivityResult>>? _connectivitySub;
  Completer<void>? _syncLock;
  bool _started = false;
  bool _wasOffline = true;

  Future<void> start() async {
    if (_started) return;
    _started = true;
    WidgetsBinding.instance.addObserver(this);
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
  Future<void> syncPendingPayments() async {
    if (_syncLock != null) {
      return _syncLock!.future;
    }
    final lock = Completer<void>();
    _syncLock = lock;
    try {
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
