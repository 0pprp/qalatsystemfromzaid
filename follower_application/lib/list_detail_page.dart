import 'dart:convert';

import 'package:follower_application/AsyncIdChecker.dart';
import 'package:follower_application/config/app_env.dart';
import 'package:follower_application/follower_payers_split.dart';
import 'package:follower_application/follower_unpaid_days_split.dart';
import 'package:follower_application/services/follower_json_cache.dart';
import 'package:follower_application/ui/app_safe_scaffold.dart';
import 'package:follower_application/ui/stripe_container.dart';
import 'package:follower_application/utils/AppTheme.dart';
import 'package:follower_application/utils/Formatters.dart';
import 'package:follower_application/utils/display_name.dart';
import 'package:follower_application/utils/iraq_datetime.dart';
import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';

class ListDetailPage extends StatefulWidget {
  final int listId;
  final String listName;

  const ListDetailPage({
    super.key,
    required this.listId,
    required this.listName,
  });

  @override
  State<ListDetailPage> createState() => _ListDetailPageState();
}

class _ListDetailPageState extends State<ListDetailPage>
    with SingleTickerProviderStateMixin {
  late final TabController _tabs;
  final DateTime _paymentDate = DateTime.now().subtract(const Duration(days: 1));

  bool _loadingPaid = false;
  bool _loadingUnpaid = false;
  bool _loadingCustomers = false;
  bool _loadingStats = false;
  String _error = '';

  List<Map<String, dynamic>> _paid = [];
  List<Map<String, dynamic>> _unpaid = [];
  List<Map<String, dynamic>> _customers = [];
  Map<String, dynamic>? _stats;
  List<Map<String, dynamic>> _remainder100 = [];
  List<Map<String, dynamic>> _remainder200 = [];

  final TextEditingController _search = TextEditingController();
  String _query = '';

  @override
  void initState() {
    super.initState();
    _tabs = TabController(length: 4, vsync: this);
    _tabs.addListener(() {
      if (_tabs.indexIsChanging) return;
      _ensureTabLoaded(_tabs.index);
    });
    _ensureTabLoaded(0);
  }

  @override
  void dispose() {
    _tabs.dispose();
    _search.dispose();
    super.dispose();
  }

  Future<Map<String, String>> _session() async {
    final prefs = await SharedPreferences.getInstance();
    return {
      'asyncId': prefs.getString('AsyncId') ?? '',
      'link': AppEnv.apiBase(fallback: prefs.getString('LinkDelegate') ?? ''),
    };
  }

  Future<void> _ensureTabLoaded(int index) async {
    switch (index) {
      case 0:
        if (_paid.isEmpty && !_loadingPaid) await _loadDaily('المسددين');
        break;
      case 1:
        if (_unpaid.isEmpty && !_loadingUnpaid) await _loadDaily('الغير مسددين');
        break;
      case 2:
        if (_customers.isEmpty && !_loadingCustomers) await _loadCustomers();
        break;
      case 3:
        if (_stats == null && !_loadingStats) await _loadStats();
        break;
    }
  }

  Future<void> _hydrateDailyCache(String showType) async {
    final ymd = IraqDateTime.formatYmd(_paymentDate);
    final cached = await FollowerJsonCache.loadList(
        FollowerJsonCache.listDetailDaily(widget.listId, showType, ymd));
    if (cached == null || cached.isEmpty || !mounted) return;
    setState(() {
      if (showType == 'المسددين') {
        _paid = cached;
      } else {
        _unpaid = cached;
      }
    });
  }

  Future<List<Map<String, dynamic>>> _parseDaily(List raw) async {
    return raw.map((item) {
      final m = item as Map;
      return {
        'customerId':
            int.tryParse('${m['customerID'] ?? m['CustomerID'] ?? m['customerId'] ?? 0}') ??
                0,
        'customerName': '${m['customerName'] ?? m['CustomerName'] ?? ''}',
        'phoneNumber': '${m['phoneNumber'] ?? m['PhoneNumber'] ?? ''}',
        'amountReceipt': _toD(m['amountReceipt'] ?? m['AmountReceipt']),
        'amountDaySales': _toD(m['amountDaySales'] ?? m['AmountDaySales']),
        'amountRemaining': _toD(m['amountRemaining'] ?? m['AmountRemaining']),
        'amountTotalSales': _toD(m['amountTotalSales'] ?? m['AmountTotalSales']),
        'numberOfDayPayment':
            _toI(m['numberOfDayPayment'] ?? m['NumberOfDayPayment']),
      };
    }).toList();
  }

  Future<void> _loadDaily(String showType) async {
    final isPaid = showType == 'المسددين';
    await _hydrateDailyCache(showType);
    final hasCache = isPaid ? _paid.isNotEmpty : _unpaid.isNotEmpty;
    if (!mounted) return;
    setState(() {
      if (isPaid) {
        _loadingPaid = !hasCache;
      } else {
        _loadingUnpaid = !hasCache;
      }
      _error = '';
    });
    try {
      final s = await _session();
      final ymd = IraqDateTime.formatYmd(_paymentDate);
      final uri = Uri.parse('${s['link']}Followers/Daily').replace(queryParameters: {
        'asyncId': s['asyncId'],
        'childId': '${widget.listId}',
        'date': ymd,
        'showType': showType,
      });
      final res = await http.get(uri).timeout(const Duration(seconds: 25));
      if (res.statusCode == 401) {
        await AsyncIdChecker.logout();
        if (mounted) Navigator.pushReplacementNamed(context, '/Login');
        return;
      }
      if (res.statusCode != 200) throw Exception('daily');
      var rows = await _parseDaily(json.decode(res.body) as List);
      if (!isPaid) {
        rows = rows.where((r) => (r['amountRemaining'] as double) > 0).toList();
      }
      await FollowerJsonCache.save(
          FollowerJsonCache.listDetailDaily(widget.listId, showType, ymd), rows);
      if (!mounted) return;
      setState(() {
        if (isPaid) {
          _paid = rows;
          _loadingPaid = false;
        } else {
          _unpaid = rows;
          _loadingUnpaid = false;
        }
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        if (!hasCache) _error = 'تعذر تحميل البيانات';
        _loadingPaid = false;
        _loadingUnpaid = false;
      });
    }
  }

  Future<void> _loadCustomers() async {
    final cached = await FollowerJsonCache.loadList(
        FollowerJsonCache.listDetailCustomers(widget.listId));
    if (cached != null && cached.isNotEmpty && mounted) {
      setState(() {
        _customers = cached;
        _loadingCustomers = false;
      });
    } else if (mounted) {
      setState(() {
        _loadingCustomers = true;
        _error = '';
      });
    }
    try {
      final s = await _session();
      // One round-trip: level=all (BE evaluates facts once).
      final uri = Uri.parse(
              '${s['link']}Followers/ListStats/${widget.listId}/rating-customers')
          .replace(queryParameters: {
        'asyncId': s['asyncId'],
        'level': 'all',
      });
      final res = await http.get(uri).timeout(const Duration(seconds: 25));
      if (res.statusCode != 200) throw Exception('customers');
      final data = json.decode(res.body) as Map;
      final raw = data['items'] ?? data['Items'] ?? [];
      final rows = <Map<String, dynamic>>[];
      for (final e in (raw as List)) {
        if (e is! Map) continue;
        final id = int.tryParse('${e['customerId'] ?? e['CustomerId'] ?? 0}') ?? 0;
        if (id <= 0) continue;
        rows.add({
          'customerId': id,
          'customerName': '${e['customerName'] ?? e['CustomerName'] ?? ''}',
          'phoneNumber': '${e['phoneNumber'] ?? e['PhoneNumber'] ?? ''}',
          'score': e['score'] ?? e['Score'],
          'ratingLabel': '${e['ratingLabel'] ?? e['RatingLabel'] ?? ''}',
          'amountRemaining': _toD(e['amountRemaining'] ?? e['AmountRemaining']),
        });
      }
      rows.sort((a, b) =>
          '${a['customerName']}'.compareTo('${b['customerName']}'));
      await FollowerJsonCache.save(
          FollowerJsonCache.listDetailCustomers(widget.listId), rows);
      if (!mounted) return;
      setState(() {
        _customers = rows;
        _loadingCustomers = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _loadingCustomers = false;
        if (_customers.isEmpty) _error = 'تعذر تحميل الزبائن';
      });
    }
  }

  Future<void> _loadStats() async {
    final cached = await FollowerJsonCache.loadMap(
        FollowerJsonCache.listDetailStats(widget.listId));
    if (cached != null && mounted) {
      setState(() {
        _stats = Map<String, dynamic>.from(cached['stats'] as Map? ?? {});
        if (_stats!.isEmpty) _stats = null;
        _remainder100 = ((cached['r100'] as List?) ?? [])
            .map((e) => Map<String, dynamic>.from(e as Map))
            .toList();
        _remainder200 = ((cached['r200'] as List?) ?? [])
            .map((e) => Map<String, dynamic>.from(e as Map))
            .toList();
        _loadingStats = _stats == null;
      });
    } else if (mounted) {
      setState(() {
        _loadingStats = true;
        _error = '';
      });
    }
    try {
      final s = await _session();
      // Single-list endpoint — do not pull every follower list.
      final statsUri =
          Uri.parse('${s['link']}Followers/ListStats/${widget.listId}')
              .replace(queryParameters: {'asyncId': s['asyncId']});
      Future<List<Map<String, dynamic>>> rem({
        required double min,
        required double max,
      }) async {
        final uri = Uri.parse(
                '${s['link']}Followers/ListStats/${widget.listId}/remainder-customers')
            .replace(queryParameters: {
          'asyncId': s['asyncId']!,
          'minRemaining': min.toStringAsFixed(0),
          'maxRemaining': max.toStringAsFixed(0),
        });
        final res = await http.get(uri).timeout(const Duration(seconds: 20));
        if (res.statusCode != 200) return [];
        final body = json.decode(res.body) as Map;
        final raw = body['items'] ?? body['Items'] ?? [];
        return (raw as List)
            .map((e) => Map<String, dynamic>.from(e as Map))
            .toList();
      }

      final statsFut = http.get(statsUri).timeout(const Duration(seconds: 25));
      // Bands must not overlap: [0,100k) then [100k,200k).
      final remFut = Future.wait([
        rem(min: 0, max: 100000),
        rem(min: 100000, max: 200000),
      ]);
      final statsRes = await statsFut;
      final rems = await remFut;

      if (statsRes.statusCode != 200) throw Exception('stats');
      final data = json.decode(statsRes.body) as Map;
      final item = data['item'] ?? data['Item'];
      if (item is! Map) throw Exception('stats-empty');
      final mine = Map<String, dynamic>.from(item);
      final r100 = rems[0];
      final r200 = rems[1];
      await FollowerJsonCache.save(
        FollowerJsonCache.listDetailStats(widget.listId),
        {'stats': mine, 'r100': r100, 'r200': r200},
      );
      if (!mounted) return;
      setState(() {
        _stats = mine;
        _remainder100 = r100;
        _remainder200 = r200;
        _loadingStats = false;
        _error = '';
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _loadingStats = false;
        if (_stats == null) _error = 'تعذر تحميل الإحصائيات';
      });
    }
  }

  double _toD(dynamic v) {
    if (v == null) return 0;
    if (v is num) return v.toDouble();
    return double.tryParse('$v') ?? 0;
  }

  int _toI(dynamic v) {
    if (v == null) return 0;
    if (v is int) return v;
    if (v is num) return v.toInt();
    return int.tryParse('$v') ?? 0;
  }

  void _openProfile(Map<String, dynamic> row) {
    Navigator.pushNamed(context, '/CustomerProfile', arguments: {
      'customer': row,
      'listId': widget.listId,
    });
  }

  @override
  Widget build(BuildContext context) {
    return Directionality(
      textDirection: TextDirection.rtl,
      child: AppSafeScaffold(
        backgroundColor: AppTheme.backgroundColor,
        appBar: AppBar(
          title: PersonNameText(
            widget.listName,
            style: const TextStyle(
                fontFamily: 'Cairo',
                fontWeight: FontWeight.bold,
                color: Colors.white,
                fontSize: 18),
          ),
          backgroundColor: AppTheme.primaryColor,
          foregroundColor: Colors.white,
          bottom: AppTheme.primaryAppBarTabBar(
            controller: _tabs,
            isScrollable: true,
            tabs: const [
              Tab(text: 'مسددين'),
              Tab(text: 'غير مسددين'),
              Tab(text: 'الزبائن'),
              Tab(text: 'الإحصائيات'),
            ],
          ),
        ),
        body: TabBarView(
          controller: _tabs,
          children: [
            _paidTab(),
            _unpaidTab(),
            _customersTab(),
            _statsTab(),
          ],
        ),
      ),
    );
  }

  Widget _paidTab() {
    if (_loadingPaid) return const Center(child: CircularProgressIndicator());
    final under = FollowerPayersSplit.underDue(_paid);
    final correct = FollowerPayersSplit.correct(_paid);
    return RefreshIndicator(
      onRefresh: () => _loadDaily('المسددين'),
      child: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          if (_error.isNotEmpty)
            Text(_error, style: const TextStyle(fontFamily: 'Cairo', color: Colors.red)),
          _section('المسددين دون القسط', under, Colors.orange.shade700),
          const SizedBox(height: 12),
          _section('مسدد بشكل صحيح', correct, Colors.green.shade700),
        ],
      ),
    );
  }

  Widget _unpaidTab() {
    if (_loadingUnpaid) return const Center(child: CircularProgressIndicator());
    return RefreshIndicator(
      onRefresh: () => _loadDaily('الغير مسددين'),
      child: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          _section('1–3 أيام', FollowerUnpaidDaysSplit.days1to3(_unpaid),
              Colors.blue.shade700),
          const SizedBox(height: 12),
          _section('4–7 أيام', FollowerUnpaidDaysSplit.days4to7(_unpaid),
              Colors.teal.shade700),
          const SizedBox(height: 12),
          _section('8–14 يوم', FollowerUnpaidDaysSplit.days8to14(_unpaid),
              Colors.deepOrange.shade700),
          const SizedBox(height: 12),
          _section('15+ يوم', FollowerUnpaidDaysSplit.days15Plus(_unpaid),
              Colors.red.shade700),
        ],
      ),
    );
  }

  Widget _customersTab() {
    if (_loadingCustomers) {
      return const Center(child: CircularProgressIndicator());
    }
    final q = _query.trim();
    final rows = q.isEmpty
        ? _customers
        : _customers
            .where((c) =>
                '${c['customerName']}'.contains(q) ||
                '${c['phoneNumber']}'.contains(q))
            .toList();
    return Column(
      children: [
        Padding(
          padding: const EdgeInsets.all(12),
          child: TextField(
            controller: _search,
            onChanged: (v) => setState(() => _query = v),
            decoration: const InputDecoration(
              hintText: 'بحث بالاسم أو الهاتف',
              hintStyle: TextStyle(fontFamily: 'Cairo'),
              prefixIcon: Icon(Icons.search),
              border: OutlineInputBorder(),
            ),
            style: const TextStyle(fontFamily: 'Cairo'),
          ),
        ),
        Expanded(
          child: RefreshIndicator(
            onRefresh: _loadCustomers,
            child: ListView.separated(
              padding: const EdgeInsets.fromLTRB(16, 0, 16, 24),
              itemCount: rows.length,
              separatorBuilder: (_, __) => const NameListSeparator(),
              itemBuilder: (_, i) {
                final c = rows[i];
                final score = c['score'];
                return ListTile(
                  shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(12)),
                  tileColor: Theme.of(context).cardColor,
                  title: PersonNameText('${c['customerName']}'),
                  subtitle: Text(
                    'التقييم: ${score ?? '—'}'
                    '${'${c['ratingLabel']}'.isEmpty ? '' : ' — ${c['ratingLabel']}'}',
                    style: const TextStyle(fontFamily: 'Cairo'),
                  ),
                  onTap: () => _openProfile(c),
                );
              },
            ),
          ),
        ),
      ],
    );
  }

  Widget _statsTab() {
    if (_loadingStats) return const Center(child: CircularProgressIndicator());
    final s = _stats;
    if (s == null) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(
                _error.isEmpty ? 'لا توجد إحصائيات' : _error,
                style: const TextStyle(fontFamily: 'Cairo'),
                textAlign: TextAlign.center,
              ),
              const SizedBox(height: 12),
              TextButton(
                onPressed: _loadStats,
                child: const Text('إعادة المحاولة',
                    style: TextStyle(fontFamily: 'Cairo')),
              ),
            ],
          ),
        ),
      );
    }
    final avg = _toD(s['averageScore'] ?? s['AverageScore']);
    final buckets = [
      _bucket('ممتاز', s['excellentCount'] ?? s['ExcellentCount'], 'excellent'),
      _bucket('جيد', s['goodCount'] ?? s['GoodCount'], 'good'),
      _bucket('ضعيف', s['weakCount'] ?? s['WeakCount'], 'weak'),
      _bucket('متلكئ', s['rejectedCount'] ?? s['RejectedCount'], 'rejected'),
      _bucket('سيء', s['badCount'] ?? s['BadCount'], 'bad'),
    ];
    return RefreshIndicator(
      onRefresh: _loadStats,
      child: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          Container(
            padding: const EdgeInsets.all(16),
            decoration: BoxDecoration(
              color: Theme.of(context).cardColor,
              borderRadius: BorderRadius.circular(16),
            ),
            child: Column(
              children: [
                Text('متوسط التقييم: ${avg.toStringAsFixed(1)}',
                    style: const TextStyle(
                        fontFamily: 'Cairo',
                        fontWeight: FontWeight.bold,
                        fontSize: 18)),
                const SizedBox(height: 8),
                Text('${s['finalRatingLabel'] ?? s['FinalRatingLabel'] ?? ''}',
                    style: const TextStyle(fontFamily: 'Cairo', color: Colors.grey)),
                const SizedBox(height: 16),
                ...buckets,
              ],
            ),
          ),
          const SizedBox(height: 16),
          _remainderCard('باقي أقل من 100,000', _remainder100, stripeIndex: 0),
          const SizedBox(height: 12),
          _remainderCard(
              'باقي من 100,000 إلى أقل من 200,000', _remainder200,
              stripeIndex: 1),
        ],
      ),
    );
  }

  Widget _bucket(String title, dynamic count, String level) {
    final n = _toI(count);
    return ListTile(
      contentPadding: EdgeInsets.zero,
      title: Text(title, style: const TextStyle(fontFamily: 'Cairo')),
      trailing: Text('$n',
          style: const TextStyle(fontFamily: 'Cairo', fontWeight: FontWeight.bold)),
      onTap: () => Navigator.pushNamed(context, '/ListStats'),
    );
  }

  Widget _remainderCard(String title, List<Map<String, dynamic>> rows,
      {int stripeIndex = 0}) {
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: StripeContainer.decoration(stripeIndex),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text(title,
              style: const TextStyle(
                  fontFamily: 'Cairo', fontWeight: FontWeight.bold)),
          const SizedBox(height: 8),
          if (rows.isEmpty)
            const Text('لا يوجد',
                style: TextStyle(fontFamily: 'Cairo', color: Colors.grey))
          else
            ...withNameSeparators(rows.take(40).map((r) {
              final name = '${r['customerName'] ?? r['CustomerName'] ?? ''}';
              final rem = _toD(r['amountRemaining'] ?? r['AmountRemaining']);
              return ListTile(
                dense: true,
                contentPadding: EdgeInsets.zero,
                title: PersonNameText(name,
                    style: const TextStyle(fontFamily: 'Cairo', fontSize: 15)),
                trailing: Text(Formatters.formatNumber(rem),
                    style: const TextStyle(fontFamily: 'Cairo')),
                onTap: () => _openProfile({
                  'customerId':
                      int.tryParse('${r['customerId'] ?? r['CustomerId'] ?? 0}') ??
                          0,
                  'customerName': name,
                  'phoneNumber': '${r['phoneNumber'] ?? r['PhoneNumber'] ?? ''}',
                  'amountRemaining': rem,
                }),
              );
            })),
        ],
      ),
    );
  }

  Widget _section(String title, List<Map<String, dynamic>> rows, Color accent) {
    return Container(
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: Theme.of(context).cardColor,
        borderRadius: BorderRadius.circular(14),
        border: Border(right: BorderSide(color: accent, width: 4)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text('$title (${rows.length})',
              style: TextStyle(
                  fontFamily: 'Cairo',
                  fontWeight: FontWeight.bold,
                  color: accent)),
          const SizedBox(height: 8),
          if (rows.isEmpty)
            const Text('لا يوجد',
                style: TextStyle(fontFamily: 'Cairo', color: Colors.grey))
          else
            ...withNameSeparators(rows.map((c) => ListTile(
                  dense: true,
                  contentPadding: EdgeInsets.zero,
                  title: PersonNameText('${c['customerName']}',
                      style: const TextStyle(
                          fontFamily: 'Cairo', fontSize: 15)),
                  subtitle: Text(
                    'الباقي: ${Formatters.formatNumber(c['amountRemaining'] as double)}',
                    style:
                        const TextStyle(fontFamily: 'Cairo', fontSize: 12),
                  ),
                  onTap: () => _openProfile(c),
                ))),
        ],
      ),
    );
  }
}
