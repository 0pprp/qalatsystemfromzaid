import 'package:branch_manager_application/core/api.dart';
import 'package:branch_manager_application/core/session.dart';
import 'package:branch_manager_application/theme/bm_theme.dart';
import 'package:flutter/material.dart';
import 'package:intl/intl.dart' hide TextDirection;

class DecisionsPage extends StatefulWidget {
  const DecisionsPage({super.key});

  @override
  State<DecisionsPage> createState() => _DecisionsPageState();
}

class _DecisionsPageState extends State<DecisionsPage> {
  bool _loading = true;
  String? _error;
  List<Map<String, dynamic>> _items = [];

  final _money = NumberFormat('#,##0', 'en');

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final rows = await BmApi.instance.weakWeekPayers();
      if (!mounted) return;
      // مصفرون (الباقي ≤ 0) لا يحتاجون قراراً؛ أسماء قانونية تتطلب ≥40 يوماً منذ آخر تسديد
      final open = rows.where((r) {
        final remain = _d(r, ['amountRemaining', 'AmountRemaining']);
        if (remain <= 0) return false;
        return _daysSinceLastPayment(r) >= 40;
      }).toList();
      setState(() {
        _items = open;
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

  /// أيام منذ آخر تسديد. بلا تسديد أصلاً يُعدّ متوقفاً (قيمة كبيرة).
  int _daysSinceLastPayment(Map r) {
    final raw = _s(r, ['lastPaymentDate', 'LastPaymentDate']);
    if (raw.isEmpty) return 9999;
    final dt = DateTime.tryParse(raw);
    if (dt == null) return 9999;
    final today = DateTime.now();
    final a = DateTime(today.year, today.month, today.day);
    final b = DateTime(dt.year, dt.month, dt.day);
    return a.difference(b).inDays;
  }

  String _s(Map m, List<String> keys) {
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

  Future<void> _openDecide(Map<String, dynamic> item) async {
    final customerId =
        int.tryParse(_s(item, ['customerID', 'CustomerID', 'customerId'])) ?? 0;
    if (customerId <= 0) return;

    String? type;
    final noteCtrl = TextEditingController();
    final ok = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.white,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(22)),
      ),
      builder: (ctx) {
        return Directionality(
          textDirection: TextDirection.rtl,
          child: Padding(
            padding: EdgeInsets.only(
              left: 20,
              right: 20,
              top: 16,
              bottom: MediaQuery.of(ctx).viewInsets.bottom + 20,
            ),
            child: StatefulBuilder(
              builder: (ctx, setLocal) {
                return Column(
                  mainAxisSize: MainAxisSize.min,
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    Center(
                      child: Container(
                        width: 42,
                        height: 4,
                        decoration: BoxDecoration(
                          color: Colors.black12,
                          borderRadius: BorderRadius.circular(99),
                        ),
                      ),
                    ),
                    const SizedBox(height: 14),
                    Text(
                      _s(item, ['customerName', 'CustomerName']),
                      style: const TextStyle(
                        fontFamily: 'Cairo',
                        fontWeight: FontWeight.bold,
                        fontSize: 18,
                      ),
                    ),
                    const SizedBox(height: 6),
                    Text(
                      'اختر القرار — نسبة التسديد ${_d(item, ['paidPercent', 'PaidPercent']).toStringAsFixed(2)}%',
                      style: const TextStyle(
                        fontFamily: 'Cairo',
                        color: BmTheme.muted,
                      ),
                    ),
                    const SizedBox(height: 16),
                    _choice(
                      selected: type == 'متواصل',
                      color: BmTheme.primary,
                      title: 'متواصل معه',
                      subtitle: 'ملاحظة إجبارية — يظهر بعد أسبوع إن بقي ضمن الشرط',
                      onTap: () => setLocal(() => type = 'متواصل'),
                    ),
                    _choice(
                      selected: type == 'قانونية',
                      color: BmTheme.danger,
                      title: 'قانونية',
                      subtitle: 'تأشير الزبون قانونية عند المحاسب الرئيسي',
                      onTap: () => setLocal(() => type = 'قانونية'),
                    ),
                    _choice(
                      selected: type == 'وهمي',
                      color: BmTheme.warn,
                      title: 'مبيع وهمي',
                      subtitle: 'وسم المبيع كوهمي وإخراجه من القائمة',
                      onTap: () => setLocal(() => type = 'وهمي'),
                    ),
                    _choice(
                      selected: type == 'تنازل',
                      color: const Color(0xFF0F766E),
                      title: 'التنازل',
                      subtitle: 'إرسال طلب تنازل للمدير المفوض — ملاحظة إجبارية',
                      onTap: () => setLocal(() => type = 'تنازل'),
                    ),
                    if (type != null) ...[
                      const SizedBox(height: 12),
                      TextField(
                        controller: noteCtrl,
                        maxLines: 3,
                        decoration: InputDecoration(
                          labelText: type == 'تنازل'
                              ? 'ملاحظة إجبارية للمدير المفوض'
                              : 'ملاحظة إجبارية',
                        ),
                        style: const TextStyle(fontFamily: 'Cairo'),
                      ),
                    ],
                    const SizedBox(height: 16),
                    FilledButton(
                      onPressed: type == null
                          ? null
                          : () async {
                              if (noteCtrl.text.trim().isEmpty) {
                                await showDialog<void>(
                                  context: ctx,
                                  builder: (dCtx) => Directionality(
                                    textDirection: TextDirection.rtl,
                                    child: AlertDialog(
                                      title: const Text(
                                        'الملاحظة إلزامية',
                                        style: TextStyle(fontFamily: 'Cairo'),
                                      ),
                                      content: const Text(
                                        'يجب كتابة ملاحظة قبل اعتماد القرار.',
                                        style: TextStyle(fontFamily: 'Cairo'),
                                      ),
                                      actions: [
                                        TextButton(
                                          onPressed: () => Navigator.pop(dCtx),
                                          child: const Text(
                                            'حسناً',
                                            style: TextStyle(fontFamily: 'Cairo'),
                                          ),
                                        ),
                                      ],
                                    ),
                                  ),
                                );
                                return;
                              }
                              Navigator.pop(ctx, true);
                            },
                      child: Text(
                        type == 'تنازل' ? 'إرسال طلب التنازل' : 'اعتماد القرار',
                        style: const TextStyle(fontFamily: 'Cairo'),
                      ),
                    ),
                  ],
                );
              },
            ),
          ),
        );
      },
    );

