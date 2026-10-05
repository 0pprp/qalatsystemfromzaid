import 'package:branch_manager_application/core/api.dart';
import 'package:branch_manager_application/core/session.dart';
import 'package:branch_manager_application/theme/bm_theme.dart';
import 'package:flutter/material.dart';
import 'package:intl/intl.dart' hide TextDirection;

class CustomerProfilePage extends StatefulWidget {
  const CustomerProfilePage({
    super.key,
    required this.customerId,
    this.initialName,
    this.seed,
  });

  final int customerId;
  final String? initialName;
  final Map<String, dynamic>? seed;

  @override
  State<CustomerProfilePage> createState() => _CustomerProfilePageState();
}

class _CustomerProfilePageState extends State<CustomerProfilePage> {
  final _noteCtrl = TextEditingController();
  final _money = NumberFormat('#,##0', 'en');
  final _dt = DateFormat('yyyy/MM/dd HH:mm');

  bool _loading = true;
  bool _saving = false;
  String? _error;
  Map<String, dynamic>? _customer;
  List<_TimelineItem> _timeline = [];

  @override
  void initState() {
    super.initState();
    _customer = widget.seed == null
        ? null
        : Map<String, dynamic>.from(widget.seed!);
    _load();
  }

  @override
  void dispose() {
    _noteCtrl.dispose();
    super.dispose();
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

  bool _isAutoDecisionNote(String text) {
    return text.startsWith('تم تصنيف بيع الزبون') ||
        text.startsWith('تم إرسال الزبون') ||
        text.startsWith('مدير الفرع متواصل مع الزبون');
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final results = await Future.wait([
        BmApi.instance.customerInfoSimple(widget.customerId),
        BmApi.instance.customerNotes(widget.customerId),
        BmApi.instance.decisions(customerId: widget.customerId),
      ]);
      if (!mounted) return;

      final info = results[0] as Map<String, dynamic>?;
      final notes = results[1] as List<Map<String, dynamic>>;
      final decisions = results[2] as List<Map<String, dynamic>>;

      final merged = <String, dynamic>{
        ...?widget.seed,
        ...?info,
      };

      final items = <_TimelineItem>[];
      for (final n in notes) {
        final text = _s(n, ['noteText', 'NoteText']);
        if (_isAutoDecisionNote(text)) continue;
        items.add(_TimelineItem(
          kind: _TimelineKind.note,
          date: DateTime.tryParse(_s(n, [
            'createdAtUtc',
            'CreatedAtUtc',
            'createdDate',
            'CreatedDate',
          ])),
          userName: _s(n, ['userName', 'UserName', 'createdByName', 'CreatedByName']),
          userType: _s(n, ['userType', 'UserType']),
          text: text,
        ));
      }
      for (final d in decisions) {
        items.add(_TimelineItem(
          kind: _TimelineKind.decision,
          date: DateTime.tryParse(_s(d, ['createdDate', 'CreatedDate'])),
          userName: _s(d, ['userName', 'UserName']),
          userType: _s(d, ['userType', 'UserType']),
          text: _s(d, ['decisionType', 'DecisionType']),
          extra: _s(d, ['note', 'Note']),
          decisionType: _s(d, ['decisionType', 'DecisionType']),
        ));
      }
      items.sort((a, b) {
        final ad = a.date ?? DateTime.fromMillisecondsSinceEpoch(0);
        final bd = b.date ?? DateTime.fromMillisecondsSinceEpoch(0);
        return bd.compareTo(ad);
      });

      setState(() {
        _customer = merged;
        _timeline = items;
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

  Future<void> _submitNote() async {
    final text = _noteCtrl.text.trim();
    if (text.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('اكتب الملاحظة أولاً')),
      );
      return;
    }
    setState(() => _saving = true);
    try {
      await BmApi.instance.postCustomerNote(
        customerId: widget.customerId,
        noteText: text,
      );
      if (!mounted) return;
      _noteCtrl.clear();
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('تمت إضافة الملاحظة')),
      );
      await _load();
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('$e')),
      );
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  Color _decisionColor(String type) {
    switch (type) {
      case 'قانونية':
        return BmTheme.danger;
      case 'وهمي':
        return BmTheme.warn;
      case 'تنازل':
        return const Color(0xFF0F766E);
      case 'متواصل':
        return BmTheme.primary;
      default:
        return BmTheme.muted;
    }
  }

