import 'dart:async';

import 'package:branch_manager_application/core/api.dart';
import 'package:branch_manager_application/core/session.dart';
import 'package:branch_manager_application/theme/bm_theme.dart';
import 'package:branch_manager_application/ui/customer_profile_page.dart';
import 'package:flutter/material.dart';
import 'package:intl/intl.dart' hide TextDirection;

class CustomersPage extends StatefulWidget {
  const CustomersPage({super.key});

  @override
  State<CustomersPage> createState() => _CustomersPageState();
}

class _CustomersPageState extends State<CustomersPage> {
  final _searchCtrl = TextEditingController();
  final _money = NumberFormat('#,##0', 'en');
  Timer? _debounce;
  bool _loading = false;
  String? _error;
  List<Map<String, dynamic>> _items = [];
  bool _searched = false;

  @override
  void dispose() {
    _debounce?.cancel();
    _searchCtrl.dispose();
    super.dispose();
  }

  void _onQueryChanged(String _) {
    _debounce?.cancel();
    _debounce = Timer(const Duration(milliseconds: 450), () {
      final q = _searchCtrl.text.trim();
      if (q.isEmpty) {
        setState(() {
          _items = [];
          _error = null;
          _searched = false;
          _loading = false;
        });
        return;
      }
      if (q.length < 2) return;
      _load(q);
    });
  }

