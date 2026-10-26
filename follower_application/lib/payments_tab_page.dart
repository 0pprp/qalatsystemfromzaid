import 'dart:convert';

import 'package:follower_application/AsyncIdChecker.dart';
import 'package:follower_application/config/app_env.dart';
import 'package:follower_application/services/follower_list_cards_cache.dart';
import 'package:follower_application/utils/AppTheme.dart';
import 'package:follower_application/utils/iraq_datetime.dart';
import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'package:intl/intl.dart' hide TextDirection;
import 'package:shared_preferences/shared_preferences.dart';

/// Payments by list + date range — mirrors branch-manager PaymentsPage using
/// Followers/Daily (المسددين) per day so no new BE endpoint is required.
class PaymentsTabPage extends StatefulWidget {
  const PaymentsTabPage({super.key});

  @override
  State<PaymentsTabPage> createState() => _PaymentsTabPageState();
}

class _PaymentsTabPageState extends State<PaymentsTabPage> {
  final _money = NumberFormat('#,##0', 'en');
  final _df = DateFormat('yyyy-MM-dd');

  late DateTime _from;
  late DateTime _to;
  int _listId = 0;

  bool _loadingLists = true;
  bool _loading = false;
  String? _error;
  List<Map<String, dynamic>> _lists = [];
  List<Map<String, dynamic>> _rows = [];

  @override
  void initState() {
    super.initState();
    final now = DateTime.now();
    _to = DateTime(now.year, now.month, now.day);
    _from = _to.subtract(const Duration(days: 6));
    _loadLists();
  }

  String _s(Map? m, List<String> keys) {
    if (m == null) return '';
    for (final k in keys) {
      final v = m[k];
      if (v != null && '$v'.trim().isNotEmpty) return '$v'.trim();
    }
    return '';
  }

  double _d(Map m, List<String> keys) {
    for (final k in keys) {
      final v = m[k];
      if (v is num) return v.toDouble();
      final p = double.tryParse('$v');
      if (p != null) return p;
    }
    return 0;
  }

  Future<Map<String, String>> _session() async {
    final prefs = await SharedPreferences.getInstance();
    return {
      'asyncId': prefs.getString('AsyncId') ?? '',
      'link': AppEnv.apiBase(fallback: prefs.getString('LinkDelegate') ?? ''),
    };
  }

