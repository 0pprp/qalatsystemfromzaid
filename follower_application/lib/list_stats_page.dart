import 'dart:convert';

import 'package:follower_application/config/app_env.dart';
import 'package:follower_application/customer_profile_page.dart';
import 'package:follower_application/ui/app_safe_scaffold.dart';
import 'package:follower_application/utils/AppTheme.dart';
import 'package:follower_application/utils/Formatters.dart';
import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';

class ListStatsPage extends StatefulWidget {
  const ListStatsPage({super.key});

  @override
  State<ListStatsPage> createState() => _ListStatsPageState();
}

class _ListStatsPageState extends State<ListStatsPage> {
  bool _loading = true;
  String _error = '';
  List<Map<String, dynamic>> _items = [];
  int? _yearMonth;

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
      final uri = Uri.parse('${s['link']}Followers/ListStats')
          .replace(queryParameters: {'asyncId': s['asyncId']});
      final res = await http.get(uri).timeout(const Duration(seconds: 60));
      if (res.statusCode != 200) throw Exception('http ${res.statusCode}');
      final data = json.decode(res.body) as Map;
      final raw = data['items'] ?? data['Items'] ?? [];
      final list = (raw as List)
          .map((e) => Map<String, dynamic>.from(e as Map))
          .toList();
      if (mounted) {
        setState(() {
          _items = list;
          _yearMonth =
              int.tryParse('${data['yearMonth'] ?? data['YearMonth'] ?? ''}');
          _loading = false;
        });
      }
    } catch (_) {
      if (mounted) {
        setState(() {
          _loading = false;
          _error = 'تعذر تحميل الإحصائيات';
        });
      }
    }
  }

  double _num(dynamic v) => double.tryParse('$v') ?? 0;

  int _int(dynamic v) => int.tryParse('$v') ?? _num(v).round();

  String _fmt(double v) => Formatters.formatNumber(v);

  String _pick(Map m, List<String> keys) {
    for (final k in keys) {
      final v = m[k];
      if (v != null && '$v'.trim().isNotEmpty) return '$v';
    }
    return '';
  }

  void _openBuckets(Map<String, dynamic> item) {
    Navigator.of(context).push(
      MaterialPageRoute(
        builder: (_) => ListStatsRatingBucketsPage(list: item),
      ),
    );
  }

  void _openActive(Map<String, dynamic> item) {
    final listId = _int(item['listId'] ?? item['ListId']);
    final name = _pick(item, ['listName', 'ListName']);
    Navigator.of(context).push(
      MaterialPageRoute(
        builder: (_) => ListStatsCustomersPage(
          listId: listId,
          listName: name,
          title: 'الزبائن النشطين',
          mode: ListStatsCustomersMode.active,
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Directionality(
      textDirection: TextDirection.rtl,
      child: AppSafeScaffold(
        appBar: AppBar(
          title: Text(
            _yearMonth == null ? 'الإحصائيات' : 'الإحصائيات — $_yearMonth',
            style: const TextStyle(fontFamily: 'Cairo'),
          ),
          backgroundColor: AppTheme.primaryColor,
          actions: [
            IconButton(onPressed: _load, icon: const Icon(Icons.refresh)),
          ],
        ),
        body: _loading
            ? const Center(child: CircularProgressIndicator())
            : _error.isNotEmpty
                ? Center(
                    child: Text(_error,
                        style: const TextStyle(fontFamily: 'Cairo')))
                : _items.isEmpty
                    ? const Center(
                        child: Text(
                          'لا توجد قوائم مرتبطة',
                          style:
                              TextStyle(fontFamily: 'Cairo', color: Colors.grey),
                        ),
                      )
                    : RefreshIndicator(
                        onRefresh: _load,
                        child: ListView.separated(
                          padding: const EdgeInsets.all(12),
                          itemCount: _items.length,
                          separatorBuilder: (_, __) =>
                              const SizedBox(height: 12),
                          itemBuilder: (context, i) {
                            final item = _items[i];
                            return _ListStatsCard(
                              item: item,
                              fmt: _fmt,
                              num: _num,
                              asInt: _int,
                              pick: _pick,
                              onOpen: () => _openBuckets(item),
                              onActive: () => _openActive(item),
                            );
                          },
                        ),
                      ),
      ),
    );
  }
}

class _ListStatsCard extends StatelessWidget {
  const _ListStatsCard({
    required this.item,
    required this.fmt,
    required this.num,
    required this.asInt,
    required this.pick,
    required this.onOpen,
    required this.onActive,
  });

  final Map<String, dynamic> item;
  final String Function(double) fmt;
  final double Function(dynamic) num;
  final int Function(dynamic) asInt;
  final String Function(Map, List<String>) pick;
  final VoidCallback onOpen;
  final VoidCallback onActive;

  @override
  Widget build(BuildContext context) {
    final name = pick(item, ['listName', 'ListName']);
    final score = num(item['averageScore'] ?? item['AverageScore']);
    final label = pick(item, ['finalRatingLabel', 'FinalRatingLabel']);
    final active =
        asInt(item['activeCustomersCount'] ?? item['ActiveCustomersCount']);
    final newCount =
        asInt(item['newCustomersCount'] ?? item['NewCustomersCount']);
    final newSale =
        num(item['newCustomersSaleSum'] ?? item['NewCustomersSaleSum']);
    final newDay = num(
        item['newCustomersDaySalesSum'] ?? item['NewCustomersDaySalesSum']);
    final zeroCount =
        asInt(item['zeroedCustomersCount'] ?? item['ZeroedCustomersCount']);
    final zeroSale =
        num(item['zeroedCustomersSaleSum'] ?? item['ZeroedCustomersSaleSum']);
    final zeroDay = num(item['zeroedCustomersDaySalesSum'] ??
        item['ZeroedCustomersDaySalesSum']);
    final netCount =
        asInt(item['netCustomersCount'] ?? item['NetCustomersCount']);
    final netSale = num(item['netSaleSum'] ?? item['NetSaleSum']);
    final netDay = num(item['netDaySalesSum'] ?? item['NetDaySalesSum']);

    return Material(
      color: Theme.of(context).cardColor,
      borderRadius: BorderRadius.circular(16),
      child: InkWell(
        borderRadius: BorderRadius.circular(16),
        onTap: onOpen,
        child: Container(
          padding: const EdgeInsets.all(14),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(16),
            border: Border.all(
              color: AppTheme.primaryColor.withValues(alpha: 0.25),
            ),
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Expanded(
                    child: Text(
                      name.isEmpty ? 'قائمة' : name,
                      style: const TextStyle(
                        fontFamily: 'Cairo',
                        fontWeight: FontWeight.bold,
                        fontSize: 16,
                      ),
                    ),
                  ),
                  Container(
                    padding:
                        const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
                    decoration: BoxDecoration(
                      color: AppTheme.primaryColor.withValues(alpha: 0.12),
                      borderRadius: BorderRadius.circular(12),
                    ),
                    child: Column(
                      children: [
                        Text(
                          score.toStringAsFixed(score == score.roundToDouble()
                              ? 0
                              : 1),
                          style: const TextStyle(
                            fontFamily: 'Cairo',
                            fontWeight: FontWeight.bold,
                            fontSize: 18,
                            color: AppTheme.primaryColor,
                          ),
                        ),
                        Text(
                          label.isEmpty ? '—' : label,
                          style: const TextStyle(
                            fontFamily: 'Cairo',
                            fontSize: 11,
                            color: AppTheme.primaryColor,
                          ),
                        ),
                      ],
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 12),
              InkWell(
                onTap: onActive,
                child: _metricBlock(
                  'الزبائن النشطين (تسديد 7 أيام > 3.8%)',
                  '$active',
                  accent: true,
                ),
              ),
              const Divider(height: 20),
              _sectionTitle('زبائن جدد هذا الشهر'),
              _metricBlock('العدد', '$newCount'),
              _metricBlock('إجمالي سعر البيع', '${fmt(newSale)} دع'),
              _metricBlock('إجمالي القسط', '${fmt(newDay)} دع'),
              const Divider(height: 20),
              _sectionTitle('مصفرون هذا الشهر'),
              _metricBlock('العدد', '$zeroCount'),
              _metricBlock('إجمالي سعر البيع', '${fmt(zeroSale)} دع'),
              _metricBlock('إجمالي القسط', '${fmt(zeroDay)} دع'),
              const Divider(height: 20),
              _sectionTitle('الفرق (جدد − مصفرون)'),
              _signedMetric('فرق العدد', netCount.toDouble(), isCount: true),
              _signedMetric('فرق سعر البيع', netSale),
              _signedMetric('فرق القسط', netDay),
              const SizedBox(height: 8),
              const Text(
                'اضغط الكارد لعرض جارت التقييم',
                style: TextStyle(
                    fontFamily: 'Cairo', fontSize: 11, color: Colors.grey),
                textAlign: TextAlign.center,
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _sectionTitle(String t) => Padding(
        padding: const EdgeInsets.only(bottom: 6),
        child: Text(t,
            style: const TextStyle(
                fontFamily: 'Cairo',
                fontWeight: FontWeight.bold,
                color: AppTheme.primaryColor)),
      );

  Widget _metricBlock(String label, String value, {bool accent = false}) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 6),
      child: Row(
        children: [
          Expanded(
            child: Text(label,
                style: TextStyle(
                    fontFamily: 'Cairo',
                    color: accent ? AppTheme.primaryColor : Colors.grey,
                    fontWeight: accent ? FontWeight.w600 : FontWeight.normal)),
          ),
          Text(value,
              style: TextStyle(
                  fontFamily: 'Cairo',
                  fontWeight: FontWeight.bold,
                  color: accent ? AppTheme.primaryColor : null)),
        ],
      ),
    );
  }

  Widget _signedMetric(String label, double v, {bool isCount = false}) {
    final color = v > 0
        ? Colors.green.shade700
        : v < 0
            ? Colors.red.shade700
            : Colors.grey.shade700;
    final sign = v > 0 ? '+' : '';
    final text = isCount ? '$sign${v.round()}' : '$sign${fmt(v)} دع';
    return Padding(
      padding: const EdgeInsets.only(bottom: 6),
      child: Row(
        children: [
          Expanded(
              child: Text(label,
                  style:
                      const TextStyle(fontFamily: 'Cairo', color: Colors.grey))),
          Text(text,
              style: TextStyle(
                  fontFamily: 'Cairo',
                  fontWeight: FontWeight.bold,
                  color: color)),
        ],
      ),
    );
  }
}

class ListStatsRatingBucketsPage extends StatelessWidget {
  const ListStatsRatingBucketsPage({super.key, required this.list});

  final Map<String, dynamic> list;

  int _i(dynamic v) => int.tryParse('$v') ?? 0;

  String _pick(List<String> keys) {
    for (final k in keys) {
      final v = list[k];
      if (v != null && '$v'.isNotEmpty) return '$v';
    }
    return '';
  }

  @override
  Widget build(BuildContext context) {
    final listId = _i(list['listId'] ?? list['ListId']);
    final name = _pick(['listName', 'ListName']);
    final score = double.tryParse(
            '${list['averageScore'] ?? list['AverageScore'] ?? 0}') ??
        0;
    final label = _pick(['finalRatingLabel', 'FinalRatingLabel']);
    final buckets = [
      _Bucket('ممتاز', 'excellent', _i(list['excellentCount'] ?? list['ExcellentCount']), Colors.green),
      _Bucket('جيد', 'good', _i(list['goodCount'] ?? list['GoodCount']), Colors.teal),
      _Bucket('ضعيف', 'weak', _i(list['weakCount'] ?? list['WeakCount']), Colors.orange),
      _Bucket('متلكئ', 'rejected', _i(list['rejectedCount'] ?? list['RejectedCount']), const Color(0xFFE65100)),
      _Bucket('سيء', 'bad', _i(list['badCount'] ?? list['BadCount']), Colors.red.shade900),
    ];

    return Directionality(
      textDirection: TextDirection.rtl,
      child: AppSafeScaffold(
        appBar: AppBar(
          title: Text(name.isEmpty ? 'تقييم القائمة' : 'تقييم — $name',
              style: const TextStyle(fontFamily: 'Cairo')),
          backgroundColor: AppTheme.primaryColor,
        ),
        body: ListView(
          padding: const EdgeInsets.all(12),
          children: [
            Container(
              padding: const EdgeInsets.all(16),
              decoration: BoxDecoration(
                color: AppTheme.primaryColor.withValues(alpha: 0.1),
                borderRadius: BorderRadius.circular(16),
              ),
              child: Row(
                children: [
                  Expanded(
                    child: Text(
                      'متوسط التقييم',
                      style: const TextStyle(
                          fontFamily: 'Cairo', fontWeight: FontWeight.bold),
                    ),
                  ),
                  Text(
                    '${score.toStringAsFixed(1)} · ${label.isEmpty ? '—' : label}',
                    style: const TextStyle(
                        fontFamily: 'Cairo',
                        fontWeight: FontWeight.bold,
                        fontSize: 18,
                        color: AppTheme.primaryColor),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 12),
            ...buckets.map((b) => Padding(
                  padding: const EdgeInsets.only(bottom: 10),
                  child: Material(
                    color: Theme.of(context).cardColor,
                    borderRadius: BorderRadius.circular(14),
                    child: InkWell(
                      borderRadius: BorderRadius.circular(14),
                      onTap: () {
                        Navigator.of(context).push(
                          MaterialPageRoute(
                            builder: (_) => ListStatsCustomersPage(
                              listId: listId,
                              listName: name,
                              title: b.title,
                              mode: ListStatsCustomersMode.rating,
                              levelKey: b.key,
                            ),
                          ),
                        );
                      },
                      child: Container(
                        padding: const EdgeInsets.all(14),
                        decoration: BoxDecoration(
                          borderRadius: BorderRadius.circular(14),
                          border: Border.all(color: b.color.withValues(alpha: 0.35)),
                        ),
                        child: Row(
                          children: [
                            Container(
                              width: 10,
                              height: 36,
                              decoration: BoxDecoration(
                                color: b.color,
                                borderRadius: BorderRadius.circular(6),
                              ),
                            ),
                            const SizedBox(width: 12),
                            Expanded(
                              child: Text(b.title,
                                  style: const TextStyle(
                                      fontFamily: 'Cairo',
                                      fontWeight: FontWeight.bold,
                                      fontSize: 16)),
                            ),
                            Text('${b.count}',
                                style: TextStyle(
                                    fontFamily: 'Cairo',
                                    fontWeight: FontWeight.bold,
                                    fontSize: 20,
                                    color: b.color)),
                            const Icon(Icons.chevron_left),
                          ],
                        ),
                      ),
                    ),
                  ),
                )),
          ],
        ),
      ),
    );
  }
}

class _Bucket {
  const _Bucket(this.title, this.key, this.count, this.color);
  final String title;
  final String key;
  final int count;
  final Color color;
}

enum ListStatsCustomersMode { rating, active }

class ListStatsCustomersPage extends StatefulWidget {
  const ListStatsCustomersPage({
    super.key,
    required this.listId,
    required this.listName,
    required this.title,
    required this.mode,
    this.levelKey,
  });

  final int listId;
  final String listName;
  final String title;
  final ListStatsCustomersMode mode;
  final String? levelKey;

  @override
  State<ListStatsCustomersPage> createState() => _ListStatsCustomersPageState();
}

class _ListStatsCustomersPageState extends State<ListStatsCustomersPage> {
  bool _loading = true;
  String _error = '';
  List<Map<String, dynamic>> _rows = [];

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = '';
    });
    try {
      final prefs = await SharedPreferences.getInstance();
      final link = AppEnv.apiBase(fallback: prefs.getString('LinkDelegate') ?? '');
      final asyncId = prefs.getString('AsyncId') ?? '';
      final path = widget.mode == ListStatsCustomersMode.active
          ? 'Followers/ListStats/${widget.listId}/active-customers'
          : 'Followers/ListStats/${widget.listId}/rating-customers';
      final qp = <String, String>{'asyncId': asyncId};
      if (widget.mode == ListStatsCustomersMode.rating) {
        qp['level'] = widget.levelKey ?? 'weak';
      }
      final uri = Uri.parse('$link$path').replace(queryParameters: qp);
      final res = await http.get(uri).timeout(const Duration(seconds: 45));
      if (res.statusCode != 200) throw Exception('http ${res.statusCode}');
      final data = json.decode(res.body) as Map;
      final raw = data['items'] ?? data['Items'] ?? [];
      if (!mounted) return;
      setState(() {
        _rows = (raw as List)
            .map((e) => Map<String, dynamic>.from(e as Map))
            .toList();
        _loading = false;
      });
    } catch (_) {
      if (mounted) {
        setState(() {
          _loading = false;
          _error = 'تعذر تحميل الزبائن';
        });
      }
    }
  }

  String _pick(Map m, List<String> keys) {
    for (final k in keys) {
      final v = m[k];
      if (v != null && '$v'.trim().isNotEmpty) return '$v';
    }
    return '';
  }

  @override
  Widget build(BuildContext context) {
    return Directionality(
      textDirection: TextDirection.rtl,
      child: AppSafeScaffold(
        appBar: AppBar(
          title: Text(
            widget.listName.isEmpty
                ? widget.title
                : '${widget.title} — ${widget.listName}',
            style: const TextStyle(fontFamily: 'Cairo'),
          ),
          backgroundColor: AppTheme.primaryColor,
        ),
        body: _loading
            ? const Center(child: CircularProgressIndicator())
            : _error.isNotEmpty
                ? Center(
                    child: Text(_error,
                        style: const TextStyle(fontFamily: 'Cairo')))
                : _rows.isEmpty
                    ? const Center(
                        child: Text('لا يوجد زبائن',
                            style: TextStyle(
                                fontFamily: 'Cairo', color: Colors.grey)))
                    : ListView.separated(
                        padding: const EdgeInsets.all(12),
                        itemCount: _rows.length,
                        separatorBuilder: (_, __) => const SizedBox(height: 8),
                        itemBuilder: (context, i) {
                          final row = _rows[i];
                          final id = int.tryParse(
                                  '${row['customerId'] ?? row['CustomerId'] ?? 0}') ??
                              0;
                          final name =
                              _pick(row, ['customerName', 'CustomerName']);
                          final phone =
                              _pick(row, ['phoneNumber', 'PhoneNumber']);
                          final pct = row['paidPercent'] ?? row['PaidPercent'];
                          final rating =
                              _pick(row, ['ratingLabel', 'RatingLabel']);
                          return ListTile(
                            shape: RoundedRectangleBorder(
                              borderRadius: BorderRadius.circular(12),
                              side: BorderSide(
                                  color: AppTheme.primaryColor
                                      .withValues(alpha: 0.2)),
                            ),
                            title: Text(name.isEmpty ? 'زبون #$id' : name,
                                style: const TextStyle(
                                    fontFamily: 'Cairo',
                                    fontWeight: FontWeight.bold)),
                            subtitle: Text(
                              [
                                if (phone.isNotEmpty) phone,
                                if (rating.isNotEmpty) rating,
                                if (pct != null) 'نسبة الأسبوع: $pct%',
                              ].join(' · '),
                              style: const TextStyle(fontFamily: 'Cairo'),
                            ),
                            trailing: const Icon(Icons.chevron_left),
                            onTap: () {
                              if (id <= 0) return;
                              Navigator.of(context).push(
                                MaterialPageRoute(
                                  builder: (_) => CustomerProfilePage(
                                    customer: {
                                      'customerId': id,
                                      'customerName': name,
                                      'phoneNumber': phone,
                                    },
                                    listId: widget.listId,
                                    hideSaleRequest: true,
                                  ),
                                ),
                              );
                            },
                          );
                        },
                      ),
      ),
    );
  }
}
