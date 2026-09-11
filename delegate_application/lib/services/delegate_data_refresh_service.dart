import 'dart:async';
import 'dart:convert';

import 'package:connectivity_plus/connectivity_plus.dart';
import 'package:delegate_application/config/app_env.dart';
import 'package:delegate_application/services/DatabaseHelper.dart';
import 'package:flutter/widgets.dart';
import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';
import 'package:sqflite/sqflite.dart';

/// Auto refresh of delegate/customer master data (replaces manual Sync screen).
/// Fetches remote first; only then replaces SQLite inside a transaction.
class DelegateDataRefreshService with WidgetsBindingObserver {
  DelegateDataRefreshService._();
  static final DelegateDataRefreshService instance =
      DelegateDataRefreshService._();

  final Connectivity _connectivity = Connectivity();
  StreamSubscription<List<ConnectivityResult>>? _connectivitySub;
  Timer? _periodicTimer;
  Completer<bool>? _refreshLock;
  bool _started = false;
  bool _wasOffline = true;
  DateTime? _lastSuccessAt;
  String? _lastError;

  final ValueNotifier<String?> statusMessage = ValueNotifier<String?>(null);

  /// Bumps after a successful SQLite replace so tabs (e.g. Customer) can reload.
  final ValueNotifier<int> dataRevision = ValueNotifier<int>(0);

  DateTime? get lastSuccessAt => _lastSuccessAt;
  String? get lastError => _lastError;
  bool get isRefreshing => _refreshLock != null;

