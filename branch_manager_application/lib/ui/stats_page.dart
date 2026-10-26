import 'dart:math' as math;

import 'package:branch_manager_application/core/api.dart';
import 'package:branch_manager_application/core/session.dart';
import 'package:branch_manager_application/theme/bm_theme.dart';
import 'package:branch_manager_application/ui/customer_profile_page.dart';
import 'package:flutter/material.dart';
import 'package:intl/intl.dart' hide TextDirection;

class StatsPage extends StatefulWidget {
  const StatsPage({super.key});

  @override
  State<StatsPage> createState() => _StatsPageState();
}

class _StatsPageState extends State<StatsPage> {
  final _score = NumberFormat('0.##', 'en');
  final _money = NumberFormat('#,##0', 'en');
  late DateTime _from;
  late DateTime _to;

  String _scope = 'branch'; // branch | list | follower | employee
  int _entityId = 0;
  int _drilledListId = 0;
  int _mainTab = 0; // 0 = تقييم، 1 = تقييمات متدنية

  bool _loadingCatalog = true;
  bool _loadingTrend = true;
  bool _loadingMonth = false;
  bool _loadingFollowerCards = false;
  bool _loadingAlerts = false;
  String? _error;

  List<Map<String, dynamic>> _lists = [];
  List<Map<String, dynamic>> _followers = [];
  List<Map<String, dynamic>> _employees = [];
  List<_FollowerListCard> _followerListCards = [];
  Map<String, dynamic>? _trend;
  Map<String, dynamic>? _monthStats;
  List<Map<String, dynamic>> _declineAlerts = [];

  static const _sand = Color(0xFFEEF3EF);
  static const _accentDeep = Color(0xFF0F3D32);
  static const _accent = Color(0xFF1F6F5B);
  static const _warn = Color(0xFFB45309);

  @override
  void initState() {
    super.initState();
    final now = DateTime.now();
    _to = DateTime(now.year, now.month, now.day);
    // افتراضي: شهر كامل متحرك — مثلاً 6/10 → من 6/9 إلى 6/10
    _from = _to.subtract(const Duration(days: 30));
    _bootstrap();
  }

  Future<void> _bootstrap() async {
    await _loadCatalog();
    await Future.wait([_loadTrend(), _loadMonthStats(), _loadDeclineAlerts()]);
  }

  ({String scope, int id}) get _effectiveScope {
    if (_drilledListId > 0) return (scope: 'list', id: _drilledListId);
    return (scope: _scope, id: _entityId);
  }

  bool get _showFollowerCards =>
      _scope == 'follower' && _entityId > 0 && _drilledListId <= 0;

  bool get _showTrendDetail =>
      !_showFollowerCards &&
      (_scope == 'branch' || _entityId > 0 || _drilledListId > 0);

  Future<void> _loadCatalog() async {
    setState(() {
      _loadingCatalog = true;
      _error = null;
    });
    try {
      final cat = await BmApi.instance.statsCatalog();
      if (!mounted) return;
      setState(() {
        _lists = _asMaps(cat['lists'] ?? cat['Lists']);
        _followers = _asMaps(cat['followers'] ?? cat['Followers']);
        _employees = _asMaps(cat['employees'] ?? cat['Employees']);
        _loadingCatalog = false;
      });
    } catch (e) {
      if (!mounted) return;
      if (e is BmApiException && e.statusCode == 401) {
        await BmSession.instance.clear();
      }
      setState(() {
        _loadingCatalog = false;
        _error = '$e';
      });
    }
  }

  Future<void> _loadTrend() async {
    if (_showFollowerCards) {
      setState(() {
        _trend = null;
        _loadingTrend = false;
      });
      return;
    }
    final eff = _effectiveScope;
    setState(() {
      _loadingTrend = true;
      _error = null;
    });
    try {
      final trend = await BmApi.instance.statsTrend(
        scope: eff.scope,
        id: eff.id,
        from: _from,
        to: _to,
      );
      if (!mounted) return;
      setState(() {
        _trend = trend;
        _loadingTrend = false;
      });
    } catch (e) {
      if (!mounted) return;
      if (e is BmApiException && e.statusCode == 401) {
        await BmSession.instance.clear();
      }
      setState(() {
        _loadingTrend = false;
        _error = '$e';
      });
    }
  }

  Future<void> _loadMonthStats() async {
    final eff = _showFollowerCards
        ? (scope: 'follower', id: _entityId)
        : _effectiveScope;
    if (eff.scope != 'branch' && eff.id <= 0) {
      setState(() {
        _monthStats = null;
        _loadingMonth = false;
      });
      return;
    }
    setState(() => _loadingMonth = true);
    try {
      final data = await BmApi.instance.statsBranchMonth(
        scope: eff.scope,
        id: eff.id,
      );
      if (!mounted) return;
      setState(() {
        _monthStats = data;
        _loadingMonth = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _monthStats = null;
        _loadingMonth = false;
      });
    }
  }

