import 'package:branch_manager_application/core/api.dart';
import 'package:branch_manager_application/core/session.dart';
import 'package:branch_manager_application/theme/bm_theme.dart';
import 'package:branch_manager_application/ui/new_sale_detail_page.dart';
import 'package:branch_manager_application/ui/request_sheets.dart';
import 'package:flutter/material.dart';
import 'package:intl/intl.dart' hide TextDirection;

class NewSalesPage extends StatefulWidget {
  const NewSalesPage({super.key});

  @override
  State<NewSalesPage> createState() => _NewSalesPageState();
}

class _NewSalesPageState extends State<NewSalesPage> {
  final _money = NumberFormat('#,##0', 'en');
  bool _loading = true;
  bool _unreadOnly = true;
  String? _error;
  List<Map<String, dynamic>> _items = [];
  int _unread = 0;

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
      final rows = await BmApi.instance.newSales(unreadOnly: _unreadOnly);
      final count = await BmApi.instance.newSalesUnreadCount();
      if (!mounted) return;
      setState(() {
        _items = rows;
        _unread = count;
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

  bool _isRead(Map m) {
    if (m['isRead'] == true || m['IsRead'] == true) return true;
    return _s(m, ['branchManagerReadAtUtc', 'BranchManagerReadAtUtc']).isNotEmpty;
  }

  Future<void> _markAll() async {
    try {
      final n = await BmApi.instance.markAllNewSalesRead();
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(n > 0 ? 'تم تعليم $n كمقروء' : 'لا يوجد غير مقروء')),
      );
      await _load();
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('$e')));
    }
  }

  Future<void> _open(Map<String, dynamic> row) async {
    final id = int.tryParse(_s(row, ['saleId', 'SaleId'])) ?? 0;
    if (id <= 0) return;
    await Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (_) => NewSaleDetailPage(saleId: id, seed: row),
      ),
    );
    if (mounted) _load();
  }

  @override
  Widget build(BuildContext context) {
    return Directionality(
      textDirection: TextDirection.rtl,
      child: Column(
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(12, 10, 12, 6),
            child: Row(
              children: [
                FilterChip(
                  label: Text(_unreadOnly ? 'غير المقروء ($_unread)' : 'الكل'),
                  selected: _unreadOnly,
                  onSelected: (v) {
                    setState(() => _unreadOnly = v);
                    _load();
                  },
                ),
                const Spacer(),
                TextButton.icon(
                  onPressed: _unread > 0 ? _markAll : null,
                  icon: const Icon(Icons.done_all, size: 18),
                  label: const Text('جعل الجميع مقروءاً',
                      style: TextStyle(fontFamily: 'Cairo', fontSize: 12)),
                ),
              ],
            ),
          ),
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 12),
            child: Row(
              children: [
                Expanded(
                  child: OutlinedButton.icon(
                    onPressed: () => SalesRequestSheet.show(context).then((_) {}),
                    icon: const Icon(Icons.add_shopping_cart_outlined),
                    label: const Text('طلب مبيع جديد',
                        style: TextStyle(fontFamily: 'Cairo', fontSize: 12)),
                  ),
                ),
                const SizedBox(width: 8),
                Expanded(
                  child: OutlinedButton.icon(
                    onPressed: () => DelegatedManagerRequestSheet.show(context),
                    icon: const Icon(Icons.support_agent_outlined),
                    label: const Text('طلب للمدير المفوض',
                        style: TextStyle(fontFamily: 'Cairo', fontSize: 12)),
                  ),
                ),
              ],
            ),
          ),
          if (_loading) const LinearProgressIndicator(minHeight: 2),
          Expanded(child: _body()),
        ],
      ),
    );
  }

  Widget _body() {
    if (_error != null) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(_error!,
                  textAlign: TextAlign.center,
                  style: const TextStyle(fontFamily: 'Cairo', color: BmTheme.danger)),
              const SizedBox(height: 12),
              FilledButton(onPressed: _load, child: const Text('إعادة')),
            ],
          ),
        ),
      );
    }

    if (!_loading && _items.isEmpty) {
      return const Center(
        child: Text(
          'لا توجد مبيعات جديدة حالياً',
          style: TextStyle(fontFamily: 'Cairo', color: BmTheme.muted),
        ),
      );
    }

    return RefreshIndicator(
      onRefresh: _load,
      child: ListView.separated(
        padding: const EdgeInsets.fromLTRB(16, 8, 16, 24),
        itemCount: _items.length,
        separatorBuilder: (_, _) => const SizedBox(height: 10),
        itemBuilder: (ctx, i) {
          final row = _items[i];
          final name = _s(row, ['fullName', 'FullName']);
          final emp = _s(row, ['employeeName', 'EmployeeName']);
          final phone = _s(row, ['phone', 'Phone']);
          final price = _d(row, ['finalSalePrice', 'FinalSalePrice']);
          final read = _isRead(row);
          return Material(
            color: read ? BmTheme.card : const Color(0xFFE8F5F3),
            borderRadius: BorderRadius.circular(16),
            child: InkWell(
              borderRadius: BorderRadius.circular(16),
              onTap: () => _open(row),
              child: Container(
                padding: const EdgeInsets.all(14),
                decoration: BoxDecoration(
                  borderRadius: BorderRadius.circular(16),
                  border: Border.all(
                    color: read
                        ? BmTheme.primary.withValues(alpha: 0.08)
                        : BmTheme.primary.withValues(alpha: 0.28),
                  ),
                ),
                child: Row(
                  children: [
                    Icon(
                      read ? Icons.mark_email_read_outlined : Icons.fiber_new,
                      color: read ? BmTheme.muted : BmTheme.accent,
                    ),
                    const SizedBox(width: 10),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            name.isEmpty ? 'مبيع بدون اسم' : name,
                            style: const TextStyle(
                              fontFamily: 'Cairo',
                              fontWeight: FontWeight.bold,
                              color: BmTheme.ink,
                            ),
                          ),
                          const SizedBox(height: 4),
                          Text(
                            [
                              if (emp.isNotEmpty) 'الموظف: $emp',
                              if (phone.isNotEmpty) phone,
                            ].join(' · '),
                            style: const TextStyle(
                              fontFamily: 'Cairo',
                              fontSize: 12,
                              color: BmTheme.muted,
                            ),
                          ),
                          const SizedBox(height: 4),
                          Text(
                            '${_money.format(price)} دع',
                            style: const TextStyle(
                              fontFamily: 'Cairo',
                              fontWeight: FontWeight.w600,
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
}
