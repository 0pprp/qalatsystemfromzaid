import 'dart:convert';

import 'package:follower_application/AsyncIdChecker.dart';
import 'package:follower_application/config/app_env.dart';
import 'package:follower_application/ui/app_safe_scaffold.dart';
import 'package:follower_application/utils/AppTheme.dart';
import 'package:follower_application/utils/Formatters.dart';
import 'package:follower_application/utils/display_name.dart';
import 'package:follower_application/utils/iraq_datetime.dart';
import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';

/// Mini collection UI: unpaid customers for an enabled list + post payment.
class CollectionPaymentPage extends StatefulWidget {
  final int listId;
  final String listName;

  const CollectionPaymentPage({
    super.key,
    required this.listId,
    required this.listName,
  });

  @override
  State<CollectionPaymentPage> createState() => _CollectionPaymentPageState();
}

class _CollectionPaymentPageState extends State<CollectionPaymentPage> {
  bool _loading = true;
  String _error = '';
  List<Map<String, dynamic>> _customers = [];

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<Map<String, String>> _session() async {
    final prefs = await SharedPreferences.getInstance();
    return {
      'asyncId': prefs.getString('AsyncId') ?? '',
      'link': AppEnv.apiBase(fallback: prefs.getString('LinkDelegate') ?? ''),
    };
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = '';
    });
    try {
      final s = await _session();
      final date = IraqDateTime.formatYmd(
          DateTime.now().subtract(const Duration(days: 1)));
      final uri = Uri.parse('${s['link']}Followers/Daily').replace(queryParameters: {
        'asyncId': s['asyncId'],
        'childId': '${widget.listId}',
        'date': date,
        'showType': 'الغير مسددين',
      });
      final res = await http.get(uri).timeout(const Duration(seconds: 60));
      if (res.statusCode == 401) {
        await AsyncIdChecker.logout();
        if (mounted) Navigator.pushReplacementNamed(context, '/Login');
        return;
      }
      if (res.statusCode != 200) throw Exception('daily');
      final data = json.decode(res.body) as List;
      final rows = data.map((item) {
        final m = item as Map;
        return {
          'customerId':
              int.tryParse('${m['customerID'] ?? m['CustomerID'] ?? m['customerId'] ?? 0}') ??
                  0,
          'customerName': '${m['customerName'] ?? m['CustomerName'] ?? ''}',
          'phoneNumber': '${m['phoneNumber'] ?? m['PhoneNumber'] ?? ''}',
          'amountDaySales': _toD(m['amountDaySales'] ?? m['AmountDaySales']),
          'amountRemaining': _toD(m['amountRemaining'] ?? m['AmountRemaining']),
        };
      }).where((r) =>
          (r['customerId'] as int) > 0 &&
          (r['amountRemaining'] as double) > 0).toList();
      if (mounted) {
        setState(() {
          _customers = rows;
          _loading = false;
        });
      }
    } catch (_) {
      if (mounted) {
        setState(() {
          _loading = false;
          _error = 'تعذر تحميل زبائن الجباية';
        });
      }
    }
  }

  double _toD(dynamic v) {
    if (v == null) return 0;
    if (v is num) return v.toDouble();
    return double.tryParse('$v') ?? 0;
  }

  Future<void> _pay(Map<String, dynamic> customer) async {
    final due = customer['amountDaySales'] as double;
    final controller = TextEditingController(
        text: due > 0 ? due.toStringAsFixed(0) : '');
    final ok = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: Text('تسديد — ${customer['customerName']}',
            style: const TextStyle(fontFamily: 'Cairo')),
        content: TextField(
          controller: controller,
          keyboardType: TextInputType.number,
          decoration: const InputDecoration(
            labelText: 'المبلغ',
            labelStyle: TextStyle(fontFamily: 'Cairo'),
          ),
          style: const TextStyle(fontFamily: 'Cairo'),
        ),
        actions: [
          TextButton(
              onPressed: () => Navigator.pop(ctx, false),
              child: const Text('إلغاء', style: TextStyle(fontFamily: 'Cairo'))),
          ElevatedButton(
              onPressed: () => Navigator.pop(ctx, true),
              child: const Text('تأكيد', style: TextStyle(fontFamily: 'Cairo'))),
        ],
      ),
    );
    final amount = double.tryParse(controller.text.trim()) ?? 0;
    controller.dispose();
    if (!mounted || ok != true || amount <= 0) return;

    try {
      final s = await _session();
      final uuid = _newUuid();
      final uri =
          Uri.parse('${s['link']}Followers/Collection/PostPaymentIdempotent');
      final res = await http
          .post(
            uri,
            headers: {'Content-Type': 'application/json'},
            body: json.encode({
              'asyncId': s['asyncId'],
              'listId': widget.listId,
              'customerId': customer['customerId'],
              'amount': amount,
              'location': 'follower-app',
              'clientPaymentId': uuid,
              'createdAtUtc': DateTime.now().toUtc().toIso8601String(),
            }),
          )
          .timeout(const Duration(seconds: 30));
      if (!mounted) return;
      if (res.statusCode == 200) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(
            content:
                Text('تم استلام التسديد', style: TextStyle(fontFamily: 'Cairo'))));
        await _load();
      } else {
        String msg = 'تعذر التسديد';
        try {
          final body = json.decode(res.body);
          msg = '${body['message'] ?? body['Message'] ?? msg}';
        } catch (_) {}
        ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(content: Text(msg, style: const TextStyle(fontFamily: 'Cairo'))));
      }
    } catch (_) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(const SnackBar(
          content: Text('تعذر الاتصال', style: TextStyle(fontFamily: 'Cairo'))));
    }
  }

  static String _newUuid() {
    final r = DateTime.now().microsecondsSinceEpoch;
    final a = (r & 0xffffffff).toRadixString(16).padLeft(8, '0');
    final b = ((r >> 8) & 0xffff).toRadixString(16).padLeft(4, '0');
    final c = (0x4000 | ((r >> 16) & 0x0fff)).toRadixString(16).padLeft(4, '0');
    final d = (0x8000 | ((r >> 20) & 0x3fff)).toRadixString(16).padLeft(4, '0');
    final e = (r.abs() % 0xffffffffffff)
        .toRadixString(16)
        .padLeft(12, '0')
        .substring(0, 12);
    return '$a-$b-$c-$d-$e';
  }

  @override
  Widget build(BuildContext context) {
    return Directionality(
      textDirection: TextDirection.rtl,
      child: AppSafeScaffold(
        backgroundColor: AppTheme.backgroundColor,
        appBar: AppBar(
          title: Text('جباية — ${widget.listName}',
              style: const TextStyle(fontFamily: 'Cairo')),
          backgroundColor: AppTheme.primaryColor,
          foregroundColor: Colors.white,
        ),
        body: _loading
            ? const Center(child: CircularProgressIndicator())
            : RefreshIndicator(
                onRefresh: _load,
                child: ListView(
                  padding: const EdgeInsets.all(16),
                  children: [
                    if (_error.isNotEmpty)
                      Text(_error,
                          style: const TextStyle(
                              fontFamily: 'Cairo', color: Colors.red)),
                    if (_customers.isEmpty && _error.isEmpty)
                      const Padding(
                        padding: EdgeInsets.all(40),
                        child: Center(
                          child: Text('لا يوجد زبائن غير مسددين',
                              style: TextStyle(
                                  fontFamily: 'Cairo', color: Colors.grey)),
                        ),
                      )
                    else
                      ...withNameSeparators(_customers.map((c) => ListTile(
                            title: PersonNameText('${c['customerName']}'),
                            subtitle: Text(
                              'الباقي: ${Formatters.formatNumber(c['amountRemaining'] as double)}'
                              ' · القسط: ${Formatters.formatNumber(c['amountDaySales'] as double)}',
                              style: const TextStyle(fontFamily: 'Cairo'),
                            ),
                            trailing: ElevatedButton(
                              onPressed: () => _pay(c),
                              style: ElevatedButton.styleFrom(
                                backgroundColor: AppTheme.primaryColor,
                                foregroundColor: Colors.white,
                              ),
                              child: const Text('تسديد',
                                  style: TextStyle(fontFamily: 'Cairo')),
                            ),
                          ))),
                  ],
                ),
              ),
      ),
    );
  }
}