  @override
  Widget build(BuildContext context) {
    final name = _s(_customer, ['customerName', 'CustomerName']);
    final title = name.isNotEmpty
        ? name
        : (widget.initialName ?? 'ملف الزبون');
    final delegate = _s(_customer, ['delegateName', 'DelegateName']);
    final total = _d(_customer, ['amountTotalSales', 'AmountTotalSales']);
    final receipts = _d(_customer, ['receiptsTotal', 'ReceiptsTotal']);
    final remaining = _d(_customer, ['amountRemaining', 'AmountRemaining']);
    final day = _d(_customer, ['amountDaySales', 'AmountDaySales']);

    return Directionality(
      textDirection: TextDirection.rtl,
      child: Scaffold(
        backgroundColor: BmTheme.surface,
        appBar: AppBar(
          title: Text(title),
          actions: [
            IconButton(
              tooltip: 'تحديث',
              onPressed: _loading ? null : _load,
              icon: const Icon(Icons.refresh),
            ),
          ],
        ),
        body: _loading && _customer == null
            ? const Center(child: CircularProgressIndicator())
            : _error != null && _customer == null
                ? Center(
                    child: Padding(
                      padding: const EdgeInsets.all(24),
                      child: Column(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          Text(_error!,
                              textAlign: TextAlign.center,
                              style: const TextStyle(
                                  fontFamily: 'Cairo', color: BmTheme.danger)),
                          const SizedBox(height: 12),
                          FilledButton(
                              onPressed: _load, child: const Text('إعادة')),
                        ],
                      ),
                    ),
                  )
                : RefreshIndicator(
                    onRefresh: _load,
                    child: ListView(
                      padding: const EdgeInsets.fromLTRB(16, 12, 16, 32),
                      children: [
                        if (_loading) const LinearProgressIndicator(minHeight: 2),
                        Text(
                          delegate.isEmpty ? 'مندوب غير محدد' : 'المندوب: $delegate',
                          style: const TextStyle(
                            fontFamily: 'Cairo',
                            color: BmTheme.muted,
                          ),
                        ),
                        const SizedBox(height: 12),
                        Row(
                          children: [
                            Expanded(
                              child: _StatTile(
                                label: 'سعر البيع',
                                value: _money.format(total),
                              ),
                            ),
                            const SizedBox(width: 8),
                            Expanded(
                              child: _StatTile(
                                label: 'الواصل',
                                value: _money.format(receipts),
                              ),
                            ),
                          ],
                        ),
                        const SizedBox(height: 8),
                        Row(
                          children: [
                            Expanded(
                              child: _StatTile(
                                label: 'الباقي',
                                value: _money.format(remaining),
                              ),
                            ),
                            const SizedBox(width: 8),
                            Expanded(
                              child: _StatTile(
                                label: 'القسط',
                                value: _money.format(day),
                              ),
                            ),
                          ],
                        ),
                        const SizedBox(height: 20),
                        const Text(
                          'إضافة ملاحظة',
                          style: TextStyle(
                            fontFamily: 'Cairo',
                            fontWeight: FontWeight.bold,
                            fontSize: 16,
                            color: BmTheme.ink,
                          ),
                        ),
                        const SizedBox(height: 8),
                        TextField(
                          controller: _noteCtrl,
                          minLines: 3,
                          maxLines: 5,
                          decoration: const InputDecoration(
                            hintText: 'اكتب ملاحظة تظهر في ملف الزبون…',
                          ),
                        ),
                        const SizedBox(height: 10),
                        FilledButton.icon(
                          onPressed: _saving ? null : _submitNote,
                          icon: _saving
                              ? const SizedBox(
                                  width: 18,
                                  height: 18,
                                  child: CircularProgressIndicator(
                                    strokeWidth: 2,
                                    color: Colors.white,
                                  ),
                                )
                              : const Icon(Icons.note_add_outlined),
                          label: Text(_saving ? 'جارٍ الحفظ…' : 'حفظ الملاحظة'),
                        ),
                        const SizedBox(height: 24),
                        Row(
                          children: [
                            const Text(
                              'سجل الملاحظات والقرارات',
                              style: TextStyle(
                                fontFamily: 'Cairo',
                                fontWeight: FontWeight.bold,
                                fontSize: 16,
                                color: BmTheme.ink,
                              ),
                            ),
                            const Spacer(),
                            Text(
                              '${_timeline.length}',
                              style: const TextStyle(
                                fontFamily: 'Cairo',
                                color: BmTheme.muted,
                              ),
                            ),
                          ],
                        ),
                        const SizedBox(height: 10),
                        if (_timeline.isEmpty)
                          const Padding(
                            padding: EdgeInsets.symmetric(vertical: 24),
                            child: Text(
                              'لا توجد ملاحظات أو قرارات بعد',
                              textAlign: TextAlign.center,
                              style: TextStyle(
                                  fontFamily: 'Cairo', color: BmTheme.muted),
                            ),
                          )
                        else
                          ..._timeline.map((item) {
                            final isDecision =
                                item.kind == _TimelineKind.decision;
                            final accent = isDecision
                                ? _decisionColor(item.decisionType ?? '')
                                : BmTheme.primary;
                            return Container(
                              margin: const EdgeInsets.only(bottom: 10),
                              padding: const EdgeInsets.all(14),
                              decoration: BoxDecoration(
                                color: BmTheme.card,
                                borderRadius: BorderRadius.circular(16),
                                border: Border.all(
                                    color: accent.withValues(alpha: 0.18)),
                              ),
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.stretch,
                                children: [
                                  Row(
                                    children: [
                                      Icon(
                                        isDecision
                                            ? Icons.gavel_outlined
                                            : Icons.chat_bubble_outline,
                                        size: 18,
                                        color: accent,
                                      ),
                                      const SizedBox(width: 8),
                                      Expanded(
                                        child: Text(
                                          [
                                            if (item.userName.isNotEmpty)
                                              item.userName,
                                            if (item.userType.isNotEmpty)
                                              item.userType,
                                          ].join(' · '),
                                          style: const TextStyle(
                                            fontFamily: 'Cairo',
                                            fontSize: 12,
                                            color: BmTheme.muted,
                                          ),
                                        ),
                                      ),
                                      if (item.date != null)
                                        Text(
                                          _dt.format(item.date!.toLocal()),
                                          style: const TextStyle(
                                            fontFamily: 'Cairo',
                                            fontSize: 11,
                                            color: BmTheme.muted,
                                          ),
                                        ),
                                    ],
                                  ),
                                  const SizedBox(height: 8),
                                  Text(
                                    item.text.isEmpty ? '—' : item.text,
                                    style: TextStyle(
                                      fontFamily: 'Cairo',
                                      fontWeight: isDecision
                                          ? FontWeight.bold
                                          : FontWeight.w500,
                                      color: isDecision ? accent : BmTheme.ink,
                                      height: 1.45,
                                    ),
                                  ),
                                  if ((item.extra ?? '').isNotEmpty) ...[
                                    const SizedBox(height: 6),
                                    Text(
                                      item.extra!,
                                      style: const TextStyle(
                                        fontFamily: 'Cairo',
                                        fontSize: 13,
                                        color: BmTheme.muted,
                                        height: 1.4,
                                      ),
                                    ),
                                  ],
                                ],
                              ),
                            );
                          }),
                      ],
                    ),
                  ),
      ),
    );
  }
}

enum _TimelineKind { note, decision }

class _TimelineItem {
  _TimelineItem({
    required this.kind,
    required this.date,
    required this.userName,
    required this.userType,
    required this.text,
    this.extra,
    this.decisionType,
  });

  final _TimelineKind kind;
  final DateTime? date;
  final String userName;
  final String userType;
  final String text;
  final String? extra;
  final String? decisionType;
}

class _StatTile extends StatelessWidget {
  const _StatTile({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: BmTheme.card,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: BmTheme.primary.withValues(alpha: 0.08)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(label,
              style: const TextStyle(
                  fontFamily: 'Cairo', fontSize: 12, color: BmTheme.muted)),
          const SizedBox(height: 4),
          Text(
            '$value دع',
            style: const TextStyle(
              fontFamily: 'Cairo',
              fontWeight: FontWeight.bold,
              fontSize: 15,
              color: BmTheme.ink,
            ),
          ),
        ],
      ),
    );
  }
}
