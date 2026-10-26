import 'dart:convert';

import 'package:follower_application/collection_delegate/CustomerNoReceiptReport.dart';
import 'package:follower_application/collection_delegate/CustomerReceiptReport.dart';
import 'package:follower_application/config/app_env.dart';
import 'package:follower_application/ui/day_rating_gate.dart';
import 'package:follower_application/utils/AppTheme.dart';
import 'package:follower_application/utils/Formatters.dart';
import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';

class CollectionHomeTab extends StatefulWidget {
  final int listId;
  final String listName;

  const CollectionHomeTab({
    super.key,
    required this.listId,
    required this.listName,
  });

  @override
  State<CollectionHomeTab> createState() => CollectionHomeTabState();
}

class CollectionHomeTabState extends State<CollectionHomeTab> {
  bool _loading = true;
  bool _printing = false;
  bool _ending = false;
  int _total = 0;
  int _paidToday = 0;
  double _remainingSum = 0;
  double _daySalesSum = 0;
  double _paidAmountSum = 0;
  List<Map<String, dynamic>> _rows = [];
  String _collectorName = 'مسؤول اللجنة';

  @override
  void initState() {
    super.initState();
    reload();
  }

  Future<void> reload() => _load();

  double _d(dynamic v) {
    if (v == null) return 0;
    if (v is num) return v.toDouble();
    return double.tryParse('$v') ?? 0;
  }

  Future<void> _load() async {
    setState(() => _loading = true);
    try {
      final prefs = await SharedPreferences.getInstance();
      final asyncId = prefs.getString('AsyncId') ?? '';
      final link =
          AppEnv.apiBase(fallback: prefs.getString('LinkDelegate') ?? '');
      final name = prefs.getString('DelegateName') ?? 'مسؤول اللجنة';
      final uri = Uri.parse('${link}Followers/Collection/Customers')
          .replace(queryParameters: {
        'asyncId': asyncId,
        'listId': '${widget.listId}',
      });
      final res = await http.get(uri).timeout(const Duration(seconds: 60));
      if (res.statusCode != 200) throw Exception('load');
      final data = json.decode(res.body) as Map;
      final raw = (data['items'] ?? data['Items'] ?? []) as List;
      final rows = <Map<String, dynamic>>[];
      var paid = 0;
      var rem = 0.0;
      var day = 0.0;
      var paidAmt = 0.0;
      for (final e in raw) {
        if (e is! Map) continue;
        final row = Map<String, dynamic>.from(e);
        rows.add(row);
        final isPaid = row['paidToday'] == true || row['PaidToday'] == true;
        if (isPaid) {
          paid++;
          var amt = _d(row['paidTodayAmount'] ?? row['PaidTodayAmount']);
          if (amt <= 0) {
            amt = _d(row['amountDaySales'] ?? row['AmountDaySales']);
          }
          paidAmt += amt;
        }
        rem += _d(row['amountRemaining'] ?? row['AmountRemaining']);
        day += _d(row['amountDaySales'] ?? row['AmountDaySales']);
      }
      if (mounted) {
        setState(() {
          _rows = rows;
          _total = rows.length;
          _paidToday = paid;
          _remainingSum = rem;
          _daySalesSum = day;
          _paidAmountSum = paidAmt;
          _collectorName = name;
          _loading = false;
        });
      }
    } catch (_) {
      if (mounted) setState(() => _loading = false);
    }
  }

  List<Map<String, dynamic>> get _paidRows => _rows
      .where((r) => r['paidToday'] == true || r['PaidToday'] == true)
      .toList();

  List<Map<String, dynamic>> get _unpaidRows => _rows
      .where((r) => !(r['paidToday'] == true || r['PaidToday'] == true))
      .toList();

  Future<void> _endCollection() async {
    if (_ending) return;
    setState(() => _ending = true);
    try {
      await endCollectionWithDayRating(
        context,
        listId: widget.listId,
        listName: widget.listName,
      );
    } finally {
      if (mounted) setState(() => _ending = false);
    }
  }

