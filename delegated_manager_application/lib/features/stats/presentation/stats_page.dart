import 'dart:convert';
import 'dart:math' as math;

import 'package:delegated_manager_application/core/config/app_env.dart';
import 'package:delegated_manager_application/core/config/province_catalog.dart';
import 'package:delegated_manager_application/core/network/api_client.dart';
import 'package:delegated_manager_application/core/theme/app_theme.dart';
import 'package:delegated_manager_application/core/utils/date_format.dart';
import 'package:delegated_manager_application/features/stats/presentation/customer_profile_screen.dart';
import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;

/// لوحة التقييمات — نفس منطق المدير / مدير الفرع على الجوال (مع اختيار محافظة).
class StatsPage extends StatefulWidget {
  const StatsPage({super.key});

  @override
  State<StatsPage> createState() => StatsPageState();
}

class StatsPageState extends State<StatsPage> {
  static const _accentDeep = Color(0xFF0F3D32);
  static const _accent = Color(0xFF1F6F5B);
  static const _warn = Color(0xFFB45309);
  static const _sand = Color(0xFFEEF3EF);

  late DateTime _from;
  late DateTime _to;

  String _scope = 'branch';
  int _entityId = 0;
  int _drilledListId = 0;
  String? _cityValue;

  bool _loadingCatalog = false;
  bool _loadingTrend = false;
  bool _loadingFollowerCards = false;
  String? _error;
  List<ProvinceOption> _provinces = [];

  List<Map<String, dynamic>> _lists = [];
  List<Map<String, dynamic>> _followers = [];
  List<Map<String, dynamic>> _employees = [];
  List<_FollowerListCard> _followerListCards = [];
  Map<String, dynamic>? _trend;

  @override
  void initState() {
    super.initState();
    final now = DateTime.now();
    _from = DateTime(now.year, now.month, 1);
    _to = DateTime(now.year, now.month, now.day);
    _provinces = ProvinceCatalog.forEnv(AppEnv.name);
    if (_provinces.isNotEmpty) {
      _cityValue = _provinces.first.value;
    }
    _bootstrap();
  }

  Future<void> reload() => _bootstrap();

  Future<void> _bootstrap() async {
    await _ensureProvinces();
    if (_cityValue == null || _cityValue!.isEmpty) return;
    await _loadCatalog();
    await _loadTrend();
  }