  Future<void> _load(String query) async {
    setState(() {
      _loading = true;
      _error = null;
      _searched = true;
    });
    try {
      final rows = await BmApi.instance.searchCustomers(text: query);
      if (!mounted) return;
      setState(() {
        _items = rows;
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

  Future<void> _openProfile(Map<String, dynamic> row) async {
    final id = int.tryParse(_s(row, ['customerID', 'CustomerID', 'customerId'])) ?? 0;
    if (id <= 0) return;
    final name = _s(row, ['customerName', 'CustomerName']);
    await Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (_) => CustomerProfilePage(
          customerId: id,
          initialName: name.isEmpty ? null : name,
          seed: row,
        ),
      ),
    );
  }

  Future<void> _requestWaiver(Map<String, dynamic> row) async {
    final id = int.tryParse(_s(row, ['customerID', 'CustomerID', 'customerId'])) ?? 0;
    if (id <= 0) return;
    final name = _s(row, ['customerName', 'CustomerName']);
    final phone = _s(row, ['phone', 'Phone', 'customerPhone', 'CustomerPhone']);
    final noteCtrl = TextEditingController();
    final ok = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('طلب تنازل', style: TextStyle(fontFamily: 'Cairo')),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text(
              name.isEmpty ? 'زبون #$id' : name,
              style: const TextStyle(fontFamily: 'Cairo', fontWeight: FontWeight.bold),
            ),
            const SizedBox(height: 12),
            TextField(
              controller: noteCtrl,
              maxLines: 4,
              maxLength: 2000,
              decoration: const InputDecoration(
                labelText: 'ملاحظة إجبارية للمدير المفوض',
                alignLabelWithHint: true,
              ),
              style: const TextStyle(fontFamily: 'Cairo'),
            ),
          ],
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx, false),
            child: const Text('إلغاء'),
          ),
          FilledButton(
            onPressed: () {
              if (noteCtrl.text.trim().isEmpty) return;
              Navigator.pop(ctx, true);
            },
            child: const Text('إرسال'),
          ),
        ],
      ),
    );
    final note = noteCtrl.text.trim();
    noteCtrl.dispose();
    if (ok != true || !mounted) return;
    if (note.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('الملاحظة إجبارية', style: TextStyle(fontFamily: 'Cairo'))),
      );
      return;
    }

    showDialog(
      context: context,
      barrierDismissible: false,
      builder: (_) => const Center(child: CircularProgressIndicator()),
    );
    try {
      await BmApi.instance.createWaiver(
        customerId: id,
        customerName: name.isEmpty ? null : name,
        customerPhone: phone.isEmpty ? null : phone,
        note: note,
      );
      if (!mounted) return;
      Navigator.of(context, rootNavigator: true).pop();
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('تم إرسال طلب التنازل إلى المدير المفوض',
              style: TextStyle(fontFamily: 'Cairo')),
        ),
      );
    } catch (e) {
      if (!mounted) return;
      Navigator.of(context, rootNavigator: true).pop();
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('$e', style: const TextStyle(fontFamily: 'Cairo'))),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    return Directionality(
      textDirection: TextDirection.rtl,
      child: Column(
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 12, 16, 8),
            child: TextField(
              controller: _searchCtrl,
              onChanged: _onQueryChanged,
              textInputAction: TextInputAction.search,
              onSubmitted: (v) {
                final q = v.trim();
                if (q.length >= 2) _load(q);
              },
              decoration: InputDecoration(
                hintText: 'ابحث بالاسم أو الهاتف أو رقم الزبون…',
                prefixIcon: const Icon(Icons.search),
                suffixIcon: _searchCtrl.text.isEmpty
                    ? null
                    : IconButton(
                        tooltip: 'مسح',
                        onPressed: () {
                          _searchCtrl.clear();
                          _onQueryChanged('');
                          setState(() {});
                        },
                        icon: const Icon(Icons.clear),
                      ),
              ),
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
              Text(_error!, textAlign: TextAlign.center,
                  style: const TextStyle(fontFamily: 'Cairo', color: BmTheme.danger)),
              const SizedBox(height: 12),
              FilledButton(
                onPressed: () => _load(_searchCtrl.text.trim()),
                child: const Text('إعادة المحاولة'),
              ),
            ],
          ),
        ),
      );
    }

    if (!_searched) {
      return const Center(
        child: Padding(
          padding: EdgeInsets.all(32),
          child: Text(
            'اكتب حرفين على الأقل للبحث عن زبائن فرعك وكتابة الملاحظات.',
            textAlign: TextAlign.center,
            style: TextStyle(fontFamily: 'Cairo', color: BmTheme.muted, height: 1.6),
          ),
        ),
      );
    }

    if (!_loading && _items.isEmpty) {
      return const Center(
        child: Text(
          'لا توجد نتائج مطابقة',
          style: TextStyle(fontFamily: 'Cairo', color: BmTheme.muted),
        ),
      );
    }

    return ListView.separated(
      padding: const EdgeInsets.fromLTRB(16, 4, 16, 24),
      itemCount: _items.length,
      separatorBuilder: (_, _) => const SizedBox(height: 10),
      itemBuilder: (ctx, i) {
        final row = _items[i];
        final name = _s(row, ['customerName', 'CustomerName']);
        final delegate = _s(row, ['delegateName', 'DelegateName']);
        final phone = _s(row, ['phone', 'Phone', 'customerPhone', 'CustomerPhone']);
        final remaining = _d(row, ['amountRemaining', 'AmountRemaining']);
        final total = _d(row, ['amountTotalSales', 'AmountTotalSales']);
        final rating = _s(row, ['ratingLabel', 'RatingLabel', 'rating', 'Rating']);

        return Material(
          color: BmTheme.card,
          borderRadius: BorderRadius.circular(16),
          child: InkWell(
            borderRadius: BorderRadius.circular(16),
            onTap: () => _openProfile(row),
            child: Container(
              padding: const EdgeInsets.all(14),
              decoration: BoxDecoration(
                borderRadius: BorderRadius.circular(16),
                border: Border.all(color: BmTheme.primary.withValues(alpha: 0.08)),
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Row(
                    children: [
                      CircleAvatar(
                        backgroundColor: BmTheme.primary.withValues(alpha: 0.12),
                        foregroundColor: BmTheme.primary,
                        child: Text(
                          name.isEmpty ? '?' : name.characters.first,
                          style: const TextStyle(fontFamily: 'Cairo', fontWeight: FontWeight.bold),
                        ),
                      ),
                      const SizedBox(width: 12),
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(
                              name.isEmpty ? 'زبون بدون اسم' : name,
                              style: const TextStyle(
                                fontFamily: 'Cairo',
                                fontWeight: FontWeight.bold,
                                fontSize: 15,
                                color: BmTheme.ink,
                              ),
                            ),
                            const SizedBox(height: 4),
                            Text(
                              [
                                if (delegate.isNotEmpty) 'المندوب: $delegate',
                                if (phone.isNotEmpty) phone,
                              ].join(' · '),
                              style: const TextStyle(
                                fontFamily: 'Cairo',
                                fontSize: 12,
                                color: BmTheme.muted,
                              ),
                            ),
                            const SizedBox(height: 6),
                            Text(
                              'البيع ${_money.format(total)} · الباقي ${_money.format(remaining)}',
                              style: const TextStyle(fontFamily: 'Cairo', fontSize: 12),
                            ),
                          ],
                        ),
                      ),
                      if (rating.isNotEmpty)
                        Container(
                          padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                          decoration: BoxDecoration(
                            color: BmTheme.accent.withValues(alpha: 0.12),
                            borderRadius: BorderRadius.circular(10),
                          ),
                          child: Text(
                            rating,
                            style: const TextStyle(
                              fontFamily: 'Cairo',
                              fontSize: 11,
                              color: BmTheme.accent,
                              fontWeight: FontWeight.bold,
                            ),
                          ),
                        ),
                      const SizedBox(width: 4),
                      const Icon(Icons.chevron_left, color: BmTheme.muted),
                    ],
                  ),
                  const SizedBox(height: 10),
                  FilledButton.tonal(
                    onPressed: () => _requestWaiver(row),
                    style: FilledButton.styleFrom(
                      backgroundColor: BmTheme.accent.withValues(alpha: 0.14),
                      foregroundColor: BmTheme.accent,
                      padding: const EdgeInsets.symmetric(vertical: 10),
                    ),
                    child: const Text(
                      'طلب تنازل',
                      style: TextStyle(
                        fontFamily: 'Cairo',
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
    );
  }
}