  Future<void> _loadLists() async {
    setState(() {
      _loadingLists = true;
      _error = null;
    });
    try {
      final cached = await FollowerListCardsCache.load();
      var lists = cached ?? <Map<String, dynamic>>[];
      if (lists.isEmpty) {
        final s = await _session();
        final uri = Uri.parse('${s['link']}Followers/ListCards')
            .replace(queryParameters: {'asyncId': s['asyncId']});
        final res = await http.get(uri).timeout(const Duration(seconds: 30));
        if (res.statusCode == 401) {
          await AsyncIdChecker.logout();
          if (mounted) {
            Navigator.pushNamedAndRemoveUntil(context, '/Login', (_) => false);
          }
          return;
        }
        if (res.statusCode != 200) throw Exception('lists HTTP ${res.statusCode}');
        final data = json.decode(res.body) as Map;
        final raw = data['items'] ?? data['Items'] ?? [];
        lists = (raw as List)
            .map((e) {
              final m = Map<String, dynamic>.from(e as Map);
              return {
                'listId':
                    int.tryParse('${m['listId'] ?? m['ListId'] ?? 0}') ?? 0,
                'listName': '${m['listName'] ?? m['ListName'] ?? 'قائمة'}',
              };
            })
            .where((c) => (c['listId'] as int) > 0)
            .toList();
      }
      if (!mounted) return;
      setState(() {
        _lists = lists;
        _loadingLists = false;
        if (_listId == 0 && lists.isNotEmpty) {
          _listId = lists.first['listId'] as int;
        }
      });
      if (_listId > 0) await _load();
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _loadingLists = false;
        _error = '$e';
      });
    }
  }

  Future<void> _load() async {
    if (_listId <= 0) {
      setState(() {
        _rows = [];
        _error = 'اختر قائمة أولاً';
      });
      return;
    }
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final s = await _session();
      final collected = <Map<String, dynamic>>[];
      var day = _from;
      while (!day.isAfter(_to)) {
        final ymd = IraqDateTime.formatYmd(day);
        final uri = Uri.parse('${s['link']}Followers/Daily').replace(
          queryParameters: {
            'asyncId': s['asyncId'],
            'childId': '$_listId',
            'date': ymd,
            'showType': 'المسددين',
          },
        );
        final res = await http.get(uri).timeout(const Duration(seconds: 25));
        if (res.statusCode == 401) {
          await AsyncIdChecker.logout();
          if (mounted) {
            Navigator.pushNamedAndRemoveUntil(context, '/Login', (_) => false);
          }
          return;
        }
        if (res.statusCode == 200) {
          final raw = json.decode(res.body);
          if (raw is List) {
            for (final e in raw) {
              if (e is! Map) continue;
              final m = Map<String, dynamic>.from(e);
              final amount = _d(m, [
                'amountReceverDay',
                'AmountReceverDay',
                'amountReceipt',
                'AmountReceipt',
              ]);
              collected.add({
                'customerName':
                    _s(m, ['customerName', 'CustomerName', 'shopName', 'ShopName']),
                'phoneNumber':
                    _s(m, ['phoneNumber', 'PhoneNumber', 'storePhoneNumber']),
                'amountDenar': amount,
                'paymentDate': ymd,
              });
            }
          }
        }
        day = day.add(const Duration(days: 1));
      }
      if (!mounted) return;
      setState(() {
        _rows = collected;
        _loading = false;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _loading = false;
        _error = '$e';
      });
    }
  }

  Future<void> _pickFrom() async {
    final d = await showDatePicker(
      context: context,
      initialDate: _from,
      firstDate: DateTime(2020),
      lastDate: _to,
    );
    if (d == null) return;
    setState(() => _from = DateTime(d.year, d.month, d.day));
    await _load();
  }

  Future<void> _pickTo() async {
    final d = await showDatePicker(
      context: context,
      initialDate: _to,
      firstDate: _from,
      lastDate: DateTime.now().add(const Duration(days: 1)),
    );
    if (d == null) return;
    setState(() => _to = DateTime(d.year, d.month, d.day));
    await _load();
  }

  double get _totalAmount =>
      _rows.fold<double>(0, (a, r) => a + _d(r, ['amountDenar']));

  int get _count => _rows.length;

  @override
  Widget build(BuildContext context) {
    return RefreshIndicator(
      onRefresh: _load,
      child: ListView(
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.fromLTRB(16, 12, 16, 24),
        children: [
          Container(
            padding: const EdgeInsets.all(14),
            decoration: BoxDecoration(
              gradient: const LinearGradient(
                colors: [Color(0xFF0F5C5C), Color(0xFF1A7A6D)],
              ),
              borderRadius: BorderRadius.circular(18),
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Text(
                  'التسديدات',
                  style: TextStyle(
                    fontFamily: 'Cairo',
                    color: Colors.white,
                    fontWeight: FontWeight.bold,
                    fontSize: 18,
                  ),
                ),
                const SizedBox(height: 6),
                Text(
                  'اختر القائمة والفترة لعرض أسماء المسددين.',
                  style: TextStyle(
                    fontFamily: 'Cairo',
                    color: Colors.white.withValues(alpha: 0.9),
                    fontSize: 12,
                  ),
                ),
                const SizedBox(height: 12),
                Row(
                  children: [
                    Expanded(
                      child: _sumChip(
                        _money.format(_totalAmount),
                        'مجموع المبالغ (دع)',
                      ),
                    ),
                    const SizedBox(width: 8),
                    Expanded(
                      child: _sumChip('$_count', 'عدد التسديدات'),
                    ),
                  ],
                ),
              ],
            ),
          ),
          const SizedBox(height: 12),
          if (_loadingLists)
            const Center(
              child: Padding(
                padding: EdgeInsets.all(24),
                child: CircularProgressIndicator(color: AppTheme.primaryColor),
              ),
            )
          else ...[
            DropdownButtonFormField<int>(
              value: _lists.any((e) => (e['listId'] as int) == _listId)
                  ? _listId
                  : null,
              decoration: const InputDecoration(
                labelText: 'القائمة',
                labelStyle: TextStyle(fontFamily: 'Cairo'),
              ),
              items: _lists
                  .map(
                    (e) => DropdownMenuItem(
                      value: e['listId'] as int,
                      child: Text(
                        '${e['listName']}',
                        style: const TextStyle(fontFamily: 'Cairo'),
                      ),
                    ),
                  )
                  .toList(),
              onChanged: (v) {
                if (v == null) return;
                setState(() => _listId = v);
                _load();
              },
            ),
            const SizedBox(height: 10),
            Row(
              children: [
                Expanded(
                  child: OutlinedButton(
                    onPressed: _pickFrom,
                    child: Text('من ${_df.format(_from)}',
                        style: const TextStyle(fontFamily: 'Cairo', fontSize: 12)),
                  ),
                ),
                const SizedBox(width: 8),
                Expanded(
                  child: OutlinedButton(
                    onPressed: _pickTo,
                    child: Text('إلى ${_df.format(_to)}',
                        style: const TextStyle(fontFamily: 'Cairo', fontSize: 12)),
                  ),
                ),
                IconButton(
                  onPressed: _loading ? null : _load,
                  icon: const Icon(Icons.refresh),
                ),
              ],
            ),
          ],
          const SizedBox(height: 12),
          if (_error != null)
            Text(_error!,
                style: const TextStyle(fontFamily: 'Cairo', color: Colors.red))
          else if (_loading)
            const Padding(
              padding: EdgeInsets.all(32),
              child: Center(
                  child: CircularProgressIndicator(color: AppTheme.primaryColor)),
            )
          else if (_rows.isEmpty)
            const Padding(
              padding: EdgeInsets.all(32),
              child: Center(
                child: Text(
                  'لا تسديدات في هذه الفترة لهذه القائمة',
                  style: TextStyle(fontFamily: 'Cairo', color: Colors.grey),
                ),
              ),
            )
          else
            ..._rows.map((r) {
              final name = _s(r, ['customerName']);
              final phone = _s(r, ['phoneNumber']);
              final amount = _d(r, ['amountDenar']);
              final date = _s(r, ['paymentDate']);
              return Padding(
                padding: const EdgeInsets.only(bottom: 10),
                child: Material(
                  color: Colors.white,
                  borderRadius: BorderRadius.circular(16),
                  child: Container(
                    padding: const EdgeInsets.all(14),
                    decoration: BoxDecoration(
                      borderRadius: BorderRadius.circular(16),
                      border: Border.all(
                        color: AppTheme.primaryColor.withValues(alpha: 0.08),
                      ),
                    ),
                    child: Row(
                      children: [
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(
                                name.isEmpty ? '—' : name,
                                style: const TextStyle(
                                  fontFamily: 'Cairo',
                                  fontWeight: FontWeight.bold,
                                  fontSize: 15,
                                ),
                              ),
                              if (phone.isNotEmpty)
                                Text(phone,
                                    style: const TextStyle(
                                        fontFamily: 'Cairo',
                                        color: Colors.black54,
                                        fontSize: 12)),
                              if (date.isNotEmpty)
                                Text(date,
                                    style: const TextStyle(
                                        fontFamily: 'Cairo',
                                        color: Colors.black54,
                                        fontSize: 12)),
                            ],
                          ),
                        ),
                        Text(
                          '${_money.format(amount)} دع',
                          style: const TextStyle(
                            fontFamily: 'Cairo',
                            fontWeight: FontWeight.bold,
                            color: AppTheme.primaryColor,
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
              );
            }),
        ],
      ),
    );
  }

  Widget _sumChip(String value, String label) {
    return Container(
      padding: const EdgeInsets.symmetric(vertical: 8, horizontal: 10),
      decoration: BoxDecoration(
        color: Colors.white.withValues(alpha: 0.14),
        borderRadius: BorderRadius.circular(12),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(value,
              style: const TextStyle(
                  fontFamily: 'Cairo',
                  color: Colors.white,
                  fontWeight: FontWeight.bold,
                  fontSize: 15)),
          Text(label,
              style: TextStyle(
                  fontFamily: 'Cairo',
                  color: Colors.white.withValues(alpha: 0.8),
                  fontSize: 11)),
        ],
      ),
    );
  }
}
