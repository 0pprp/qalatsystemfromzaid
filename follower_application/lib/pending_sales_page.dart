import 'dart:convert';

import 'package:follower_application/config/app_env.dart';
import 'package:follower_application/ui/app_safe_scaffold.dart';
import 'package:follower_application/utils/AppTheme.dart';
import 'package:follower_application/utils/Formatters.dart';
import 'package:follower_application/utils/thousand_separator_formatter.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';

class PendingSalesPage extends StatefulWidget {
  const PendingSalesPage({super.key});

  @override
  State<PendingSalesPage> createState() => _PendingSalesPageState();
}

class _PendingSalesPageState extends State<PendingSalesPage> {
  bool _loading = true;
  String _error = '';
  List<Map<String, dynamic>> _items = [];

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<Map<String, String>> _session() async {
    final prefs = await SharedPreferences.getInstance();
    return {
      'link': AppEnv.apiBase(fallback: prefs.getString('LinkDelegate') ?? ''),
      'asyncId': prefs.getString('AsyncId') ?? '',
    };
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = '';
    });
    try {
      final s = await _session();
      final uri = Uri.parse('${s['link']}Followers/PendingSales')
          .replace(queryParameters: {'asyncId': s['asyncId']});
      final res = await http.get(uri).timeout(const Duration(seconds: 45));
      if (res.statusCode != 200) throw Exception('http ${res.statusCode}');
      final data = json.decode(res.body);
      final raw = data is Map ? (data['items'] ?? data['Items'] ?? []) : data;
      final list = (raw as List)
          .map((e) => Map<String, dynamic>.from(e as Map))
          .toList();
      if (mounted) {
        setState(() {
          _items = list;
          _loading = false;
        });
      }
    } catch (_) {
      if (mounted) {
        setState(() {
          _loading = false;
          _error = 'تعذر تحميل المبيعات بانتظار المصادقة';
        });
      }
    }
  }

  Future<void> _authenticate(Map<String, dynamic> item) async {
    var rating = 7;
    var note = '';
    final stockCtrl = TextEditingController();
    final dailyCtrl = TextEditingController();
    String? localError;
    final ok = await showDialog<bool>(
      context: context,
      builder: (ctx) => StatefulBuilder(
        builder: (ctx, setLocal) => AlertDialog(
          title: const Text('مصادقة المبيع', style: TextStyle(fontFamily: 'Cairo')),
          content: SingleChildScrollView(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(
                  '${item['customerName'] ?? item['CustomerName'] ?? ''}',
                  style: const TextStyle(fontFamily: 'Cairo', fontWeight: FontWeight.bold),
                ),
                if ('${item['shopName'] ?? item['ShopName'] ?? ''}'.trim().isNotEmpty) ...[
                  const SizedBox(height: 4),
                  Text(
                    '${item['shopName'] ?? item['ShopName']}',
                    style: const TextStyle(fontFamily: 'Cairo', color: Colors.grey),
                  ),
                ],
                const SizedBox(height: 12),
                Text('التقييم: $rating', style: const TextStyle(fontFamily: 'Cairo')),
                Slider(
                  value: rating.toDouble(),
                  min: 1,
                  max: 10,
                  divisions: 9,
                  label: '$rating',
                  onChanged: (v) => setLocal(() => rating = v.round()),
                ),
                Text(
                  rating >= 7
                      ? 'الملاحظة اختيارية'
                      : 'الملاحظة إجبارية (تقييم أقل من 7)',
                  style: TextStyle(
                    fontFamily: 'Cairo',
                    fontSize: 12,
                    color: rating >= 7 ? Colors.grey : Colors.red,
                  ),
                ),
                const SizedBox(height: 8),
                TextField(
                  maxLines: 3,
                  onChanged: (v) => note = v,
                  decoration: const InputDecoration(
                    labelText: 'ملاحظة',
                    border: OutlineInputBorder(),
                  ),
                  style: const TextStyle(fontFamily: 'Cairo'),
                ),
                const SizedBox(height: 10),
                TextField(
                  controller: stockCtrl,
                  keyboardType: TextInputType.number,
                  inputFormatters: [
                    FilteringTextInputFormatter.digitsOnly,
                    ThousandSeparatorInputFormatter(),
                  ],
                  decoration: const InputDecoration(
                    labelText: 'قيمة بضاعة المحل *',
                    border: OutlineInputBorder(),
                  ),
                  style: const TextStyle(fontFamily: 'Cairo'),
                ),
                const SizedBox(height: 8),
                TextField(
                  controller: dailyCtrl,
                  keyboardType: TextInputType.number,
                  inputFormatters: [
                    FilteringTextInputFormatter.digitsOnly,
                    ThousandSeparatorInputFormatter(),
                  ],
                  decoration: const InputDecoration(
                    labelText: 'قيمة الوارد اليومي *',
                    border: OutlineInputBorder(),
                  ),
                  style: const TextStyle(fontFamily: 'Cairo'),
                ),
                if (localError != null) ...[
                  const SizedBox(height: 8),
                  Text(localError!,
                      style: const TextStyle(fontFamily: 'Cairo', color: Colors.red)),
                ],
              ],
            ),
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(ctx, false),
              child: const Text('إلغاء', style: TextStyle(fontFamily: 'Cairo')),
            ),
            ElevatedButton(
              onPressed: () {
                if (rating < 7 && note.trim().isEmpty) {
                  setLocal(() => localError = 'الملاحظة إجبارية للتقييم أقل من 7');
                  return;
                }
                final stock = ThousandSeparatorInputFormatter.parse(stockCtrl.text);
                final daily = ThousandSeparatorInputFormatter.parse(dailyCtrl.text);
                if (stock == null || stock <= 0) {
                  setLocal(() => localError = 'قيمة بضاعة المحل مطلوبة');
                  return;
                }
                if (daily == null || daily <= 0) {
                  setLocal(() => localError = 'قيمة الوارد اليومي مطلوبة');
                  return;
                }
                Navigator.pop(ctx, true);
              },
              child: const Text('اعتماد', style: TextStyle(fontFamily: 'Cairo')),
            ),
          ],
        ),
      ),
    );
    final stock = ThousandSeparatorInputFormatter.parse(stockCtrl.text) ?? 0;
    final daily = ThousandSeparatorInputFormatter.parse(dailyCtrl.text) ?? 0;
    stockCtrl.dispose();
    dailyCtrl.dispose();
    if (ok != true) return;

    try {
      final s = await _session();
      final uri = Uri.parse('${s['link']}Followers/PendingSales/Authenticate');
      final res = await http
          .post(
            uri,
            headers: {'Content-Type': 'application/json'},
            body: json.encode({
              'asyncId': s['asyncId'],
              'customerId': item['customerId'] ?? item['CustomerId'],
              'listId': item['listId'] ?? item['ListId'],
              'rating': rating,
              'note': note.trim().isEmpty ? null : note.trim(),
              'shopStockEstimatedValue': stock,
              'estimatedDailyRevenue': daily,
            }),
          )
          .timeout(const Duration(seconds: 30));
      if (res.statusCode != 200) {
        final msg = () {
          try {
            final j = json.decode(res.body);
            return '${j['message'] ?? j['Message'] ?? res.body}';
          } catch (_) {
            return 'تعذر المصادقة';
          }
        }();
        throw Exception(msg);
      }
      await _load();
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('تمت المصادقة بنجاح', style: TextStyle(fontFamily: 'Cairo')),
          ),
        );
      }
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('$e', style: const TextStyle(fontFamily: 'Cairo'))),
        );
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Directionality(
      textDirection: TextDirection.rtl,
      child: AppSafeScaffold(
        appBar: AppBar(
          title: const Text('المبيعات', style: TextStyle(fontFamily: 'Cairo')),
          backgroundColor: AppTheme.primaryColor,
          actions: [
            IconButton(onPressed: _load, icon: const Icon(Icons.refresh)),
          ],
        ),
        body: _loading
            ? const Center(child: CircularProgressIndicator())
            : _error.isNotEmpty
                ? Center(child: Text(_error, style: const TextStyle(fontFamily: 'Cairo')))
                : Column(
                    children: [
                      Container(
                        width: double.infinity,
                        margin: const EdgeInsets.fromLTRB(12, 12, 12, 0),
                        padding: const EdgeInsets.all(12),
                        decoration: BoxDecoration(
                          color: AppTheme.primaryColor.withValues(alpha: 0.08),
                          borderRadius: BorderRadius.circular(12),
                        ),
                        child: const Text(
                          'فائدة القسم: مبيعات جديدة على قوائمك بانتظار مصادقة المتابع '
                          '(تقييم + ملاحظة + قيمة بضاعة المحل + الوارد اليومي). لا يُرسل طلب مبيع من هنا.',
                          style: TextStyle(fontFamily: 'Cairo', fontSize: 12, height: 1.5),
                        ),
                      ),
                      Expanded(
                        child: _items.isEmpty
                            ? const Center(
                                child: Text(
                                  'لا توجد مبيعات بانتظار المصادقة',
                                  style: TextStyle(fontFamily: 'Cairo', color: Colors.grey),
                                ),
                              )
                            : RefreshIndicator(
                                onRefresh: _load,
                                child: ListView.separated(
                                  padding: const EdgeInsets.all(12),
                                  itemCount: _items.length,
                                  separatorBuilder: (_, __) => const SizedBox(height: 10),
                                  itemBuilder: (context, i) {
                                    final item = _items[i];
                                    final name =
                                        '${item['customerName'] ?? item['CustomerName'] ?? ''}';
                                    final listName =
                                        '${item['listName'] ?? item['ListName'] ?? ''}';
                                    final total = Formatters.formatNumber(
                                      double.tryParse(
                                              '${item['amountTotalSales'] ?? item['AmountTotalSales'] ?? 0}') ??
                                          0,
                                    );
                                    return Container(
                                      decoration: BoxDecoration(
                                        color: Theme.of(context).cardColor,
                                        borderRadius: BorderRadius.circular(16),
                                      ),
                                      child: Padding(
                                        padding: const EdgeInsets.all(12),
                                        child: Column(
                                          crossAxisAlignment: CrossAxisAlignment.stretch,
                                          children: [
                                            InkWell(
                                              onTap: () async {
                                                final listId = int.tryParse(
                                                        '${item['listId'] ?? item['ListId'] ?? 0}') ??
                                                    0;
                                                await Navigator.pushNamed(
                                                  context,
                                                  '/CustomerProfile',
                                                  arguments: {
                                                    'customer': {
                                                      'customerId': item['customerId'] ??
                                                          item['CustomerId'],
                                                      'customerName': name,
                                                      'phoneNumber': item['phoneNumber'] ??
                                                          item['PhoneNumber'] ??
                                                          '',
                                                    },
                                                    'listId': listId,
                                                    'pendingAuth': true,
                                                  },
                                                );
                                                if (mounted) await _load();
                                              },
                                              child: Column(
                                                crossAxisAlignment:
                                                    CrossAxisAlignment.stretch,
                                                children: [
                                                  Text(name,
                                                      style: const TextStyle(
                                                          fontFamily: 'Cairo',
                                                          fontWeight: FontWeight.bold)),
                                                  const SizedBox(height: 4),
                                                  Text(
                                                    '$listName\nالمبلغ: $total دع',
                                                    style: const TextStyle(
                                                        fontFamily: 'Cairo', fontSize: 12),
                                                  ),
                                                ],
                                              ),
                                            ),
                                            const SizedBox(height: 10),
                                            FilledButton(
                                              style: FilledButton.styleFrom(
                                                  backgroundColor: AppTheme.primaryColor),
                                              onPressed: () => _authenticate(item),
                                              child: const Text(
                                                'تمت المصادقة',
                                                style: TextStyle(fontFamily: 'Cairo'),
                                              ),
                                            ),
                                          ],
                                        ),
                                      ),
                                    );
                                  },
                                ),
                              ),
                      ),
                    ],
                  ),
      ),
    );
  }
}