  Future<void> _loadFollowerListCards() async {
    if (!_showFollowerCards) {
      setState(() {
        _followerListCards = [];
        _loadingFollowerCards = false;
      });
      return;
    }
    final idSet = <int>{};
    for (final f in _followers) {
      if ((int.tryParse(_s(f, ['id', 'Id'])) ?? -1) != _entityId) continue;
      final rawIds = f['assignedListIds'] ?? f['AssignedListIds'];
      if (rawIds is List) {
        for (final x in rawIds) {
          final n = x is int ? x : int.tryParse('$x');
          if (n != null && n > 0) idSet.add(n);
        }
      }
      break;
    }
    final lists = <Map<String, dynamic>>[];
    for (final l in _lists) {
      final lid = int.tryParse(_s(l, ['id', 'Id'])) ?? 0;
      if (lid > 0 && idSet.contains(lid)) {
        lists.add(l);
      }
    }

    final followerId = _entityId;
    setState(() => _loadingFollowerCards = true);
    try {
      final settled = await Future.wait(lists.map((l) async {
        final id = int.tryParse(_s(l, ['id', 'Id'])) ?? 0;
        final name = _s(l, ['name', 'Name']);
        try {
          final data = await BmApi.instance.statsTrend(
            scope: 'list',
            id: id,
            from: _from,
            to: _to,
          );
          return _FollowerListCard(
            id: id,
            name: name,
            score: _d(data, ['phantomEndValue', 'PhantomEndValue']).abs() > 0 ||
                    data['phantomEndValue'] != null ||
                    data['PhantomEndValue'] != null
                ? _d(data, ['phantomEndValue', 'PhantomEndValue'])
                : _d(data, ['endValue', 'EndValue']),
            ratingLabel: () {
              final p = _s(data, ['phantomRatingLabel', 'PhantomRatingLabel']);
              if (p.isNotEmpty) return p;
              return _s(data, ['ratingLabel', 'RatingLabel']);
            }(),
            customerCount: () {
              final pe = data['phantomCustomerCount'] ?? data['PhantomCustomerCount'];
              if (pe != null) {
                return _d(data, ['phantomCustomerCount', 'PhantomCustomerCount']).toInt();
              }
              return _d(data, ['customerCount', 'CustomerCount']).toInt();
            }(),
            direction: _s(data, ['direction', 'Direction']).toLowerCase(),
            hasScore: data['phantomEndValue'] != null ||
                data['PhantomEndValue'] != null ||
                data['endValue'] != null ||
                data['EndValue'] != null,
          );
        } catch (_) {
          return _FollowerListCard(
            id: id,
            name: name,
            score: 0,
            ratingLabel: '—',
            customerCount: null,
            direction: 'flat',
            hasScore: false,
          );
        }
      }));
      if (!mounted) return;
      if (_scope != 'follower' || _entityId != followerId || _drilledListId > 0) {
        return;
      }
      settled.sort((a, b) => a.name.compareTo(b.name));
      setState(() {
        _followerListCards = settled;
        _loadingFollowerCards = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() => _loadingFollowerCards = false);
    }
  }

  Future<void> _loadDeclineAlerts({bool refresh = true}) async {
    setState(() => _loadingAlerts = true);
    try {
      final rows = await BmApi.instance.listDeclineAlerts(refresh: refresh);
      if (!mounted) return;
      setState(() {
        _declineAlerts = rows;
        _loadingAlerts = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() => _loadingAlerts = false);
    }
  }

  void _openFollowerListCard(int listId) {
    if (listId <= 0) return;
    setState(() {
      _drilledListId = listId;
      _trend = null;
      _monthStats = null;
    });
    _loadTrend();
    _loadMonthStats();
  }

  void _clearFollowerDrill() {
    setState(() {
      _drilledListId = 0;
      _trend = null;
      _monthStats = null;
    });
    _loadFollowerListCards();
  }

  List<Map<String, dynamic>> _asMaps(dynamic raw) {
    if (raw is! List) return [];
    return raw.map((e) => Map<String, dynamic>.from(e as Map)).toList();
  }

  String _s(Map? m, List<String> keys) {
    if (m == null) return '';
    for (final k in keys) {
      final v = m[k];
      if (v != null && '$v'.trim().isNotEmpty) return '$v'.trim();
    }
    return '';
  }

  double _d(Map? m, List<String> keys) {
    if (m == null) return 0;
    for (final k in keys) {
      final v = m[k];
      if (v is num) return v.toDouble();
      final p = double.tryParse('$v');
      if (p != null) return p;
    }
    return 0;
  }

  Map<String, dynamic>? _monthBlock(String which) {
    final raw = _monthStats;
    if (raw == null) return null;
    final block = which == 'previous'
        ? (raw['previous'] ?? raw['Previous'])
        : (raw['current'] ?? raw['Current']);
    if (block is Map) return Map<String, dynamic>.from(block);
    return null;
  }

  String _formatYm(int ym) {
    if (ym <= 0) return '';
    final y = ym ~/ 100;
    final m = ym % 100;
    return '$y/${m.toString().padLeft(2, '0')}';
  }

  String _signedInt(int n) => n > 0 ? '+$n' : '$n';

  String _signedMoney(num n) {
    final s = _money.format(n.abs());
    if (n > 0) return '+$s';
    if (n < 0) return '-$s';
    return s;
  }

  Future<void> _pickFrom() async {
    final d = await showDatePicker(
      context: context,
      initialDate: _from,
      firstDate: DateTime(2020),
      lastDate: DateTime.now().add(const Duration(days: 1)),
    );
    if (d == null) return;
    setState(() => _from = DateTime(d.year, d.month, d.day));
    await _loadTrend();
    if (_showFollowerCards) await _loadFollowerListCards();
  }

  Future<void> _pickTo() async {
    final d = await showDatePicker(
      context: context,
      initialDate: _to,
      firstDate: DateTime(2020),
      lastDate: DateTime.now().add(const Duration(days: 1)),
    );
    if (d == null) return;
    setState(() => _to = DateTime(d.year, d.month, d.day));
    await _loadTrend();
    if (_showFollowerCards) await _loadFollowerListCards();
  }

  void _setScope(String scope) {
    setState(() {
      _scope = scope;
      _entityId = 0;
      _drilledListId = 0;
      _followerListCards = [];
      if (scope == 'list' && _lists.isNotEmpty) {
        _entityId = int.tryParse(_s(_lists.first, ['id', 'Id'])) ?? 0;
      } else if (scope == 'follower' && _followers.isNotEmpty) {
        _entityId = int.tryParse(_s(_followers.first, ['id', 'Id'])) ?? 0;
      } else if (scope == 'employee' && _employees.isNotEmpty) {
        _entityId = int.tryParse(_s(_employees.first, ['id', 'Id'])) ?? 0;
      }
    });
    if (scope == 'follower' && _entityId > 0) {
      setState(() => _trend = null);
      _loadFollowerListCards();
      _loadMonthStats();
    } else {
      _loadTrend();
      _loadMonthStats();
    }
  }

  List<Map<String, dynamic>> get _entityOptions {
    switch (_scope) {
      case 'list':
        return _lists;
      case 'follower':
        return _followers;
      case 'employee':
        return _employees;
      default:
        return const [];
    }
  }

  bool get _isEmployeeScope =>
      _effectiveScope.scope == 'employee' ||
      (_scope == 'employee' && _drilledListId <= 0);

  String _bucketKey(String labelAr) {
    switch (labelAr) {
      case 'ممتاز':
        return 'excellent';
      case 'جيد':
        return 'good';
      case 'ضعيف':
        return 'weak';
      case 'متلكئ':
      case 'مرفوض':
        return 'rejected';
      case 'سيء':
        return 'bad';
      default:
        return '';
    }
  }

  Future<void> _openCustomersSheet({
    String? level,
    required String title,
    bool phantom = true,
  }) async {
    final eff = _effectiveScope;
    if (eff.scope != 'branch' && eff.id <= 0) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('اختر عنصراً أولاً', style: TextStyle(fontFamily: 'Cairo'))),
      );
      return;
    }