  Future<void> _ensureProvinces() async {
    if (!AppEnv.isProduction) {
      final local = ProvinceCatalog.forEnv(AppEnv.name);
      if (!mounted) return;
      setState(() {
        _provinces = local;
        _cityValue ??= local.isNotEmpty ? local.first.value : null;
      });
      return;
    }
    if (_provinces.isNotEmpty) return;
    try {
      final response = await http
          .get(
            Uri.parse(ProvinceCatalog.getAdminUrl),
            headers: const {'Accept': 'application/json'},
          )
          .timeout(ApiClient.timeout);
      if (response.statusCode < 200 || response.statusCode >= 300) {
        throw ApiException('تعذر تحميل المحافظات (${response.statusCode})');
      }
      final decoded = jsonDecode(response.body);
      final list = decoded is List ? decoded : const [];
      final mapped = ProvinceCatalog.fromGetAdmin(list);
      if (!mounted) return;
      setState(() {
        _provinces = mapped;
        _cityValue ??= mapped.isNotEmpty ? mapped.first.value : null;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() => _error = 'تعذر تحميل قائمة المحافظات');
    }
  }

  Future<void> _loadCatalog() async {
    final city = _cityValue;
    if (city == null || city.isEmpty) return;
    setState(() {
      _loadingCatalog = true;
      _error = null;
    });
    try {
      final cat = await ApiClient.get('delegated-manager/stats/catalog', {
        'cityValue': city,
      });
      if (!mounted) return;
      final map =
          cat is Map ? Map<String, dynamic>.from(cat) : <String, dynamic>{};
      setState(() {
        _lists = _asMaps(map['lists'] ?? map['Lists']);
        _followers = _asMaps(map['followers'] ?? map['Followers']);
        _employees = _asMaps(map['employees'] ?? map['Employees']);
        _loadingCatalog = false;
      });
      if (_scope == 'follower' && _entityId > 0 && _drilledListId <= 0) {
        await _loadFollowerListCards();
      }
    } on ApiException catch (e) {
      if (!mounted) return;
      setState(() {
        _loadingCatalog = false;
        _error = e.message;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _loadingCatalog = false;
        _error = '$e';
      });
    }
  }

  ({String scope, int id}) _trendScopeId() {
    if (_drilledListId > 0) return (scope: 'list', id: _drilledListId);
    if (_scope == 'branch') return (scope: 'branch', id: 0);
    return (scope: _scope, id: _entityId);
  }

  Future<void> _loadTrend() async {
    final city = _cityValue;
    if (city == null || city.isEmpty) return;
    final sid = _trendScopeId();
    if (sid.scope != 'branch' && sid.id <= 0) {
      setState(() {
        _trend = null;
        _loadingTrend = false;
      });
      return;
    }
    setState(() {
      _loadingTrend = true;
      _error = null;
    });
    try {
      final trend = await ApiClient.get('delegated-manager/stats/trend', {
        'cityValue': city,
        'scope': sid.scope,
        'id': '${sid.id}',
        'fromDate': AppDate.isoDate(_from),
        'toDate': AppDate.isoDate(_to),
      });
      if (!mounted) return;
      setState(() {
        _trend = trend is Map ? Map<String, dynamic>.from(trend) : null;
        _loadingTrend = false;
      });
    } on ApiException catch (e) {
      if (!mounted) return;
      setState(() {
        _loadingTrend = false;
        _error = e.message;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _loadingTrend = false;
        _error = '$e';
      });
    }
  }

  List<int> _assignedListIds(Map<String, dynamic> follower) {
    final raw =
        follower['assignedListIds'] ?? follower['AssignedListIds'] ?? const [];
    if (raw is! List) return const [];
    return raw
        .map((e) => int.tryParse('$e') ?? 0)
        .where((id) => id > 0)
        .toList();
  }

  Future<void> _loadFollowerListCards() async {
    final city = _cityValue;
    if (city == null ||
        city.isEmpty ||
        _scope != 'follower' ||
        _entityId <= 0) {
      if (!mounted) return;
      setState(() => _followerListCards = []);
      return;
    }
    Map<String, dynamic>? follower;
    for (final f in _followers) {
      if ((int.tryParse(_s(f, ['id', 'Id'])) ?? -1) == _entityId) {
        follower = f;
        break;
      }
    }
    final ids = follower == null ? <int>[] : _assignedListIds(follower);
    final lists = _lists
        .where((l) => ids.contains(int.tryParse(_s(l, ['id', 'Id'])) ?? -1))
        .toList();
    if (lists.isEmpty) {
      if (!mounted) return;
      setState(() {
        _followerListCards = [];
        _loadingFollowerCards = false;
      });
      return;
    }

    final followerId = _entityId;
    setState(() => _loadingFollowerCards = true);
    try {
      final settled = await Future.wait(lists.map((l) async {
        final id = int.tryParse(_s(l, ['id', 'Id'])) ?? 0;
        final name = _s(l, ['name', 'Name']);
        try {
          final data = await ApiClient.get('delegated-manager/stats/trend', {
            'cityValue': city,
            'scope': 'list',
            'id': '$id',
            'fromDate': AppDate.isoDate(_from),
            'toDate': AppDate.isoDate(_to),
          });
          final map =
              data is Map ? Map<String, dynamic>.from(data) : <String, dynamic>{};
          return _FollowerListCard(
            id: id,
            name: name,
            score: _d(map, ['endValue', 'EndValue']),
            ratingLabel: _s(map, ['ratingLabel', 'RatingLabel']),
            customerCount:
                _d(map, ['customerCount', 'CustomerCount']).toInt(),
            direction: _s(map, ['direction', 'Direction']).toLowerCase(),
            hasScore: map['endValue'] != null || map['EndValue'] != null,
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
      if (_cityValue != city ||
          _scope != 'follower' ||
          _entityId != followerId) {
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

  void _openFollowerListCard(int listId) {
    if (listId <= 0) return;
    setState(() {
      _drilledListId = listId;
      _trend = null;
    });
    _loadTrend();
  }

  void _clearFollowerDrill() {
    setState(() {
      _drilledListId = 0;
      _trend = null;
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
    if (_scope == 'follower' && _entityId > 0 && _drilledListId <= 0) {
      await _loadFollowerListCards();
    }
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
    if (_scope == 'follower' && _entityId > 0 && _drilledListId <= 0) {
      await _loadFollowerListCards();
    }
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
      _loadFollowerListCards();
      setState(() => _trend = null);
    } else {
      _loadTrend();
    }
  }

  Future<void> _setCity(String? value) async {
    if (value == null || value == _cityValue) return;
    setState(() {
      _cityValue = value;
      _entityId = 0;
      _drilledListId = 0;
      _scope = 'branch';
      _lists = [];
      _followers = [];
      _employees = [];
      _followerListCards = [];
      _trend = null;
    });
    await _loadCatalog();
    await _loadTrend();
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

  bool get _isEmployeeScope => _scope == 'employee';

  bool get _showFollowerCards =>
      _scope == 'follower' && _entityId > 0 && _drilledListId <= 0;

  bool get _showTrendDetail =>
      !_showFollowerCards &&
      (_scope == 'branch' ||
          _entityId > 0 ||
          _drilledListId > 0);

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

  Future<void> _openCustomersSheet({String? level, required String title}) async {
    final sid = _trendScopeId();
    if (sid.scope != 'branch' && sid.id <= 0) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('اختر عنصراً أولاً')),
      );
      return;
    }
    final city = _cityValue;
    if (city == null || city.isEmpty) return;

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
          cityValue: city,
          scope: sid.scope,
          entityId: sid.id,
          from: _from,
          to: _to,
          level: level,
        );
      },
    );
  }

  @override
  Widget build(BuildContext context) {
    final direction = _s(_trend, ['direction', 'Direction']);
    final down = direction == 'down';
    final points = _asMaps(_trend?['points'] ?? _trend?['Points']);
    final values = points.map((p) => _d(p, ['value', 'Value'])).toList();
    final metric = _s(_trend, ['metricLabel', 'MetricLabel']);
    final name = _s(_trend, ['name', 'Name']);
    final endValue = _d(_trend, ['endValue', 'EndValue']);
    final customerCount =
        _d(_trend, ['customerCount', 'CustomerCount']).toInt();
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
    final phantomMetric =
        _s(_trend, ['phantomMetricLabel', 'PhantomMetricLabel']);
    final phantomCustomerCount =
        _d(_trend, ['phantomCustomerCount', 'PhantomCustomerCount']).toInt();
    final hasPhantom = phantomEnd != null;
    final excellent = _d(_trend, ['excellentCount', 'ExcellentCount']).toInt();
    final good = _d(_trend, ['goodCount', 'GoodCount']).toInt();
    final weak = _d(_trend, ['weakCount', 'WeakCount']).toInt();
    final rejected = _d(_trend, ['rejectedCount', 'RejectedCount']).toInt();
    final bad = _d(_trend, ['badCount', 'BadCount']).toInt();
    final hasBuckets =
        excellent + good + weak + rejected + bad > 0 || customerCount > 0;
    final pillColor = down ? _warn : _accentDeep;
    final pillBg = down
        ? _warn.withValues(alpha: 0.15)
        : _accent.withValues(alpha: 0.12);
    final countLabel = _isEmployeeScope
        ? 'كل الزبائن ضمن الفترة: $customerCount'
        : 'الزبائن المسددين: $customerCount';
    String drilledName = '';
    if (_drilledListId > 0) {
      for (final l in _lists) {
        if ((int.tryParse(_s(l, ['id', 'Id'])) ?? -1) == _drilledListId) {
          drilledName = _s(l, ['name', 'Name']);
          break;
        }
      }
    }

    return ListView(
      padding: const EdgeInsets.fromLTRB(16, 12, 16, 16),
      children: [
        DropdownButtonFormField<String>(
          value: _provinces.any((p) => p.value == _cityValue) ? _cityValue : null,
          decoration: const InputDecoration(labelText: 'اختر المحافظة'),
          items: _provinces
              .map(
                (p) => DropdownMenuItem(
                  value: p.value,
                  child: Text(p.name),
                ),
              )
              .toList(),
          onChanged: _setCity,
        ),
        const SizedBox(height: 10),
        Row(
          children: [
            Expanded(
              child: OutlinedButton(
                onPressed: _pickFrom,
                child: Text(
                  'بيع من ${AppDate.day(_from)}',
                  style: const TextStyle(fontSize: 12),
                ),
              ),
            ),
            const SizedBox(width: 8),
            Expanded(
              child: OutlinedButton(
                onPressed: _pickTo,
                child: Text(
                  'إلى ${AppDate.day(_to)}',
                  style: const TextStyle(fontSize: 12),
                ),
              ),
            ),
            IconButton(
              onPressed: (_loadingTrend || _loadingCatalog) ? null : () async {
                await _loadTrend();
                if (_showFollowerCards) await _loadFollowerListCards();
              },
              icon: const Icon(Icons.refresh),
            ),
          ],
        ),
        const SizedBox(height: 6),
        const Text(
          'لوحة التقييم — الفترة = تاريخ البيع فقط، والتقييم/التسديدات تُحسب حتى اليوم.',
          style: TextStyle(fontSize: 12, color: AppColors.muted),
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
            value: _entityOptions.any((e) =>
                    (int.tryParse(_s(e, ['id', 'Id'])) ?? -1) == _entityId)
                ? _entityId
                : null,
            decoration: InputDecoration(
              labelText: _isEmployeeScope ? 'اختر الموظف' : 'اختر للرسم',
            ),
            items: _entityOptions
                .map(
                  (e) => DropdownMenuItem(
                    value: int.tryParse(_s(e, ['id', 'Id'])) ?? 0,
                    child: Text(_s(e, ['name', 'Name'])),
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
              } else {
                _loadTrend();
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
              ),
            ),
          ),
        ],
        const SizedBox(height: 14),
        if (_error != null)
          Text(_error!, style: const TextStyle(color: AppColors.rejected))
        else if (_showFollowerCards) ...[
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
                style: TextStyle(color: AppColors.muted),
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
                                  card.name,
                                  style: const TextStyle(
                                    fontWeight: FontWeight.bold,
                                    fontSize: 15,
                                  ),
                                ),
                                const SizedBox(height: 4),
                                Text(
                                  [
                                    if (card.hasScore)
                                      AppDate.formatScore(card.score),
                                    if (card.ratingLabel.isNotEmpty)
                                      card.ratingLabel,
                                    if (card.customerCount != null)
                                      'زبائن: ${card.customerCount}',
                                  ].join(' · '),
                                  style: const TextStyle(
                                    fontSize: 12,
                                    color: AppColors.muted,
                                  ),
                                ),
                              ],
                            ),
                          ),
                          Icon(
                            card.direction == 'up'
                                ? Icons.trending_up
                                : card.direction == 'down'
                                    ? Icons.trending_down
                                    : Icons.trending_flat,
                            color: card.direction == 'down'
                                ? _warn
                                : _accentDeep,
                          ),
                          const Icon(Icons.chevron_left),
                        ],
                      ),
                    ),
                  ),
                ),
              ),
            ),
        ] else if (_showTrendDetail) ...[
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
                    fontWeight: FontWeight.bold,
                    fontSize: 16,
                  ),
                ),
                const SizedBox(height: 10),
                if (hasPhantom)
                  Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(
                              metric.isEmpty ? 'التقييم الحقيقي' : metric,
                              style: const TextStyle(
                                color: AppColors.muted,
                                fontSize: 12,
                              ),
                            ),
                            const SizedBox(height: 6),
                            Text(
                              AppDate.formatScore(endValue),
                              style: const TextStyle(
                                color: _accentDeep,
                                fontWeight: FontWeight.w800,
                                fontSize: 32,
                                height: 1.05,
                              ),
                            ),
                            if (ratingLabel.isNotEmpty) ...[
                              const SizedBox(height: 6),
                              Container(
                                padding: const EdgeInsets.symmetric(
                                    horizontal: 10, vertical: 3),
                                decoration: BoxDecoration(
                                  color: pillBg,
                                  borderRadius: BorderRadius.circular(999),
                                ),
                                child: Text(
                                  ratingLabel,
                                  style: TextStyle(
                                    color: pillColor,
                                    fontWeight: FontWeight.w600,
                                    fontSize: 13,
                                  ),
                                ),
                              ),
                            ],
                            const SizedBox(height: 6),
                            Text(
                              'الزبائن: $customerCount',
                              style: const TextStyle(
                                color: AppColors.muted,
                                fontSize: 12,
                              ),
                            ),
                          ],
                        ),
                      ),
                      const SizedBox(width: 10),
                      Expanded(
                        child: Container(
                          padding: const EdgeInsets.all(10),
                          decoration: BoxDecoration(
                            color: _accent.withValues(alpha: 0.08),
                            borderRadius: BorderRadius.circular(12),
                            border: Border.all(
                                color: _accent.withValues(alpha: 0.35)),
                          ),
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(
                                phantomMetric.isEmpty
                                    ? 'التقييم الوهمي'
                                    : phantomMetric,
                                style: const TextStyle(
                                  fontSize: 12,
                                  color: AppColors.muted,
                                ),
                              ),
                              const SizedBox(height: 6),
                              Text(
                                AppDate.formatScore(phantomEndValue ?? 0),
                                style: const TextStyle(
                                  fontSize: 28,
                                  fontWeight: FontWeight.w800,
                                  height: 1.05,
                                ),
                              ),
                              if (phantomLabel.isNotEmpty) ...[
                                const SizedBox(height: 4),
                                Text(
                                  phantomLabel,
                                  style: const TextStyle(
                                    fontSize: 13,
                                    fontWeight: FontWeight.w600,
                                    color: _accentDeep,
                                  ),
                                ),
                              ],
                              const SizedBox(height: 6),
                              Text(
                                'عدد الزبائن: $phantomCustomerCount',
                                style: const TextStyle(
                                  fontSize: 12,
                                  color: AppColors.muted,
                                ),
                              ),
                            ],
                          ),
                        ),
                      ),
                    ],
                  )
                else ...[
                  Text(
                    metric.isEmpty ? 'التقييم الحقيقي' : metric,
                    style: const TextStyle(
                      color: AppColors.muted,
                      fontSize: 12,
                    ),
                  ),
                  const SizedBox(height: 10),
                  Text(
                    AppDate.formatScore(endValue),
                    style: const TextStyle(
                      color: _accentDeep,
                      fontWeight: FontWeight.w800,
                      fontSize: 40,
                      height: 1.05,
                    ),
                  ),
                  if (ratingLabel.isNotEmpty) ...[
                    const SizedBox(height: 8),
                    Align(
                      alignment: Alignment.centerRight,
                      child: Container(
                        padding: const EdgeInsets.symmetric(
                            horizontal: 12, vertical: 4),
                        decoration: BoxDecoration(
                          color: pillBg,
                          borderRadius: BorderRadius.circular(999),
                        ),
                        child: Text(
                          ratingLabel,
                          style: TextStyle(
                            color: pillColor,
                            fontWeight: FontWeight.w600,
                            fontSize: 14,
                          ),
                        ),
                      ),
                    ),
                  ],
                ],
                const SizedBox(height: 8),
                InkWell(
                  onTap: customerCount <= 0
                      ? null
                      : () => _openCustomersSheet(
                            title: 'الأسماء ($customerCount)',
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
                              color: customerCount > 0
                                  ? _accent
                                  : AppColors.muted,
                              fontSize: 13,
                              fontWeight: customerCount > 0
                                  ? FontWeight.w600
                                  : FontWeight.normal,
                              decoration: customerCount > 0
                                  ? TextDecoration.underline
                                  : TextDecoration.none,
                              decorationColor: _accent,
                            ),
                          ),
                        ),
                        if (customerCount > 0)
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
                      border:
                          Border.all(color: _accent.withValues(alpha: 0.25)),
                    ),
                    child: Text(
                      'تقييم الفترة: ${AppDate.formatScore(periodEnd)}'
                      '${periodLabel.isEmpty ? '' : ' · $periodLabel'}'
                      ' · زبائن الفترة: $periodCustomerCount',
                      style: const TextStyle(fontSize: 12, height: 1.4),
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
                        ),
                      ),
                      _BucketChip(
                        label: 'جيد',
                        count: good,
                        onTap: () => _openCustomersSheet(
                          level: _bucketKey('جيد'),
                          title: 'جيد ($good)',
                        ),
                      ),
                      _BucketChip(
                        label: 'ضعيف',
                        count: weak,
                        onTap: () => _openCustomersSheet(
                          level: _bucketKey('ضعيف'),
                          title: 'ضعيف ($weak)',
                        ),
                      ),
                      _BucketChip(
                        label: 'متلكئ',
                        count: rejected,
                        onTap: () => _openCustomersSheet(
                          level: _bucketKey('متلكئ'),
                          title: 'متلكئ ($rejected)',
                        ),
                      ),
                      _BucketChip(
                        label: 'سيء',
                        count: bad,
                        onTap: () => _openCustomersSheet(
                          level: _bucketKey('سيء'),
                          title: 'سيء ($bad)',
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
                        height: 180,
                        child: _loadingTrend
                            ? const Center(child: CircularProgressIndicator())
                            : values.isEmpty
                                ? const Center(
                                    child: Text(
                                      'لا نقاط في هذه الفترة',
                                      style: TextStyle(color: AppColors.muted),
                                    ),
                                  )
                                : CustomPaint(
                                    painter: _ForexLinePainter(
                                      values: values,
                                      lineColor: _accent,
                                      fillColor:
                                          _accent.withValues(alpha: 0.18),
                                    ),
                                    child: const SizedBox.expand(),
                                  ),
                      ),
                      const SizedBox(height: 8),
                      Row(
                        mainAxisAlignment: MainAxisAlignment.spaceBetween,
                        children: [
                          Text(AppDate.day(_from),
                              style: const TextStyle(
                                  fontSize: 11, color: AppColors.muted)),
                          Text(AppDate.day(_to),
                              style: const TextStyle(
                                  fontSize: 11, color: AppColors.muted)),
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
            'اضغط على الأسماء أو حالة التقييم لعرض القائمة، ثم اضغط الاسم لفتح البروفايل.',
            style: TextStyle(
              fontSize: 12,
              color: AppColors.muted,
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
              color: AppColors.primary.withValues(alpha: 0.12),
            ),
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                label,
                style: TextStyle(
                  fontSize: 12,
                  color: count > 0
                      ? AppColors.muted
                      : AppColors.muted.withValues(alpha: 0.5),
                ),
              ),
              Text(
                '$count',
                style: TextStyle(
                  fontWeight: FontWeight.bold,
                  fontSize: 18,
                  color: count > 0
                      ? StatsPageState._accentDeep
                      : AppColors.muted,
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
      label: Text(label),
      selected: selected,
      onSelected: (_) => onTap(),
      selectedColor: AppColors.primary.withValues(alpha: 0.18),
      labelStyle: TextStyle(
        color: selected ? AppColors.primary : AppColors.text,
        fontWeight: selected ? FontWeight.bold : FontWeight.normal,
      ),
    );
  }
}

class _ForexLinePainter extends CustomPainter {
  _ForexLinePainter({
    required this.values,
    required this.lineColor,
    required this.fillColor,
  });

  final List<double> values;
  final Color lineColor;
  final Color fillColor;

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

    final fill = Path.from(path)
      ..lineTo(size.width, size.height)
      ..lineTo(0, size.height)
      ..close();
    canvas.drawPath(
      fill,
      Paint()
        ..shader = LinearGradient(
          begin: Alignment.topCenter,
          end: Alignment.bottomCenter,
          colors: [fillColor, fillColor.withValues(alpha: 0.02)],
        ).createShader(Offset.zero & size),
    );

    canvas.drawPath(
      path,
      Paint()
        ..color = lineColor
        ..style = PaintingStyle.stroke
        ..strokeWidth = 2.6
        ..strokeCap = StrokeCap.round
        ..strokeJoin = StrokeJoin.round,
    );

    final last = pointAt(values.length - 1);
    canvas.drawCircle(last, 4.5, Paint()..color = lineColor);
    canvas.drawCircle(last, 2.2, Paint()..color = Colors.white);
  }

  @override
  bool shouldRepaint(covariant _ForexLinePainter oldDelegate) {
    return oldDelegate.values != values || oldDelegate.lineColor != lineColor;
  }
}

