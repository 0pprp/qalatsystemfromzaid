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
  late DateTime _from;
  late DateTime _to;

  String _scope = 'branch'; // branch | list | follower | employee
  int _entityId = 0;

  bool _loadingCatalog = true;
  bool _loadingTrend = true;
  String? _error;

  List<Map<String, dynamic>> _lists = [];
  List<Map<String, dynamic>> _followers = [];
  List<Map<String, dynamic>> _employees = [];
  Map<String, dynamic>? _trend;

  static const _sand = Color(0xFFEEF3EF);
  static const _accentDeep = Color(0xFF0F3D32);
  static const _accent = Color(0xFF1F6F5B);
  static const _warn = Color(0xFFB45309);

  @override
  void initState() {
    super.initState();
    final now = DateTime.now();
    _from = DateTime(now.year, now.month, 1);
    _to = DateTime(now.year, now.month, now.day);
    _bootstrap();
  }

  Future<void> _bootstrap() async {
    await _loadCatalog();
    await _loadTrend();
  }

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
    setState(() {
      _loadingTrend = true;
      _error = null;
    });
    try {
      final trend = await BmApi.instance.statsTrend(
        scope: _scope,
        id: _entityId,
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
  }

  void _setScope(String scope) {
    setState(() {
      _scope = scope;
      _entityId = 0;
      if (scope == 'list' && _lists.isNotEmpty) {
        _entityId = int.tryParse(_s(_lists.first, ['id', 'Id'])) ?? 0;
      } else if (scope == 'follower' && _followers.isNotEmpty) {
        _entityId = int.tryParse(_s(_followers.first, ['id', 'Id'])) ?? 0;
      } else if (scope == 'employee' && _employees.isNotEmpty) {
        _entityId = int.tryParse(_s(_employees.first, ['id', 'Id'])) ?? 0;
      }
    });
    _loadTrend();
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
    if (_scope != 'branch' && _entityId <= 0) {
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
          scope: _scope,
          entityId: _entityId,
          from: _from,
          to: _to,
          level: level,
        );
      },
    );
  }

  @override
  Widget build(BuildContext context) {
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
    final phantomEndValue = phantomEnd == null ? null : _d(_trend, ['phantomEndValue', 'PhantomEndValue']);
    final phantomLabel = _s(_trend, ['phantomRatingLabel', 'PhantomRatingLabel']);
    final phantomMetric = _s(_trend, ['phantomMetricLabel', 'PhantomMetricLabel']);
    final phantomCustomerCount =
        _d(_trend, ['phantomCustomerCount', 'PhantomCustomerCount']).toInt();
    final hasPhantom = phantomEnd != null;
    final excellent = _d(_trend, ['excellentCount', 'ExcellentCount']).toInt();
    final good = _d(_trend, ['goodCount', 'GoodCount']).toInt();
    final weak = _d(_trend, ['weakCount', 'WeakCount']).toInt();
    final rejected = _d(_trend, ['rejectedCount', 'RejectedCount']).toInt();
    final bad = _d(_trend, ['badCount', 'BadCount']).toInt();
    final hasBuckets = excellent + good + weak + rejected + bad > 0 || customerCount > 0;
    final pillColor = down ? _warn : _accentDeep;
    final pillBg = down
        ? _warn.withValues(alpha: 0.15)
        : _accent.withValues(alpha: 0.12);
    final countLabel = _isEmployeeScope
        ? 'كل الزبائن ضمن الفترة: $customerCount'
        : 'الزبائن المسددين: $customerCount';
    final fallbackMetric = _isEmployeeScope
        ? 'التقييم الحقيقي'
        : 'التقييم الحقيقي';

    return Directionality(
      textDirection: TextDirection.rtl,
      child: ListView(
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
                onPressed: (_loadingTrend || _loadingCatalog) ? null : _loadTrend,
                icon: const Icon(Icons.refresh),
              ),
            ],
          ),
          const SizedBox(height: 6),
          const Text(
            'لوحة التقييم — الفترة = تاريخ البيع فقط، والتقييم/التسديدات تُحسب حتى اليوم.',
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
                labelText: _isEmployeeScope ? 'اختر الموظف' : 'اختر للرسم',
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
                setState(() => _entityId = v);
                _loadTrend();
              },
            ),
          ],
          const SizedBox(height: 14),
          if (_error != null)
            Text(_error!,
                style: const TextStyle(fontFamily: 'Cairo', color: BmTheme.danger))
          else ...[
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
                  if (hasPhantom)
                    Row(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(
                                metric.isEmpty ? fallbackMetric : metric,
                                style: const TextStyle(
                                  fontFamily: 'Cairo',
                                  color: BmTheme.muted,
                                  fontSize: 12,
                                ),
                              ),
                              const SizedBox(height: 6),
                              Text(
                                _score.format(endValue),
                                style: const TextStyle(
                                  fontFamily: 'Cairo',
                                  color: _accentDeep,
                                  fontWeight: FontWeight.w800,
                                  fontSize: 32,
                                  height: 1.05,
                                  letterSpacing: -0.5,
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
                                      fontFamily: 'Cairo',
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
                                  fontFamily: 'Cairo',
                                  color: BmTheme.muted,
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
                                    fontFamily: 'Cairo',
                                    fontSize: 12,
                                    color: BmTheme.muted,
                                  ),
                                ),
                                const SizedBox(height: 6),
                                Text(
                                  _score.format(phantomEndValue ?? 0),
                                  style: const TextStyle(
                                    fontFamily: 'Cairo',
                                    fontSize: 28,
                                    fontWeight: FontWeight.w800,
                                    color: BmTheme.ink,
                                    height: 1.05,
                                  ),
                                ),
                                if (phantomLabel.isNotEmpty) ...[
                                  const SizedBox(height: 4),
                                  Text(
                                    phantomLabel,
                                    style: const TextStyle(
                                      fontFamily: 'Cairo',
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
                                    fontFamily: 'Cairo',
                                    fontSize: 12,
                                    color: BmTheme.muted,
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
                      metric.isEmpty ? fallbackMetric : metric,
                      style: const TextStyle(
                        fontFamily: 'Cairo',
                        color: BmTheme.muted,
                        fontSize: 12,
                      ),
                    ),
                    const SizedBox(height: 10),
                    Text(
                      _score.format(endValue),
                      style: const TextStyle(
                        fontFamily: 'Cairo',
                        color: _accentDeep,
                        fontWeight: FontWeight.w800,
                        fontSize: 40,
                        height: 1.05,
                        letterSpacing: -0.5,
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
                              fontFamily: 'Cairo',
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
                              hasPhantom
                                  ? (values.isEmpty
                                      ? countLabel
                                      : '$countLabel · ${values.length} نقطة يومية')
                                  : ('$countLabel'
                                      '${values.isEmpty ? '' : ' · ${values.length} نقطة يومية'}'),
                              style: TextStyle(
                                fontFamily: 'Cairo',
                                color: customerCount > 0
                                    ? _accent
                                    : BmTheme.muted,
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
                        border: Border.all(
                            color: _accent.withValues(alpha: 0.25)),
                      ),
                      child: Text(
                        'تقييم الفترة: ${_score.format(periodEnd)}'
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
                          height: 160,
                          child: _loadingTrend
                              ? const Center(
                                  child: CircularProgressIndicator(
                                      color: _accent))
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
              'الفترة = تاريخ البيع فقط. التقييم والمستويات تُحسب حتى اليوم (تسديدات ما بعد فترة البيع تدخل). '
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
  });

  final String title;
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
      final rows = await BmApi.instance.statsRatingCustomers(
        scope: widget.scope,
        id: widget.entityId,
        from: widget.from,
        to: widget.to,
        level: widget.level,
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