    await showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.white,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
      ),
      builder: (ctx) {
        return _RatingCustomersSheet(
          title: title,
          scope: eff.scope,
          entityId: eff.id,
          from: _from,
          to: _to,
          level: level,
          phantom: phantom,
        );
      },
    );
  }

  Future<void> _openDeclineAlert(Map<String, dynamic> row) async {
    final id = int.tryParse(_s(row, ['id', 'Id'])) ?? 0;
    if (id <= 0) return;
    try {
      await BmApi.instance.markDeclineAlertRead(id);
    } catch (_) {}
    if (!mounted) return;
    final subjectType = _s(row, ['subjectType', 'SubjectType']).toLowerCase();
    final subjectId = int.tryParse(_s(row, ['subjectId', 'SubjectId'])) ?? 0;
    final subjectName = _s(row, ['subjectName', 'SubjectName']);
    final fromScore = _d(row, ['fromScore', 'FromScore']);
    final toScore = _d(row, ['toScore', 'ToScore']);
    final message = _s(row, ['message', 'Message']);

    setState(() {
      _mainTab = 0;
      _drilledListId = 0;
      if (subjectType == 'employee') {
        _scope = 'employee';
        _entityId = subjectId;
      } else {
        _scope = 'follower';
        _entityId = subjectId;
      }
      _trend = null;
    });
    await Future.wait([_loadTrend(), _loadMonthStats(), _loadDeclineAlerts(refresh: false)]);
    if (!mounted) return;
    await showDialog<void>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: Text(
          subjectName.isEmpty ? 'تقييم متدنٍ' : subjectName,
          style: const TextStyle(fontFamily: 'Cairo'),
        ),
        content: Text(
          message.isNotEmpty
              ? message
              : 'نزل من ${_score.format(fromScore)} إلى ${_score.format(toScore)}',
          style: const TextStyle(fontFamily: 'Cairo', height: 1.5),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx),
            child: const Text('إغلاق', style: TextStyle(fontFamily: 'Cairo')),
          ),
        ],
      ),
    );
  }

  int get _unreadAlertCount => _declineAlerts.where((a) {
        final v = a['isRead'] ?? a['IsRead'];
        return v == false || v == 0 || '$v'.toLowerCase() == 'false';
      }).length;

  @override
  Widget build(BuildContext context) {
    final unread = _unreadAlertCount;
    return Directionality(
      textDirection: TextDirection.rtl,
      child: Column(
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 8, 16, 0),
            child: Row(
              children: [
                Expanded(
                  child: ChoiceChip(
                    label: const Text('التقييم', style: TextStyle(fontFamily: 'Cairo')),
                    selected: _mainTab == 0,
                    onSelected: (_) => setState(() => _mainTab = 0),
                    selectedColor: _accent.withValues(alpha: 0.18),
                  ),
                ),
                const SizedBox(width: 8),
                Expanded(
                  child: ChoiceChip(
                    label: Text(
                      unread > 0 ? 'تقييمات متدنية ($unread)' : 'تقييمات متدنية',
                      style: const TextStyle(fontFamily: 'Cairo'),
                    ),
                    selected: _mainTab == 1,
                    onSelected: (_) {
                      setState(() => _mainTab = 1);
                      _loadDeclineAlerts();
                    },
                    selectedColor: _warn.withValues(alpha: 0.2),
                  ),
                ),
              ],
            ),
          ),
          Expanded(
            child: _mainTab == 1 ? _buildDeclineInbox() : _buildRatingsBody(),
          ),
        ],
      ),
    );
  }

  Widget _buildDeclineInbox() {
    if (_loadingAlerts) {
      return const Center(child: CircularProgressIndicator());
    }
    if (_declineAlerts.isEmpty) {
      return RefreshIndicator(
        onRefresh: () => _loadDeclineAlerts(refresh: true),
        child: ListView(
          physics: const AlwaysScrollableScrollPhysics(),
          padding: const EdgeInsets.all(24),
          children: const [
            SizedBox(height: 80),
            Text(
              'لا إشعارات نزول تقييم حالياً',
              textAlign: TextAlign.center,
              style: TextStyle(fontFamily: 'Cairo', color: BmTheme.muted),
            ),
          ],
        ),
      );
    }
    return RefreshIndicator(
      onRefresh: () => _loadDeclineAlerts(refresh: true),
      child: ListView.separated(
        padding: const EdgeInsets.fromLTRB(16, 12, 16, 24),
        itemCount: _declineAlerts.length,
        separatorBuilder: (_, __) => const SizedBox(height: 8),
        itemBuilder: (context, i) {
          final row = _declineAlerts[i];
          final msg = _s(row, ['message', 'Message']);
          final name = _s(row, ['subjectName', 'SubjectName']);
          final from = _d(row, ['fromScore', 'FromScore']);
          final to = _d(row, ['toScore', 'ToScore']);
          final unread = () {
            final v = row['isRead'] ?? row['IsRead'];
            return v == false || v == 0 || '$v'.toLowerCase() == 'false';
          }();
          return Material(
            color: unread ? const Color(0xFFFFF7ED) : _sand,
            borderRadius: BorderRadius.circular(14),
            child: InkWell(
              borderRadius: BorderRadius.circular(14),
              onTap: () => _openDeclineAlert(row),
              child: Padding(
                padding: const EdgeInsets.all(14),
                child: Row(
                  children: [
                    if (unread)
                      Container(
                        width: 10,
                        height: 10,
                        margin: const EdgeInsets.only(left: 10),
                        decoration: const BoxDecoration(
                          color: _warn,
                          shape: BoxShape.circle,
                        ),
                      ),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            msg.isNotEmpty
                                ? msg
                                : 'نزل ${name.isEmpty ? '—' : name} من ${_score.format(from)} إلى ${_score.format(to)}',
                            style: TextStyle(
                              fontFamily: 'Cairo',
                              fontWeight: unread ? FontWeight.w700 : FontWeight.w500,
                              fontSize: 14,
                              height: 1.4,
                            ),
                          ),
                          const SizedBox(height: 4),
                          Text(
                            unread ? 'غير مقروء · اضغط للفتح' : 'مقروء · اضغط للتفاصيل',
                            style: const TextStyle(
                              fontFamily: 'Cairo',
                              fontSize: 11,
                              color: BmTheme.muted,
                            ),
                          ),
                        ],
                      ),
                    ),
                    const Icon(Icons.chevron_left, color: BmTheme.muted),
                  ],
                ),
              ),
            ),
          );
        },
      ),
    );
  }

  Widget _buildRatingsBody() {
    final df = DateFormat('yyyy/MM/dd');
    final direction = _s(_trend, ['direction', 'Direction']);
    final down = direction == 'down';
    final points = _asMaps(_trend?['points'] ?? _trend?['Points']);
    final values = points.map((p) => _d(p, ['value', 'Value'])).toList();
    final metric = _s(_trend, ['metricLabel', 'MetricLabel']);
    final name = _s(_trend, ['name', 'Name']);
    final endValue = _d(_trend, ['endValue', 'EndValue']);
    final customerCount = _d(_trend, ['customerCount', 'CustomerCount']).toInt();
    final periodCustomerCount =
        _d(_trend, ['periodCustomerCount', 'PeriodCustomerCount']).toInt();
    final periodEnd = _d(_trend, ['periodEndValue', 'PeriodEndValue']);
    final periodLabel = _s(_trend, ['periodRatingLabel', 'PeriodRatingLabel']);
    final hasPeriodBlock = !_isEmployeeScope &&
        (_trend?['periodCustomerCount'] != null ||
            _trend?['PeriodCustomerCount'] != null);
    final ratingLabel = _s(_trend, ['ratingLabel', 'RatingLabel']);
    final phantomEnd = _trend?['phantomEndValue'] ?? _trend?['PhantomEndValue'];
    final phantomEndValue =
        phantomEnd == null ? null : _d(_trend, ['phantomEndValue', 'PhantomEndValue']);
    final phantomLabel = _s(_trend, ['phantomRatingLabel', 'PhantomRatingLabel']);
    final phantomMetric = _s(_trend, ['phantomMetricLabel', 'PhantomMetricLabel']);
    final phantomCustomerCount =
        _d(_trend, ['phantomCustomerCount', 'PhantomCustomerCount']).toInt();
    final hasPhantom = phantomEnd != null;
    // العرض الأساسي = وهمي؛ الحقيقي يظهر فقط عند نطاق «الفرع» (مدير الفرع)
    final showRealForManager = _scope == 'branch' && _drilledListId <= 0;
    final displayEnd = hasPhantom ? (phantomEndValue ?? 0) : endValue;
    final displayLabel = hasPhantom
        ? (phantomLabel.isNotEmpty ? phantomLabel : ratingLabel)
        : ratingLabel;
    final displayMetric = hasPhantom
        ? (phantomMetric.isEmpty ? 'التقييم الوهمي' : phantomMetric)
        : (metric.isEmpty ? 'التقييم الحقيقي' : metric);
    final displayCustomerCount = hasPhantom ? phantomCustomerCount : customerCount;
    final excellent = hasPhantom
        ? _d(_trend, ['phantomExcellentCount', 'PhantomExcellentCount']).toInt()
        : _d(_trend, ['excellentCount', 'ExcellentCount']).toInt();
    final good = hasPhantom
        ? _d(_trend, ['phantomGoodCount', 'PhantomGoodCount']).toInt()
        : _d(_trend, ['goodCount', 'GoodCount']).toInt();
    final weak = hasPhantom
        ? _d(_trend, ['phantomWeakCount', 'PhantomWeakCount']).toInt()
        : _d(_trend, ['weakCount', 'WeakCount']).toInt();
    final rejected = hasPhantom
        ? _d(_trend, ['phantomRejectedCount', 'PhantomRejectedCount']).toInt()
        : _d(_trend, ['rejectedCount', 'RejectedCount']).toInt();
    final bad = hasPhantom
        ? _d(_trend, ['phantomBadCount', 'PhantomBadCount']).toInt()
        : _d(_trend, ['badCount', 'BadCount']).toInt();
    final hasBuckets =
        excellent + good + weak + rejected + bad > 0 || displayCustomerCount > 0;
    final pillColor = down ? _warn : _accentDeep;
    final pillBg = down
        ? _warn.withValues(alpha: 0.15)
        : _accent.withValues(alpha: 0.12);
    final countLabel = _isEmployeeScope
        ? 'كل الزبائن ضمن الفترة: $displayCustomerCount'
        : 'الزبائن المسددين: $displayCustomerCount';
    final phantomPeriodEnd = _d(_trend, ['phantomPeriodEndValue', 'PhantomPeriodEndValue']);
    final phantomPeriodLabel =
        _s(_trend, ['phantomPeriodRatingLabel', 'PhantomPeriodRatingLabel']);
    final usePhantomPeriod = hasPhantom &&
        (_trend?['phantomPeriodEndValue'] != null ||
            _trend?['PhantomPeriodEndValue'] != null);
    String drilledName = '';
    if (_drilledListId > 0) {
      for (final l in _lists) {
        if ((int.tryParse(_s(l, ['id', 'Id'])) ?? -1) == _drilledListId) {
          drilledName = _s(l, ['name', 'Name']);
          break;
        }
      }
    }
    final prevMonth = _monthBlock('previous');
    final currMonth = _monthBlock('current');

    return ListView(
      padding: const EdgeInsets.fromLTRB(16, 12, 16, 16),
      children: [
        Row(
          children: [
            Expanded(
              child: OutlinedButton(
                onPressed: _pickFrom,
                child: Text('بيع من ${df.format(_from)}',
                    style: const TextStyle(fontFamily: 'Cairo', fontSize: 12)),
              ),
            ),
            const SizedBox(width: 8),
            Expanded(
              child: OutlinedButton(
                onPressed: _pickTo,
                child: Text('إلى ${df.format(_to)}',
                    style: const TextStyle(fontFamily: 'Cairo', fontSize: 12)),
              ),
            ),
            IconButton(
              onPressed: (_loadingTrend || _loadingCatalog)
                  ? null
                  : () async {
                      await _loadTrend();
                      await _loadMonthStats();
                      if (_showFollowerCards) await _loadFollowerListCards();
                    },
              icon: const Icon(Icons.refresh),
            ),
          ],
        ),
        const SizedBox(height: 6),
        const Text(
          'لوحة التقييم — الفترة الافتراضية آخر 30 يوماً (تاريخ البيع). العرض الأساسي: التقييم الوهمي. الحقيقي يظهر لمدير الفرع فقط.',
          style: TextStyle(fontFamily: 'Cairo', fontSize: 12, color: BmTheme.muted),
        ),
        const SizedBox(height: 10),
        Wrap(
          spacing: 8,
          runSpacing: 8,
          children: [
            _ScopeChip(
              label: 'الفرع',
              selected: _scope == 'branch',
              onTap: () => _setScope('branch'),
            ),
            _ScopeChip(
              label: 'قائمة',
              selected: _scope == 'list',
              onTap: () => _setScope('list'),
            ),
            _ScopeChip(
              label: 'متابع',
              selected: _scope == 'follower',
              onTap: () => _setScope('follower'),
            ),
            _ScopeChip(
              label: 'موظف مبيعات',
              selected: _scope == 'employee',
              onTap: () => _setScope('employee'),
            ),
          ],
        ),
        if (_scope != 'branch') ...[
          const SizedBox(height: 10),
          DropdownButtonFormField<int>(
            value: _entityOptions.any(
                    (e) => (int.tryParse(_s(e, ['id', 'Id'])) ?? -1) == _entityId)
                ? _entityId
                : null,
            decoration: InputDecoration(
              labelText: _scope == 'employee' ? 'اختر الموظف' : 'اختر للرسم',
            ),
            items: _entityOptions
                .map(
                  (e) => DropdownMenuItem(
                    value: int.tryParse(_s(e, ['id', 'Id'])) ?? 0,
                    child: Text(
                      _s(e, ['name', 'Name']),
                      style: const TextStyle(fontFamily: 'Cairo'),
                    ),
                  ),
                )
                .toList(),
            onChanged: (v) {
              if (v == null) return;
              setState(() {
                _entityId = v;
                _drilledListId = 0;
                _trend = null;
              });
              if (_scope == 'follower') {
                _loadFollowerListCards();
                _loadMonthStats();
              } else {
                _loadTrend();
                _loadMonthStats();
              }
            },
          ),
        ],
        if (_drilledListId > 0) ...[
          const SizedBox(height: 10),
          Align(
            alignment: Alignment.centerRight,
            child: TextButton.icon(
              onPressed: _clearFollowerDrill,
              icon: const Icon(Icons.arrow_forward),
              label: Text(
                drilledName.isEmpty
                    ? 'رجوع لقوائم المتابع'
                    : 'رجوع · $drilledName',
                style: const TextStyle(fontFamily: 'Cairo'),
              ),
            ),
          ),
        ],
        const SizedBox(height: 14),
        if (_error != null)
          Text(_error!,
              style: const TextStyle(fontFamily: 'Cairo', color: BmTheme.danger))
        else if (_showFollowerCards) ...[
          if (_loadingMonth) ...[
            const Padding(
              padding: EdgeInsets.only(bottom: 12),
              child: LinearProgressIndicator(minHeight: 2),
            ),
          ] else if (prevMonth != null || currMonth != null) ...[
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                if (prevMonth != null)
                  Expanded(
                    child: _MonthStatCard(
                      title: 'الشهر السابق',
                      yearMonth: _formatYm(
                          _d(prevMonth, ['yearMonth', 'YearMonth']).toInt()),
                      newCount:
                          _d(prevMonth, ['newCustomersCount', 'NewCustomersCount'])
                              .toInt(),
                      newSale: _d(prevMonth,
                          ['newCustomersSaleSum', 'NewCustomersSaleSum']),
                      newDay: _d(prevMonth, [
                        'newCustomersDaySalesSum',
                        'NewCustomersDaySalesSum'
                      ]),
                      zeroCount: _d(prevMonth,
                              ['zeroedCustomersCount', 'ZeroedCustomersCount'])
                          .toInt(),
                      zeroSale: _d(prevMonth,
                          ['zeroedCustomersSaleSum', 'ZeroedCustomersSaleSum']),
                      zeroDay: _d(prevMonth, [
                        'zeroedCustomersDaySalesSum',
                        'ZeroedCustomersDaySalesSum'
                      ]),
                      netCount: _d(prevMonth, ['netCustomersCount', 'NetCustomersCount'])
                          .toInt(),
                      netSale: _d(prevMonth, ['netSaleSum', 'NetSaleSum']),
                      netDay: _d(prevMonth, ['netDaySalesSum', 'NetDaySalesSum']),
                      money: _money,
                      signedInt: _signedInt,
                      signedMoney: _signedMoney,
                    ),
                  ),
                if (prevMonth != null && currMonth != null)
                  const SizedBox(width: 8),
                if (currMonth != null)
                  Expanded(
                    child: _MonthStatCard(
                      title: 'الشهر الحالي',
                      yearMonth: _formatYm(
                          _d(currMonth, ['yearMonth', 'YearMonth']).toInt()),
                      newCount:
                          _d(currMonth, ['newCustomersCount', 'NewCustomersCount'])
                              .toInt(),
                      newSale: _d(currMonth,
                          ['newCustomersSaleSum', 'NewCustomersSaleSum']),
                      newDay: _d(currMonth, [
                        'newCustomersDaySalesSum',
                        'NewCustomersDaySalesSum'
                      ]),
                      zeroCount: _d(currMonth,
                              ['zeroedCustomersCount', 'ZeroedCustomersCount'])
                          .toInt(),
                      zeroSale: _d(currMonth,
                          ['zeroedCustomersSaleSum', 'ZeroedCustomersSaleSum']),
                      zeroDay: _d(currMonth, [
                        'zeroedCustomersDaySalesSum',
                        'ZeroedCustomersDaySalesSum'
                      ]),
                      netCount: _d(currMonth, ['netCustomersCount', 'NetCustomersCount'])
                          .toInt(),
                      netSale: _d(currMonth, ['netSaleSum', 'NetSaleSum']),
                      netDay: _d(currMonth, ['netDaySalesSum', 'NetDaySalesSum']),
                      money: _money,
                      signedInt: _signedInt,
                      signedMoney: _signedMoney,
                    ),
                  ),
              ],
            ),
            const SizedBox(height: 12),
          ],
          if (_loadingFollowerCards)
            const Padding(
              padding: EdgeInsets.symmetric(vertical: 32),
              child: Center(child: CircularProgressIndicator()),
            )
          else if (_followerListCards.isEmpty)
            const Padding(
              padding: EdgeInsets.symmetric(vertical: 24),
              child: Text(
                'لا قوائم مرتبطة بهذا المتابع',
                textAlign: TextAlign.center,
                style: TextStyle(fontFamily: 'Cairo', color: BmTheme.muted),
              ),
            )
          else
            ..._followerListCards.map(
              (card) => Padding(
                padding: const EdgeInsets.only(bottom: 10),
                child: Material(
                  color: _sand,
                  borderRadius: BorderRadius.circular(16),
                  child: InkWell(
                    borderRadius: BorderRadius.circular(16),
                    onTap: () => _openFollowerListCard(card.id),
                    child: Padding(
                      padding: const EdgeInsets.all(14),
                      child: Row(
                        children: [
                          Expanded(
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Text(
                                  card.name.isEmpty ? 'قائمة' : card.name,
                                  style: const TextStyle(
                                    fontFamily: 'Cairo',
                                    fontWeight: FontWeight.bold,
                                    fontSize: 15,
                                  ),
                                ),
                                const SizedBox(height: 4),
                                Text(
                                  card.hasScore
                                      ? '${_score.format(card.score)} · ${card.ratingLabel}'
                                      : '—',
                                  style: const TextStyle(
                                    fontFamily: 'Cairo',
                                    fontSize: 13,
                                    color: BmTheme.muted,
                                  ),
                                ),
                                if (card.customerCount != null)
                                  Text(
                                    'زبائن: ${card.customerCount}',
                                    style: const TextStyle(
                                      fontFamily: 'Cairo',
                                      fontSize: 12,
                                      color: BmTheme.muted,
                                    ),
                                  ),
                              ],
                            ),
                          ),
                          Icon(
                            card.direction == 'down'
                                ? Icons.trending_down
                                : card.direction == 'up'
                                    ? Icons.trending_up
                                    : Icons.trending_flat,
                            color: card.direction == 'down' ? _warn : _accent,
                          ),
                        ],
                      ),
                    ),
                  ),
                ),
              ),
            ),
        ] else if (_showTrendDetail) ...[
          if (_loadingMonth) ...[
            const Padding(
              padding: EdgeInsets.only(bottom: 12),
              child: LinearProgressIndicator(minHeight: 2),
            ),
          ] else if (prevMonth != null || currMonth != null) ...[
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                if (prevMonth != null)
                  Expanded(
                    child: _MonthStatCard(
                      title: 'الشهر السابق',
                      yearMonth: _formatYm(
                          _d(prevMonth, ['yearMonth', 'YearMonth']).toInt()),
                      newCount:
                          _d(prevMonth, ['newCustomersCount', 'NewCustomersCount'])
                              .toInt(),
                      newSale: _d(prevMonth,
                          ['newCustomersSaleSum', 'NewCustomersSaleSum']),
                      newDay: _d(prevMonth, [
                        'newCustomersDaySalesSum',
                        'NewCustomersDaySalesSum'
                      ]),
                      zeroCount: _d(prevMonth,
                              ['zeroedCustomersCount', 'ZeroedCustomersCount'])
                          .toInt(),
                      zeroSale: _d(prevMonth,
                          ['zeroedCustomersSaleSum', 'ZeroedCustomersSaleSum']),
                      zeroDay: _d(prevMonth, [
                        'zeroedCustomersDaySalesSum',
                        'ZeroedCustomersDaySalesSum'
                      ]),
                      netCount: _d(prevMonth, ['netCustomersCount', 'NetCustomersCount'])
                          .toInt(),
                      netSale: _d(prevMonth, ['netSaleSum', 'NetSaleSum']),
                      netDay: _d(prevMonth, ['netDaySalesSum', 'NetDaySalesSum']),
                      money: _money,
                      signedInt: _signedInt,
                      signedMoney: _signedMoney,
                    ),
                  ),
                if (prevMonth != null && currMonth != null)
                  const SizedBox(width: 8),
                if (currMonth != null)
                  Expanded(
                    child: _MonthStatCard(
                      title: 'الشهر الحالي',
                      yearMonth: _formatYm(
                          _d(currMonth, ['yearMonth', 'YearMonth']).toInt()),
                      newCount:
                          _d(currMonth, ['newCustomersCount', 'NewCustomersCount'])
                              .toInt(),
                      newSale: _d(currMonth,
                          ['newCustomersSaleSum', 'NewCustomersSaleSum']),
                      newDay: _d(currMonth, [
                        'newCustomersDaySalesSum',
                        'NewCustomersDaySalesSum'
                      ]),
                      zeroCount: _d(currMonth,
                              ['zeroedCustomersCount', 'ZeroedCustomersCount'])
                          .toInt(),
                      zeroSale: _d(currMonth,
                          ['zeroedCustomersSaleSum', 'ZeroedCustomersSaleSum']),
                      zeroDay: _d(currMonth, [
                        'zeroedCustomersDaySalesSum',
                        'ZeroedCustomersDaySalesSum'
                      ]),
                      netCount: _d(currMonth, ['netCustomersCount', 'NetCustomersCount'])
                          .toInt(),
                      netSale: _d(currMonth, ['netSaleSum', 'NetSaleSum']),
                      netDay: _d(currMonth, ['netDaySalesSum', 'NetDaySalesSum']),
                      money: _money,
                      signedInt: _signedInt,
                      signedMoney: _signedMoney,
                    ),
                  ),
              ],
            ),
            const SizedBox(height: 12),
          ],
          Container(
            padding: const EdgeInsets.all(16),
            decoration: BoxDecoration(
              color: _sand,
              borderRadius: BorderRadius.circular(18),
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Text(
                  name.isEmpty
                      ? (_isEmployeeScope ? 'موظف مبيعات' : 'تقييم القائمة')
                      : name,
                  style: const TextStyle(
                    fontFamily: 'Cairo',
                    color: BmTheme.ink,
                    fontWeight: FontWeight.bold,
                    fontSize: 16,
                  ),
                ),
                const SizedBox(height: 10),
                Text(
                  displayMetric,
                  style: const TextStyle(
                    fontFamily: 'Cairo',
                    color: BmTheme.muted,
                    fontSize: 12,
                  ),
                ),
                const SizedBox(height: 6),
                Text(
                  _score.format(displayEnd),
                  style: const TextStyle(
                    fontFamily: 'Cairo',
                    color: _accentDeep,
                    fontWeight: FontWeight.w800,
                    fontSize: 36,
                    height: 1.05,
                    letterSpacing: -0.5,
                  ),
                ),
                if (displayLabel.isNotEmpty) ...[
                  const SizedBox(height: 6),
                  Align(
                    alignment: Alignment.centerRight,
                    child: Container(
                      padding: const EdgeInsets.symmetric(
                          horizontal: 10, vertical: 3),
                      decoration: BoxDecoration(
                        color: pillBg,
                        borderRadius: BorderRadius.circular(999),
                      ),
                      child: Text(
                        displayLabel,
                        style: TextStyle(
                          fontFamily: 'Cairo',
                          color: pillColor,
                          fontWeight: FontWeight.w600,
                          fontSize: 13,
                        ),
                      ),
                    ),
                  ),
                ],
                if (showRealForManager) ...[
                  const SizedBox(height: 12),
                  Container(
                    width: double.infinity,
                    padding: const EdgeInsets.all(10),
                    decoration: BoxDecoration(
                      color: Colors.white.withValues(alpha: 0.7),
                      borderRadius: BorderRadius.circular(12),
                      border: Border.all(color: _accent.withValues(alpha: 0.3)),
                    ),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          metric.isEmpty ? 'التقييم الحقيقي' : metric,
                          style: const TextStyle(
                            fontFamily: 'Cairo',
                            fontSize: 12,
                            color: BmTheme.muted,
                          ),
                        ),
                        const SizedBox(height: 4),
                        Text(
                          _score.format(endValue),
                          style: const TextStyle(
                            fontFamily: 'Cairo',
                            fontSize: 22,
                            fontWeight: FontWeight.w800,
                            color: BmTheme.ink,
                          ),
                        ),
                        if (ratingLabel.isNotEmpty) ...[
                          const SizedBox(height: 2),
                          Text(
                            ratingLabel,
                            style: const TextStyle(
                              fontFamily: 'Cairo',
                              fontSize: 12,
                              fontWeight: FontWeight.w600,
                              color: _accentDeep,
                            ),
                          ),
                        ],
                        const SizedBox(height: 2),
                        Text(
                          'زبائن الحقيقي: $customerCount',
                          style: const TextStyle(
                            fontFamily: 'Cairo',
                            fontSize: 11,
                            color: BmTheme.muted,
                          ),
                        ),
                      ],
                    ),
                  ),
                ],
                const SizedBox(height: 8),
                InkWell(
                  onTap: displayCustomerCount <= 0
                      ? null
                      : () => _openCustomersSheet(
                            title: 'الأسماء ($displayCustomerCount)',
                            phantom: hasPhantom,
                          ),
                  borderRadius: BorderRadius.circular(8),
                  child: Padding(
                    padding: const EdgeInsets.symmetric(vertical: 4),
                    child: Row(
                      children: [
                        Expanded(
                          child: Text(
                            '$countLabel'
                            '${values.isEmpty ? '' : ' · ${values.length} نقطة يومية'}',
                            style: TextStyle(
                              fontFamily: 'Cairo',
                              color: displayCustomerCount > 0
                                  ? _accent
                                  : BmTheme.muted,
                              fontSize: 13,
                              fontWeight: displayCustomerCount > 0
                                  ? FontWeight.w600
                                  : FontWeight.normal,
                              decoration: displayCustomerCount > 0
                                  ? TextDecoration.underline
                                  : TextDecoration.none,
                              decorationColor: _accent,
                            ),
                          ),
                        ),
                        if (displayCustomerCount > 0)
                          const Icon(Icons.chevron_left,
                              size: 18, color: _accent),
                      ],
                    ),
                  ),
                ),
                if (hasPeriodBlock) ...[
                  const SizedBox(height: 10),
                  Container(
                    width: double.infinity,
                    padding: const EdgeInsets.all(10),
                    decoration: BoxDecoration(
                      color: Colors.white.withValues(alpha: 0.55),
                      borderRadius: BorderRadius.circular(10),
                      border: Border.all(
                          color: _accent.withValues(alpha: 0.25)),
                    ),
                    child: Text(
                      usePhantomPeriod
                          ? 'تقييم الفترة (وهمي): ${_score.format(phantomPeriodEnd)}'
                              '${phantomPeriodLabel.isEmpty ? '' : ' · $phantomPeriodLabel'}'
                              ' · زبائن الفترة: $periodCustomerCount'
                          : 'تقييم الفترة: ${_score.format(periodEnd)}'
                              '${periodLabel.isEmpty ? '' : ' · $periodLabel'}'
                              ' · زبائن الفترة: $periodCustomerCount',
                      style: const TextStyle(
                        fontFamily: 'Cairo',
                        fontSize: 12,
                        color: BmTheme.ink,
                        height: 1.4,
                      ),
                    ),
                  ),
                ],
                if (hasBuckets) ...[
                  const SizedBox(height: 12),
                  Wrap(
                    spacing: 8,
                    runSpacing: 8,
                    children: [
                      _BucketChip(
                        label: 'ممتاز',
                        count: excellent,
                        onTap: () => _openCustomersSheet(
                          level: _bucketKey('ممتاز'),
                          title: 'ممتاز ($excellent)',
                          phantom: hasPhantom,
                        ),
                      ),
                      _BucketChip(
                        label: 'جيد',
                        count: good,
                        onTap: () => _openCustomersSheet(
                          level: _bucketKey('جيد'),
                          title: 'جيد ($good)',
                          phantom: hasPhantom,
                        ),
                      ),
                      _BucketChip(
                        label: 'ضعيف',
                        count: weak,
                        onTap: () => _openCustomersSheet(
                          level: _bucketKey('ضعيف'),
                          title: 'ضعيف ($weak)',
                          phantom: hasPhantom,
                        ),
                      ),
                      _BucketChip(
                        label: 'متلكئ',
                        count: rejected,
                        onTap: () => _openCustomersSheet(
                          level: _bucketKey('متلكئ'),
                          title: 'متلكئ ($rejected)',
                          phantom: hasPhantom,
                        ),
                      ),
                      _BucketChip(
                        label: 'سيء',
                        count: bad,
                        onTap: () => _openCustomersSheet(
                          level: _bucketKey('سيء'),
                          title: 'سيء ($bad)',
                          phantom: hasPhantom,
                        ),
                      ),
                    ],
                  ),
                ],
                const SizedBox(height: 16),
                Directionality(
                  textDirection: TextDirection.ltr,
                  child: Column(
                    children: [
                      SizedBox(
                        height: 160,
                        child: _loadingTrend
                            ? const Center(
                                child: CircularProgressIndicator(color: _accent))
                            : values.isEmpty
                                ? const Center(
                                    child: Text(
                                      'لا نقاط في هذه الفترة',
                                      style: TextStyle(
                                          fontFamily: 'Cairo',
                                          color: BmTheme.muted),
                                    ),
                                  )
                                : CustomPaint(
                                    painter: _SparkLinePainter(
                                      values: values,
                                      lineColor: _accent,
                                    ),
                                    child: const SizedBox.expand(),
                                  ),
                      ),
                      const SizedBox(height: 8),
                      Row(
                        mainAxisAlignment: MainAxisAlignment.spaceBetween,
                        children: [
                          Text(df.format(_from),
                              style: const TextStyle(
                                  fontFamily: 'Cairo',
                                  fontSize: 11,
                                  color: BmTheme.muted)),
                          Text(df.format(_to),
                              style: const TextStyle(
                                  fontFamily: 'Cairo',
                                  fontSize: 11,
                                  color: BmTheme.muted)),
                        ],
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(height: 12),
          const Text(
            'الفترة = تاريخ البيع فقط. التقييم والمستويات تُحسب حتى اليوم. '
            'اضغط على الأسماء أو حالة التقييم لعرض القائمة، ثم اضغط الاسم لفتح البروفايل.',
            style: TextStyle(
              fontFamily: 'Cairo',
              fontSize: 12,
              color: BmTheme.muted,
              height: 1.5,
            ),
          ),
        ],
      ],
    );
  }
}