class _RatingCustomersSheet extends StatefulWidget {
  const _RatingCustomersSheet({
    required this.title,
    required this.cityValue,
    required this.scope,
    required this.entityId,
    required this.from,
    required this.to,
    this.level,
  });

  final String title;
  final String cityValue;
  final String scope;
  final int entityId;
  final DateTime from;
  final DateTime to;
  final String? level;

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
      final qp = <String, String>{
        'cityValue': widget.cityValue,
        'scope': widget.scope,
        'id': '${widget.entityId}',
        'fromDate': AppDate.isoDate(widget.from),
        'toDate': AppDate.isoDate(widget.to),
      };
      if (widget.level != null && widget.level!.trim().isNotEmpty) {
        qp['level'] = widget.level!.trim();
      }
      final data =
          await ApiClient.get('delegated-manager/stats/rating-customers', qp);
      if (!mounted) return;
      List<Map<String, dynamic>> rows = [];
      if (data is List) {
        rows = data
            .whereType<Map>()
            .map((e) => Map<String, dynamic>.from(e))
            .toList();
      } else if (data is Map) {
        final list = data['data'] ?? data['Data'];
        if (list is List) {
          rows = list
              .whereType<Map>()
              .map((e) => Map<String, dynamic>.from(e))
              .toList();
        }
      }
      final open = rows.where((r) {
        final remain = _d(r, ['amountRemaining', 'AmountRemaining']);
        final total = _d(r, [
          'amountTotal',
          'AmountTotal',
          'amountTotalSales',
          'AmountTotalSales',
        ]);
        return remain > 0 && total > 0;
      }).toList();
      setState(() {
        _rows = open;
        _loading = false;
      });
    } on ApiException catch (e) {
      if (!mounted) return;
      setState(() {
        _loading = false;
        _error = e.message;
      });
    } catch (e) {
      if (!mounted) return;
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
        builder: (_) => CustomerProfileScreen(
          cityValue: widget.cityValue,
          customerId: id,
          initialName: name.isEmpty ? null : name,
          seed: {
            'customerID': id,
            'customerName': name,
            'phone': _s(row, ['phone', 'Phone']),
            'amountPaid': row['amountPaid'] ?? row['AmountPaid'],
            'amountRemaining':
                row['amountRemaining'] ?? row['AmountRemaining'],
            'amountTotal': row['amountTotal'] ?? row['AmountTotal'],
          },
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final h = MediaQuery.sizeOf(context).height * 0.72;
    return SizedBox(
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
                            style:
                                const TextStyle(color: AppColors.rejected),
                          ),
                        ),
                      )
                    : _rows.isEmpty
                        ? const Center(
                            child: Text(
                              'لا أسماء في هذه الحالة',
                              style: TextStyle(color: AppColors.muted),
                            ),
                          )
                        : ListView.separated(
                            padding: const EdgeInsets.symmetric(vertical: 8),
                            itemCount: _rows.length,
                            separatorBuilder: (_, __) =>
                                const Divider(height: 1),
                            itemBuilder: (context, i) {
                              final row = _rows[i];
                              final name =
                                  _s(row, ['customerName', 'CustomerName']);
                              final phone = _s(row, ['phone', 'Phone']);
                              final label =
                                  _s(row, ['ratingLabel', 'RatingLabel']);
                              return ListTile(
                                title: Text(
                                  name.isEmpty ? 'زبون' : name,
                                  style: const TextStyle(
                                    fontWeight: FontWeight.w600,
                                  ),
                                ),
                                subtitle: Text(
                                  [
                                    if (phone.isNotEmpty) phone,
                                    if (label.isNotEmpty) label,
                                  ].join(' · '),
                                  style: const TextStyle(
                                    fontSize: 12,
                                    color: AppColors.muted,
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
    );
  }
}