  Future<void> _printPaid() async {
    final paid = _paidRows;
    if (paid.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(const SnackBar(
          content: Text('لا يوجد مسددون اليوم',
              style: TextStyle(fontFamily: 'Cairo'))));
      return;
    }
    setState(() => _printing = true);
    try {
      final receiptData = paid.map((r) {
        var amt = _d(r['paidTodayAmount'] ?? r['PaidTodayAmount']);
        if (amt <= 0) {
          amt = _d(r['amountDaySales'] ?? r['AmountDaySales']);
        }
        return {
          'CustomerId': r['customerId'] ?? r['CustomerId'],
          'CustomerName': r['customerName'] ?? r['CustomerName'],
          'Amount': amt,
          'AmountDaySales': r['amountDaySales'] ?? r['AmountDaySales'],
        };
      }).toList();
      final total =
          receiptData.fold<double>(0, (s, e) => s + _d(e['Amount']));
      final report = CustomerReceiptReport(
        delegateName: widget.listName,
        receiptName: _collectorName,
        numberOfReceipts: receiptData.length,
        amountReceiptTotal: total,
        receiptData: receiptData,
      );
      if (mounted) await report.printReceipt(context);
    } finally {
      if (mounted) setState(() => _printing = false);
    }
  }

  Future<void> _printUnpaid() async {
    final unpaid = _unpaidRows;
    if (unpaid.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(const SnackBar(
          content: Text('لا يوجد غير مسددين',
              style: TextStyle(fontFamily: 'Cairo'))));
      return;
    }
    setState(() => _printing = true);
    try {
      final report = CustomerNoReceiptReport(
        delegateName: widget.listName,
        receiptName: _collectorName,
        customerData: unpaid,
      );
      if (mounted) await report.printReport(context);
    } finally {
      if (mounted) setState(() => _printing = false);
    }
  }