class _FollowerListCard {
  _FollowerListCard({
    required this.id,
    required this.name,
    required this.score,
    required this.ratingLabel,
    required this.customerCount,
    required this.direction,
    required this.hasScore,
  });

  final int id;
  final String name;
  final double score;
  final String ratingLabel;
  final int? customerCount;
  final String direction;
  final bool hasScore;
}

class _MonthStatCard extends StatelessWidget {
  const _MonthStatCard({
    required this.title,
    required this.yearMonth,
    required this.newCount,
    required this.newSale,
    required this.newDay,
    required this.zeroCount,
    required this.zeroSale,
    required this.zeroDay,
    required this.netCount,
    required this.netSale,
    required this.netDay,
    required this.money,
    required this.signedInt,
    required this.signedMoney,
  });

  final String title;
  final String yearMonth;
  final int newCount;
  final double newSale;
  final double newDay;
  final int zeroCount;
  final double zeroSale;
  final double zeroDay;
  final int netCount;
  final double netSale;
  final double netDay;
  final NumberFormat money;
  final String Function(int) signedInt;
  final String Function(num) signedMoney;

  @override
  Widget build(BuildContext context) {
    Color netColor(num n) {
      if (n > 0) return const Color(0xFF1F6F5B);
      if (n < 0) return const Color(0xFFB45309);
      return BmTheme.muted;
    }

    Widget line(String label, String value, {Color? color}) {
      return Padding(
        padding: const EdgeInsets.only(bottom: 2),
        child: Row(
          children: [
            Expanded(
              child: Text(
                label,
                style: const TextStyle(
                  fontFamily: 'Cairo',
                  fontSize: 11,
                  color: BmTheme.muted,
                ),
              ),
            ),
            Text(
              value,
              style: TextStyle(
                fontFamily: 'Cairo',
                fontSize: 11,
                fontWeight: FontWeight.w600,
                color: color ?? BmTheme.ink,
              ),
            ),
          ],
        ),
      );
    }

    return Container(
      padding: const EdgeInsets.all(10),
      decoration: BoxDecoration(
        color: const Color(0xFFEEF3EF),
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: BmTheme.primary.withValues(alpha: 0.12)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text(
            title,
            style: const TextStyle(
              fontFamily: 'Cairo',
              fontWeight: FontWeight.bold,
              fontSize: 13,
            ),
          ),
          if (yearMonth.isNotEmpty)
            Text(
              yearMonth,
              style: const TextStyle(
                fontFamily: 'Cairo',
                fontSize: 11,
                color: BmTheme.muted,
              ),
            ),
          const SizedBox(height: 6),
          const Text('جدد',
              style: TextStyle(
                  fontFamily: 'Cairo', fontSize: 11, fontWeight: FontWeight.w700)),
          line('العدد', '$newCount'),
          line('سعر البيع', '${money.format(newSale)} د.ع'),
          line('القسط', '${money.format(newDay)} د.ع'),
          const SizedBox(height: 4),
          const Text('مصفرون',
              style: TextStyle(
                  fontFamily: 'Cairo', fontSize: 11, fontWeight: FontWeight.w700)),
          line('العدد', '$zeroCount'),
          line('سعر البيع', '${money.format(zeroSale)} د.ع'),
          line('القسط', '${money.format(zeroDay)} د.ع'),
          const SizedBox(height: 4),
          const Text('الفرق',
              style: TextStyle(
                  fontFamily: 'Cairo', fontSize: 11, fontWeight: FontWeight.w700)),
          line('العدد', signedInt(netCount), color: netColor(netCount)),
          line('سعر البيع', '${signedMoney(netSale)} د.ع', color: netColor(netSale)),
          line('القسط', '${signedMoney(netDay)} د.ع', color: netColor(netDay)),
        ],
      ),
    );
  }
}

