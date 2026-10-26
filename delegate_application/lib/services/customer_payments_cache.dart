import 'dart:convert';

import 'package:delegate_application/services/DatabaseHelper.dart';
import 'package:delegate_application/utils/Formatters.dart';
import 'package:delegate_application/utils/safe_num.dart';
import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';

typedef PaymentLine = ({String date, double amount});

/// In-memory + prefs cache so كشف حساب PDF never blocks on a cold network call.
class CustomerPaymentsCache {
  CustomerPaymentsCache._();
  static final CustomerPaymentsCache instance = CustomerPaymentsCache._();

  final Map<int, List<PaymentLine>> _mem = {};
  final Map<int, Future<List<PaymentLine>>> _inflight = {};

  List<PaymentLine>? peek(int customerId) => _mem[customerId];

  Future<List<PaymentLine>> get(
    int customerId, {
    bool forceRefresh = false,
  }) {
    if (!forceRefresh) {
      final hit = _mem[customerId];
      if (hit != null) return Future.value(hit);
      final pending = _inflight[customerId];
      if (pending != null) return pending;
    }
    final fut = _fetch(customerId);
    _inflight[customerId] = fut;
    return fut.whenComplete(() => _inflight.remove(customerId));
  }

  Future<List<PaymentLine>> _fetch(int customerId) async {
    final merged = <PaymentLine>[];
    try {
      final prefs = await SharedPreferences.getInstance();
      final link = prefs.getString('LinkDelegate') ?? '';
      final asyncId = prefs.getString('AsyncId') ?? '';
      final delegateId = prefs.getString('DelegateID') ?? '';
      if (link.isNotEmpty) {
        final res = await http
            .get(
              Uri.parse(
                  '${link}CustomersPayments/GetCustomersPaymentsCustomer/$customerId'),
              headers: {
                if (asyncId.isNotEmpty) 'X-Async-Id': asyncId,
                if (delegateId.isNotEmpty) 'X-Delegate-Id': delegateId,
              },
            )
            .timeout(const Duration(seconds: 12));
        if (res.statusCode == 200) {
          final data = jsonDecode(res.body);
          if (data is List) {
            for (final raw in data) {
              if (raw is! Map) continue;
              final m = Map<String, dynamic>.from(raw);
              final amount = SafeNum.toDouble(
                  m['amountDenar'] ?? m['AmountDenar'] ?? m['Amount']);
              final dateRaw =
                  '${m['paymentDate'] ?? m['PaymentDate'] ?? ''}'.trim();
              if (amount == 0 && dateRaw.isEmpty) continue;
              merged.add((date: _fmtDate(dateRaw), amount: amount));
            }
          }
        }
      }
    } catch (_) {}

    try {
      final db = await DatabaseHelper().database;
      final local = await db.query(
        'CustomerPayment',
        where: 'CustomerId = ?',
        whereArgs: [customerId],
        orderBy: 'PaymentDate DESC',
      );
      for (final p in local) {
        final amount = SafeNum.toDouble(p['Amount']);
        final dateRaw = '${p['PaymentDate'] ?? ''}'.trim();
        final line = (date: _fmtDate(dateRaw), amount: amount);
        final already = merged.any((e) =>
            e.date == line.date &&
            e.amount.toStringAsFixed(0) == line.amount.toStringAsFixed(0));
        if (!already) merged.insert(0, line);
      }
    } catch (_) {}

    _mem[customerId] = merged;
    try {
      final prefs = await SharedPreferences.getInstance();
      await prefs.setString(
        'pay_hist_$customerId',
        jsonEncode(merged
            .map((e) => {'d': e.date, 'a': e.amount})
            .toList()),
      );
    } catch (_) {}
    return merged;
  }

  /// Instant paint from disk while network refresh runs.
  Future<List<PaymentLine>?> loadDisk(int customerId) async {
    try {
      final prefs = await SharedPreferences.getInstance();
      final raw = prefs.getString('pay_hist_$customerId');
      if (raw == null || raw.isEmpty) return null;
      final list = jsonDecode(raw) as List;
      final rows = list
          .map((e) {
            final m = Map<String, dynamic>.from(e as Map);
            return (
              date: '${m['d'] ?? ''}',
              amount: SafeNum.toDouble(m['a']),
            );
          })
          .toList();
      _mem[customerId] = rows;
      return rows;
    } catch (_) {
      return null;
    }
  }

  static String _fmtDate(String raw) {
    if (raw.isEmpty) return '—';
    try {
      return Formatters.formatDate(raw);
    } catch (_) {
      return raw;
    }
  }
}