  Widget _stat(String title, String value, Color color) {
    return Expanded(
      child: Container(
        margin: const EdgeInsets.all(6),
        padding: const EdgeInsets.all(14),
        decoration: BoxDecoration(
          color: Theme.of(context).cardColor,
          borderRadius: BorderRadius.circular(14),
          border: Border(right: BorderSide(color: color, width: 4)),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(title,
                style: const TextStyle(fontFamily: 'Cairo', color: Colors.grey)),
            const SizedBox(height: 6),
            Text(value,
                style: TextStyle(
                    fontFamily: 'Cairo',
                    fontWeight: FontWeight.bold,
                    fontSize: 18,
                    color: color)),
          ],
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    if (_loading) return const Center(child: CircularProgressIndicator());
    return RefreshIndicator(
      onRefresh: _load,
      child: ListView(
        padding: const EdgeInsets.all(12),
        children: [
          Text(widget.listName,
              style: const TextStyle(
                  fontFamily: 'Cairo',
                  fontWeight: FontWeight.bold,
                  fontSize: 20)),
          const SizedBox(height: 4),
          const Text('واجهة جباية مسؤول اللجنة — نفس تبويبات المندوب',
              style: TextStyle(fontFamily: 'Cairo', color: Colors.grey)),
          const SizedBox(height: 16),
          Container(
            width: double.infinity,
            margin: const EdgeInsets.symmetric(horizontal: 6, vertical: 4),
            padding: const EdgeInsets.all(18),
            decoration: BoxDecoration(
              gradient: LinearGradient(
                colors: [
                  AppTheme.primaryColor,
                  AppTheme.primaryColor.withValues(alpha: 0.75),
                ],
              ),
              borderRadius: BorderRadius.circular(16),
              boxShadow: [
                BoxShadow(
                  color: AppTheme.primaryColor.withValues(alpha: 0.25),
                  blurRadius: 10,
                  offset: const Offset(0, 4),
                ),
              ],
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Text('مجموع التسديدات',
                    style: TextStyle(
                        fontFamily: 'Cairo',
                        color: Colors.white70,
                        fontSize: 14)),
                const SizedBox(height: 8),
                Text(
                  '${Formatters.formatNumber(_paidAmountSum)} دع',
                  style: const TextStyle(
                    fontFamily: 'Cairo',
                    color: Colors.white,
                    fontWeight: FontWeight.bold,
                    fontSize: 26,
                  ),
                ),
                const SizedBox(height: 4),
                Text(
                  '$_paidToday تسديدة اليوم من $_total زبون',
                  style: const TextStyle(
                      fontFamily: 'Cairo', color: Colors.white70, fontSize: 12),
                ),
              ],
            ),
          ),
          const SizedBox(height: 12),
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 6),
            child: SizedBox(
              width: double.infinity,
              child: ElevatedButton.icon(
                onPressed: _ending || _printing ? null : _endCollection,
                icon: _ending
                    ? const SizedBox(
                        width: 18,
                        height: 18,
                        child: CircularProgressIndicator(
                          strokeWidth: 2,
                          color: Colors.white,
                        ),
                      )
                    : const Icon(Icons.check_circle_outline, color: Colors.white),
                label: Text(
                  _ending ? 'جاري الحفظ...' : 'إنهاء الجباية',
                  style: const TextStyle(
                    fontFamily: 'Cairo',
                    color: Colors.white,
                    fontWeight: FontWeight.bold,
                    fontSize: 16,
                  ),
                ),
                style: ElevatedButton.styleFrom(
                  backgroundColor: Colors.teal.shade700,
                  padding: const EdgeInsets.symmetric(vertical: 16),
                  shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(10)),
                ),
              ),
            ),
          ),
          const SizedBox(height: 10),
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 6),
            child: Row(
              children: [
                Expanded(
                  child: ElevatedButton.icon(
                    onPressed: _printing || _ending ? null : _printPaid,
                    icon: const Icon(Icons.print, color: Colors.white),
                    label: const Text('المسددين',
                        style:
                            TextStyle(fontFamily: 'Cairo', color: Colors.white)),
                    style: ElevatedButton.styleFrom(
                      backgroundColor: AppTheme.secondaryColor,
                      padding: const EdgeInsets.symmetric(vertical: 14),
                      shape: RoundedRectangleBorder(
                          borderRadius: BorderRadius.circular(10)),
                    ),
                  ),
                ),
                const SizedBox(width: 10),
                Expanded(
                  child: ElevatedButton.icon(
                    onPressed: _printing || _ending ? null : _printUnpaid,
                    icon:
                        const Icon(Icons.print_disabled, color: Colors.white),
                    label: const Text('غير المسددين',
                        style:
                            TextStyle(fontFamily: 'Cairo', color: Colors.white)),
                    style: ElevatedButton.styleFrom(
                      backgroundColor: Colors.redAccent,
                      padding: const EdgeInsets.symmetric(vertical: 14),
                      shape: RoundedRectangleBorder(
                          borderRadius: BorderRadius.circular(10)),
                    ),
                  ),
                ),
              ],
            ),
          ),
          if (_printing)
            const Padding(
              padding: EdgeInsets.all(12),
              child: LinearProgressIndicator(),
            ),
          const SizedBox(height: 12),
          Row(children: [
            _stat('الزبائن', '$_total', AppTheme.primaryColor),
            _stat('مسدد اليوم', '$_paidToday', Colors.green.shade700),
          ]),
          Row(children: [
            _stat('مجموع الأقساط', Formatters.formatNumber(_daySalesSum),
                Colors.teal.shade700),
            _stat('مجموع الباقي', Formatters.formatNumber(_remainingSum),
                Colors.orange.shade800),
          ]),
        ],
      ),
    );
  }
}