  Future<void> start() async {
    if (_started) return;
    _started = true;
    WidgetsBinding.instance.addObserver(this);
    _connectivitySub =
        _connectivity.onConnectivityChanged.listen((results) async {
      final online = results.any((r) => r != ConnectivityResult.none);
      if (online && _wasOffline) {
        await refreshIfPossible();
      }
      _wasOffline = !online;
    });
    final current = await _connectivity.checkConnectivity();
    _wasOffline = current.every((r) => r == ConnectivityResult.none);

    _periodicTimer?.cancel();
    _periodicTimer = Timer.periodic(const Duration(minutes: 5), (_) {
      unawaited(refreshIfPossible());
    });

    await refreshIfPossible();
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed) {
      unawaited(refreshIfPossible());
    }
  }

  Future<void> dispose() async {
    WidgetsBinding.instance.removeObserver(this);
    await _connectivitySub?.cancel();
    _connectivitySub = null;
    _periodicTimer?.cancel();
    _periodicTimer = null;
    _started = false;
  }

  Future<bool> get isOnline async {
    final results = await _connectivity.checkConnectivity();
    return results.any((r) => r != ConnectivityResult.none);
  }

  /// Mutex: concurrent callers await the same in-flight refresh.
  Future<bool> refreshIfPossible() async {
    if (_refreshLock != null) {
      return _refreshLock!.future;
    }
    final lock = Completer<bool>();
    _refreshLock = lock;
    var ok = false;
    try {
      ok = await _refreshUnlocked();
      return ok;
    } finally {
      _refreshLock = null;
      if (!lock.isCompleted) lock.complete(ok);
    }
  }

  Future<bool> _refreshUnlocked() async {
    if (!await isOnline) {
      return false;
    }

    final prefs = await SharedPreferences.getInstance();
    final linkDelegate = prefs.getString('LinkDelegate') ?? '';
    final delegateId = int.tryParse(prefs.getString('DelegateID') ?? '0') ?? 0;
    if (linkDelegate.isEmpty || delegateId <= 0) {
      return false;
    }

    // Demo isolation: never refresh against a production host.
    if (AppEnv.isDemo && !_isDemoLink(linkDelegate)) {
      debugPrint(
          'DelegateDataRefresh blocked: demo env with non-demo LinkDelegate');
      _lastError = 'demo_link_mismatch';
      return false;
    }

    try {
      final snapshot = await _fetchSnapshot(linkDelegate, delegateId);
      if (snapshot == null) {
        _lastError = 'fetch_failed';
        return false;
      }

      await _replaceLocalAtomically(snapshot);
      _lastSuccessAt = DateTime.now().toUtc();
      _lastError = null;
      dataRevision.value++;
      statusMessage.value = 'تم تحديث البيانات';
      // Clear soft indicator after a short delay.
      Future<void>.delayed(const Duration(seconds: 3), () {
        if (statusMessage.value == 'تم تحديث البيانات') {
          statusMessage.value = null;
        }
      });
      return true;
    } catch (e, st) {
      debugPrint('DelegateDataRefresh failed: $e\n$st');
      _lastError = e.toString();
      return false;
    }
  }

  bool _isDemoLink(String link) =>
      link.contains('169.58.236.52') || link.contains('8081');

  Future<_RefreshSnapshot?> _fetchSnapshot(
      String linkDelegate, int delegateId) async {
    final selectRes = await http
        .get(
          Uri.parse('${linkDelegate}Delegates/GetDelegateSelect/$delegateId'),
          headers: {'Content-Type': 'application/json'},
        )
        .timeout(const Duration(seconds: 30));
    assert(() {
      debugPrint(
          'GetDelegateSelect status=${selectRes.statusCode} path=Delegates/GetDelegateSelect/$delegateId');
      return true;
    }());
    if (selectRes.statusCode != 200) return null;

    final List<dynamic> delegates = jsonDecode(selectRes.body) as List<dynamic>;
    assert(() {
      debugPrint('GetDelegateSelect count=${delegates.length}');
      return true;
    }());
    if (delegates.isEmpty) return null;

    final weekRes = await http
        .get(Uri.parse('${linkDelegate}Customers/GetDateWeek'))
        .timeout(const Duration(seconds: 20));
    if (weekRes.statusCode != 200) return null;
    final week = jsonDecode(weekRes.body) as Map<String, dynamic>;

    final customers = <Map<String, dynamic>>[];
    for (final d in delegates) {
      final id = d['delegateId'];
      final custRes = await http
          .get(Uri.parse(
              '${linkDelegate}Customers/GetCustomersDelegateAll/$id'))
          .timeout(const Duration(seconds: 60));
      if (custRes.statusCode != 200) return null;
      final List<dynamic> rows = jsonDecode(custRes.body) as List<dynamic>;
      for (final c in rows) {
        customers.add(_mapCustomer(c));
      }
    }

    final selectRows = delegates.map(_mapSelectDelegate).toList();
    return _RefreshSnapshot(
      selectDelegates: selectRows,
      dateWeek: {
        'Date1': week['date1']?.toString() ?? '',
        'Date2': week['date2']?.toString() ?? '',
        'Date3': week['date3']?.toString() ?? '',
        'Date4': week['date4']?.toString() ?? '',
        'Date5': week['date5']?.toString() ?? '',
        'Date6': week['date6']?.toString() ?? '',
        'Date7': week['date7']?.toString() ?? '',
      },
      customers: customers,
    );
  }

  Map<String, dynamic> _mapSelectDelegate(dynamic element) {
    return {
      'DelegateId': int.tryParse(element['delegateId']?.toString() ?? '0') ?? 0,
      'DelegateName': element['delegateName']?.toString() ?? '',
      'ReceiptName': element['receiptName']?.toString() ?? '',
      'UpdateReceipt':
          (element['updateReceipt'] == true || element['updateReceipt'] == 1)
              ? 1
              : 0,
      'DeleteReceipt':
          (element['deleteReceipt'] == true || element['deleteReceipt'] == 1)
              ? 1
              : 0,
      'DevicePaymentState': (element['devicePaymentState'] == true ||
              element['devicePaymentState'] == 1)
          ? 1
          : 0,
    };
  }

  Map<String, dynamic> _mapCustomer(dynamic customerElement) {
    return {
      'CustomerId': customerElement['customerId'],
      'CustomerName': customerElement['customerName']?.toString() ?? '',
      'DelegateId': customerElement['delegateId'],
      'PhoneNumber': customerElement['phoneNumber']?.toString() ?? '',
      'AmountTotalSales': customerElement['amountTotalSales'],
      'AmountDaySales': customerElement['amountDaySales'],
      'ReceiptsTotal': customerElement['receiptsTotal'],
      'AmountRemaining': customerElement['amountRemaining'],
      'ItemsNames': customerElement['itemsNames']?.toString() ?? '',
      'CityId': customerElement['cityId'],
      'Amount1': customerElement['amount1'],
      'Amount2': customerElement['amount2'],
      'Amount3': customerElement['amount3'],
      'Amount4': customerElement['amount4'],
      'Amount5': customerElement['amount5'],
      'Amount6': customerElement['amount6'],
      'Amount7': customerElement['amount7'],
      'PhoneNumberCompany':
          customerElement['phoneNumberCompany']?.toString() ?? '',
      'CountReceiptDevice': customerElement['countReceiptDevice'],
      'Address': customerElement['address']?.toString() ?? '',
      'ShopName': customerElement['shopName']?.toString() ?? '',
      'NumberOfDayPayment': customerElement['numberOfDayPayment'],
      'IsLegal': customerElement['isLegal']?.toString() ?? 'false',
      'LastPaymentDate': customerElement['lastPaymentDate']?.toString() ?? '',
      'DateSaleDevice': customerElement['dateSaleDevice']?.toString() ?? '',
    };
  }

  Future<void> _replaceLocalAtomically(_RefreshSnapshot snapshot) async {
    final db = await DatabaseHelper().database;
    await db.transaction((txn) async {
      await txn.delete('SelectDelegate');
      for (final row in snapshot.selectDelegates) {
        await txn.insert('SelectDelegate', row,
            conflictAlgorithm: ConflictAlgorithm.replace);
      }

      await txn.delete('DateWeek');
      await txn.insert('DateWeek', snapshot.dateWeek,
          conflictAlgorithm: ConflictAlgorithm.replace);

      await txn.delete('Customer');
      for (final row in snapshot.customers) {
        await txn.insert('Customer', row,
            conflictAlgorithm: ConflictAlgorithm.replace);
      }
      // CustomerPayment queue is intentionally preserved.
    });
  }
}

class _RefreshSnapshot {
  _RefreshSnapshot({
    required this.selectDelegates,
    required this.dateWeek,
    required this.customers,
  });

  final List<Map<String, dynamic>> selectDelegates;
  final Map<String, dynamic> dateWeek;
  final List<Map<String, dynamic>> customers;
}

/// Pure helpers for tests (atomic refresh contract).
class DelegateDataRefreshRules {
  static bool shouldReplaceLocal({
    required bool fetchSucceeded,
    required bool responseValid,
  }) =>
      fetchSucceeded && responseValid;

  static bool allowsConcurrentSecondRefresh({required bool alreadyInFlight}) =>
      !alreadyInFlight;
}