class _BucketChip extends StatelessWidget {
  const _BucketChip({
    required this.label,
    required this.count,
    required this.onTap,
  });

  final String label;
  final int count;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: Colors.white,
      borderRadius: BorderRadius.circular(12),
      child: InkWell(
        onTap: count <= 0 ? null : onTap,
        borderRadius: BorderRadius.circular(12),
        child: Container(
          padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(12),
            border: Border.all(
              color: BmTheme.primary.withValues(alpha: 0.12),
            ),
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                label,
                style: TextStyle(
                  fontFamily: 'Cairo',
                  fontSize: 12,
                  color: count > 0 ? BmTheme.muted : BmTheme.muted.withValues(alpha: 0.5),
                ),
              ),
              Text(
                '$count',
                style: TextStyle(
                  fontFamily: 'Cairo',
                  fontWeight: FontWeight.bold,
                  fontSize: 18,
                  color: count > 0 ? _StatsPageState._accentDeep : BmTheme.muted,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _ScopeChip extends StatelessWidget {
  const _ScopeChip({
    required this.label,
    required this.selected,
    required this.onTap,
  });

  final String label;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return ChoiceChip(
      label: Text(label, style: const TextStyle(fontFamily: 'Cairo')),
      selected: selected,
      onSelected: (_) => onTap(),
      selectedColor: BmTheme.primary.withValues(alpha: 0.18),
      labelStyle: TextStyle(
        fontFamily: 'Cairo',
        color: selected ? BmTheme.primary : BmTheme.ink,
        fontWeight: selected ? FontWeight.bold : FontWeight.normal,
      ),
    );
  }
}

class _SparkLinePainter extends CustomPainter {
  _SparkLinePainter({
    required this.values,
    required this.lineColor,
  });

  final List<double> values;
  final Color lineColor;

  @override
  void paint(Canvas canvas, Size size) {
    if (values.isEmpty) return;
    final minV = values.reduce(math.min);
    final maxV = values.reduce(math.max);
    final span = (maxV - minV).abs() < 0.0001 ? 1.0 : (maxV - minV);
    final pad = span * 0.08;

    Offset pointAt(int i) {
      final x = values.length == 1
          ? size.width / 2
          : size.width * (i / (values.length - 1));
      final norm =
          ((values[i] - (minV - pad)) / (span + pad * 2)).clamp(0.0, 1.0);
      final y = size.height - (norm * size.height);
      return Offset(x, y);
    }

    final path = Path()..moveTo(pointAt(0).dx, pointAt(0).dy);
    for (var i = 1; i < values.length; i++) {
      final p = pointAt(i);
      path.lineTo(p.dx, p.dy);
    }

    canvas.drawPath(
      path,
      Paint()
        ..color = lineColor
        ..style = PaintingStyle.stroke
        ..strokeWidth = 2.5
        ..strokeCap = StrokeCap.round
        ..strokeJoin = StrokeJoin.round,
    );

    final last = pointAt(values.length - 1);
    canvas.drawCircle(last, 4, Paint()..color = lineColor);
  }

  @override
  bool shouldRepaint(covariant _SparkLinePainter oldDelegate) {
    return oldDelegate.values != values || oldDelegate.lineColor != lineColor;
  }
}

class _RatingCustomersSheet extends StatefulWidget {
  const _RatingCustomersSheet({
    required this.title,
    required this.scope,
    required this.entityId,
    required this.from,
    required this.to,
    this.level,
    this.phantom = true,
  });

  final String title;
  final String scope;
  final int entityId;
  final DateTime from;
  final DateTime to;
  final String? level;
  final bool phantom;

  @override
  State<_RatingCustomersSheet> createState() => _RatingCustomersSheetState();
}

class _RatingCustomersSheetState extends State<_RatingCustomersSheet> {
  bool _loading = true;
  String? _error;
  List<Map<String, dynamic>> _rows = [];

  @override
  void initState() {
    super.initState();
    _load();
  }

  String _s(Map? m, List<String> keys) {
    if (m == null) return '';
    for (final k in keys) {
      final v = m[k];
      if (v != null && '$v'.trim().isNotEmpty) return '$v'.trim();
    }
    return '';
  }

  double _d(Map? m, List<String> keys) {
    if (m == null) return 0;
    for (final k in keys) {
      final v = m[k];
      if (v is num) return v.toDouble();
      final p = double.tryParse('$v');
      if (p != null) return p;
    }
    return 0;
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final rows = await BmApi.instance.statsRatingCustomers(
        scope: widget.scope,
        id: widget.entityId,
        from: widget.from,
        to: widget.to,
        level: widget.level,
        phantom: widget.phantom,
      );
      if (!mounted) return;
      // مصفرون / بدون بيع: الباقي أو سعر البيع ≤ 0 لا يظهرون بالتقييم
      final open = rows.where((r) {
        final remain = _d(r, ['amountRemaining', 'AmountRemaining']);
        final total = _d(r, ['amountTotal', 'AmountTotal', 'amountTotalSales', 'AmountTotalSales']);
        return remain > 0 && total > 0;
      }).toList();
      setState(() {
        _rows = open;
        _loading = false;
      });
    } catch (e) {
      if (!mounted) return;
      if (e is BmApiException && e.statusCode == 401) {
        await BmSession.instance.clear();
      }
      setState(() {
        _loading = false;
        _error = '$e';
      });
    }
  }

  Future<void> _openProfile(Map<String, dynamic> row) async {
    final id = int.tryParse(_s(row, ['customerId', 'CustomerId'])) ?? 0;
    if (id <= 0) return;
    final name = _s(row, ['customerName', 'CustomerName']);
    await Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (_) => CustomerProfilePage(
          customerId: id,
          initialName: name.isEmpty ? null : name,
          seed: {
            'customerID': id,
            'customerName': name,
            'phone': _s(row, ['phone', 'Phone']),
            'amountPaid': row['amountPaid'] ?? row['AmountPaid'],
            'amountRemaining': row['amountRemaining'] ?? row['AmountRemaining'],
            'amountTotal': row['amountTotal'] ?? row['AmountTotal'],
          },
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final h = MediaQuery.sizeOf(context).height * 0.72;
    return Directionality(
      textDirection: TextDirection.rtl,
      child: SizedBox(
        height: h,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            const SizedBox(height: 8),
            Center(
              child: Container(
                width: 40,
                height: 4,
                decoration: BoxDecoration(
                  color: Colors.black26,
                  borderRadius: BorderRadius.circular(999),
                ),
              ),
            ),
            Padding(
              padding: const EdgeInsets.fromLTRB(16, 12, 16, 8),
              child: Row(
                children: [
                  Expanded(
                    child: Text(
                      widget.title,
                      style: const TextStyle(
                        fontFamily: 'Cairo',
                        fontWeight: FontWeight.bold,
                        fontSize: 16,
                      ),
                    ),
                  ),
                  IconButton(
                    onPressed: () => Navigator.pop(context),
                    icon: const Icon(Icons.close),
                  ),
                ],
              ),
            ),
            const Divider(height: 1),
            Expanded(
              child: _loading
                  ? const Center(child: CircularProgressIndicator())
                  : _error != null
                      ? Center(
                          child: Padding(
                            padding: const EdgeInsets.all(16),
                            child: Text(
                              _error!,
                              textAlign: TextAlign.center,
                              style: const TextStyle(
                                  fontFamily: 'Cairo', color: BmTheme.danger),
                            ),
                          ),
                        )
                      : _rows.isEmpty
                          ? const Center(
                              child: Text(
                                'لا أسماء في هذه الحالة',
                                style: TextStyle(
                                    fontFamily: 'Cairo', color: BmTheme.muted),
                              ),
                            )
                          : ListView.separated(
                              padding: const EdgeInsets.symmetric(vertical: 8),
                              itemCount: _rows.length,
                              separatorBuilder: (_, __) =>
                                  const Divider(height: 1),
                              itemBuilder: (context, i) {
                                final row = _rows[i];
                                final name = _s(
                                    row, ['customerName', 'CustomerName']);
                                final phone = _s(row, ['phone', 'Phone']);
                                final label = _s(
                                    row, ['ratingLabel', 'RatingLabel']);
                                return ListTile(
                                  title: Text(
                                    name.isEmpty ? 'زبون' : name,
                                    style: const TextStyle(
                                      fontFamily: 'Cairo',
                                      fontWeight: FontWeight.w600,
                                    ),
                                  ),
                                  subtitle: Text(
                                    [
                                      if (phone.isNotEmpty) phone,
                                      if (label.isNotEmpty) label,
                                    ].join(' · '),
                                    style: const TextStyle(
                                      fontFamily: 'Cairo',
                                      fontSize: 12,
                                      color: BmTheme.muted,
                                    ),
                                  ),
                                  trailing: const Icon(Icons.chevron_left),
                                  onTap: () => _openProfile(row),
                                );
                              },
                            ),
            ),
          ],
        ),
      ),
    );
  }
}