    final decisionType = type;
    final note = noteCtrl.text.trim();
    noteCtrl.dispose();
    if (ok != true || decisionType == null) return;

    try {
      if (decisionType == 'تنازل') {
        final name = _s(item, ['customerName', 'CustomerName']);
        final phone = _s(item, ['phoneNumber', 'PhoneNumber']);
        await BmApi.instance.createWaiver(
          customerId: customerId,
          customerName: name.isEmpty ? null : name,
          customerPhone: phone.isEmpty ? null : phone,
          note: note,
        );
        if (!mounted) return;
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text(
              'تم إرسال طلب التنازل إلى المدير المفوض',
              style: TextStyle(fontFamily: 'Cairo'),
            ),
          ),
        );
      } else {
        await BmApi.instance.decide(
          customerId: customerId,
          decisionType: decisionType,
          note: note.isEmpty ? null : note,
        );
        if (!mounted) return;
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(
              'تم تسجيل القرار: $decisionType',
              style: const TextStyle(fontFamily: 'Cairo'),
            ),
          ),
        );
      }
      await _load();
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('$e', style: const TextStyle(fontFamily: 'Cairo')),
        ),
      );
    }
  }

  Widget _choice({
    required bool selected,
    required Color color,
    required String title,
    required String subtitle,
    required VoidCallback onTap,
  }) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 8),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(14),
        child: Container(
          padding: const EdgeInsets.all(12),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(14),
            border: Border.all(
              color: selected ? color : color.withValues(alpha: 0.25),
              width: selected ? 2 : 1,
            ),
            color: selected ? color.withValues(alpha: 0.08) : Colors.white,
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                title,
                style: TextStyle(
                  fontFamily: 'Cairo',
                  fontWeight: FontWeight.bold,
                  color: color,
                ),
              ),
              Text(
                subtitle,
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
    );
  }

  @override
  Widget build(BuildContext context) {
    return RefreshIndicator(
      onRefresh: _load,
      child: CustomScrollView(
        physics: const AlwaysScrollableScrollPhysics(),
        slivers: [
          SliverToBoxAdapter(
            child: Padding(
              padding: const EdgeInsets.fromLTRB(16, 16, 16, 8),
              child: Container(
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
                      'أسماء قانونية',
                      style: TextStyle(
                        fontFamily: 'Cairo',
                        color: Colors.white,
                        fontWeight: FontWeight.bold,
                        fontSize: 18,
                      ),
                    ),
                    const SizedBox(height: 6),
                    Text(
                      'تسديد آخر 7 أيام ≤ 3.8% من مبلغ البيع، وآخر تسديد منذ ≥ 40 يوماً. '
                      '«متواصل» لا يتكرر قبل انتهاء الأسبوع.',
                      style: TextStyle(
                        fontFamily: 'Cairo',
                        color: Colors.white.withValues(alpha: 0.9),
                        fontSize: 12,
                        height: 1.45,
                      ),
                    ),
                    const SizedBox(height: 12),
                    Row(
                      children: [
                        _statChip('${_items.length}', 'زبون'),
                        const SizedBox(width: 8),
                        _statChip(
                          _items.isEmpty
                              ? '0%'
                              : '${(_items.map((e) => _d(e, ['paidPercent', 'PaidPercent'])).fold<double>(0, (a, b) => a + b) / _items.length).toStringAsFixed(1)}%',
                          'متوسط النسبة',
                        ),
                      ],
                    ),
                  ],
                ),
              ),
            ),
          ),
          if (_loading)
            const SliverFillRemaining(
              child: Center(child: CircularProgressIndicator()),
            )
          else if (_error != null)
            SliverFillRemaining(
              child: Center(
                child: Padding(
                  padding: const EdgeInsets.all(24),
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Text(_error!,
                          textAlign: TextAlign.center,
                          style: const TextStyle(fontFamily: 'Cairo')),
                      const SizedBox(height: 12),
                      FilledButton(onPressed: _load, child: const Text('إعادة')),
                    ],
                  ),
                ),
              ),
            )
          else if (_items.isEmpty)
            const SliverFillRemaining(
              child: Center(
                child: Text(
                  'لا يوجد زبائن ضمن شرط النسبة وآخر تسديد ≥40 يوماً حالياً',
                  style: TextStyle(fontFamily: 'Cairo', color: BmTheme.muted),
                ),
              ),
            )
          else
            SliverPadding(
              padding: const EdgeInsets.fromLTRB(16, 8, 16, 24),
              sliver: SliverList.separated(
                itemCount: _items.length,
                separatorBuilder: (_, __) => const SizedBox(height: 10),
                itemBuilder: (context, i) {
                  final item = _items[i];
                  final name = _s(item, ['customerName', 'CustomerName']);
                  final phone = _s(item, ['phoneNumber', 'PhoneNumber']);
                  final list = _s(item, ['delegateName', 'DelegateName']);
                  final pct = _d(item, ['paidPercent', 'PaidPercent']);
                  final total = _d(item, ['amountTotalSales', 'AmountTotalSales']);
                  final week = _d(item, ['weekPaid', 'WeekPaid']);
                  final remain = _d(item, ['amountRemaining', 'AmountRemaining']);
                  return Material(
                    color: BmTheme.card,
                    borderRadius: BorderRadius.circular(18),
                    child: InkWell(
                      borderRadius: BorderRadius.circular(18),
                      onTap: () => _openDecide(item),
                      child: Container(
                        padding: const EdgeInsets.all(14),
                        decoration: BoxDecoration(
                          borderRadius: BorderRadius.circular(18),
                          border: Border.all(
                            color: BmTheme.primary.withValues(alpha: 0.08),
                          ),
                        ),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Row(
                              children: [
                                Expanded(
                                  child: Text(
                                    name,
                                    style: const TextStyle(
                                      fontFamily: 'Cairo',
                                      fontWeight: FontWeight.bold,
                                      fontSize: 16,
                                    ),
                                  ),
                                ),
                                Container(
                                  padding: const EdgeInsets.symmetric(
                                      horizontal: 10, vertical: 4),
                                  decoration: BoxDecoration(
                                    color: BmTheme.accent.withValues(alpha: 0.12),
                                    borderRadius: BorderRadius.circular(99),
                                  ),
                                  child: Text(
                                    '${pct.toStringAsFixed(2)}%',
                                    style: const TextStyle(
                                      fontFamily: 'Cairo',
                                      fontWeight: FontWeight.bold,
                                      color: BmTheme.accent,
                                    ),
                                  ),
                                ),
                              ],
                            ),
                            if (phone.isNotEmpty)
                              Text(phone,
                                  style: const TextStyle(
                                      fontFamily: 'Cairo',
                                      color: BmTheme.muted,
                                      fontSize: 13)),
                            if (list.isNotEmpty)
                              Text('القائمة: $list',
                                  style: const TextStyle(
                                      fontFamily: 'Cairo',
                                      color: BmTheme.muted,
                                      fontSize: 12)),
                            const SizedBox(height: 10),
                            Wrap(
                              spacing: 10,
                              runSpacing: 6,
                              children: [
                                _mini('البيع', '${_money.format(total)} دع'),
                                _mini('هذا الأسبوع', '${_money.format(week)} دع'),
                                _mini('الباقي', '${_money.format(remain)} دع'),
                              ],
                            ),
                            const SizedBox(height: 10),
                            Align(
                              alignment: Alignment.centerLeft,
                              child: Text(
                                'اضغط لاتخاذ قرار',
                                style: TextStyle(
                                  fontFamily: 'Cairo',
                                  fontSize: 12,
                                  color: BmTheme.primary.withValues(alpha: 0.85),
                                  fontWeight: FontWeight.bold,
                                ),
                              ),
                            ),
                          ],
                        ),
                      ),
                    ),
                  );
                },
              ),
            ),
        ],
      ),
    );
  }

  Widget _statChip(String value, String label) {
    return Expanded(
      child: Container(
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
                    fontSize: 16)),
            Text(label,
                style: TextStyle(
                    fontFamily: 'Cairo',
                    color: Colors.white.withValues(alpha: 0.8),
                    fontSize: 11)),
          ],
        ),
      ),
    );
  }

  Widget _mini(String k, String v) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
      decoration: BoxDecoration(
        color: BmTheme.surface,
        borderRadius: BorderRadius.circular(10),
      ),
      child: Text('$k: $v',
          style: const TextStyle(fontFamily: 'Cairo', fontSize: 12)),
    );
  }
}
