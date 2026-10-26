import 'package:branch_manager_application/core/api.dart';
import 'package:branch_manager_application/core/session.dart';
import 'package:branch_manager_application/theme/bm_theme.dart';
import 'package:flutter/material.dart';
import 'package:intl/intl.dart' hide TextDirection;

/// Pending WrongSale inbox for إدارة الفرع — review with mandatory note.
class WrongSalesPage extends StatefulWidget {
  const WrongSalesPage({super.key});

  @override
  State<WrongSalesPage> createState() => _WrongSalesPageState();
}

class _WrongSalesPageState extends State<WrongSalesPage> {
  bool _loading = true;
  String? _error;
  List<Map<String, dynamic>> _items = [];
  final _date = DateFormat('yyyy/MM/dd HH:mm');

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
      final data = await BmApi.instance.listWrongSales(reviewed: false);
      final raw = data['items'] ?? data['Items'] ?? [];
      final list = raw is List ? raw : <dynamic>[];
      if (!mounted) return;
      setState(() {
        _items = list.map((e) => Map<String, dynamic>.from(e as Map)).toList();
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

  String _name(Map<String, dynamic> m) =>
      '${m['customerName'] ?? m['CustomerName'] ?? 'زبون'}';

  String _phone(Map<String, dynamic> m) =>
      '${m['customerPhone'] ?? m['CustomerPhone'] ?? ''}';

  String? _fmt(dynamic v) {
    if (v == null) return null;
    final d = DateTime.tryParse('$v');
    if (d == null) return '$v';
    return _date.format(d.toLocal());
  }

  Future<void> _openDetail(Map<String, dynamic> item) async {
    final id = '${item['id'] ?? item['Id'] ?? ''}';
    if (id.isEmpty) return;
    final changed = await Navigator.of(context).push<bool>(
      MaterialPageRoute(
        builder: (_) => WrongSaleDetailPage(id: id, listPreview: item),
      ),
    );
    if (changed == true && mounted) await _load();
  }

  @override
  Widget build(BuildContext context) {
    if (_loading) {
      return const Center(child: CircularProgressIndicator());
    }
    if (_error != null) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(_error!,
                  textAlign: TextAlign.center,
                  style: const TextStyle(fontFamily: 'Cairo')),
              const SizedBox(height: 12),
              FilledButton(
                onPressed: _load,
                child: const Text('إعادة المحاولة',
                    style: TextStyle(fontFamily: 'Cairo')),
              ),
            ],
          ),
        ),
      );
    }
    if (_items.isEmpty) {
      return const Center(
        child: Text(
          'لا توجد مبيعات خاطئة بانتظار المراجعة',
          style: TextStyle(fontFamily: 'Cairo', color: BmTheme.muted),
        ),
      );
    }

    return RefreshIndicator(
      onRefresh: _load,
      child: ListView.separated(
        padding: const EdgeInsets.all(12),
        itemCount: _items.length,
        separatorBuilder: (_, _) => const SizedBox(height: 8),
        itemBuilder: (ctx, i) {
          final m = _items[i];
          return Material(
            color: Colors.white,
            borderRadius: BorderRadius.circular(12),
            elevation: 0.5,
            child: InkWell(
              borderRadius: BorderRadius.circular(12),
              onTap: () => _openDetail(m),
              child: Padding(
                padding: const EdgeInsets.all(14),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      _name(m),
                      style: const TextStyle(
                        fontFamily: 'Cairo',
                        fontWeight: FontWeight.w700,
                        fontSize: 15,
                      ),
                    ),
                    if (_phone(m).isNotEmpty) ...[
                      const SizedBox(height: 4),
                      Text(_phone(m),
                          style: const TextStyle(
                              fontFamily: 'Cairo', color: BmTheme.muted)),
                    ],
                    const SizedBox(height: 6),
                    Text(
                      'تاريخ الإشعار: ${_fmt(m['requestedAtUtc'] ?? m['RequestedAtUtc']) ?? '—'}',
                      style: const TextStyle(
                          fontFamily: 'Cairo',
                          fontSize: 12,
                          color: BmTheme.muted),
                    ),
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

class WrongSaleDetailPage extends StatefulWidget {
  const WrongSaleDetailPage({
    super.key,
    required this.id,
    this.listPreview,
  });

  final String id;
  final Map<String, dynamic>? listPreview;

  @override
  State<WrongSaleDetailPage> createState() => _WrongSaleDetailPageState();
}

class _WrongSaleDetailPageState extends State<WrongSaleDetailPage> {
  bool _loading = true;
  bool _submitting = false;
  String? _error;
  Map<String, dynamic>? _request;
  final _noteCtrl = TextEditingController();

  @override
  void initState() {
    super.initState();
    _request = widget.listPreview;
    _load();
  }

  @override
  void dispose() {
    _noteCtrl.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final data = await BmApi.instance.getWrongSale(widget.id);
      final req = data['request'] ?? data['Request'];
      if (!mounted) return;
      setState(() {
        _request = req is Map ? Map<String, dynamic>.from(req) : _request;
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

  String _s(Map<String, dynamic>? m, List<String> keys) {
    if (m == null) return '';
    for (final k in keys) {
      final v = m[k];
      if (v != null && '$v'.trim().isNotEmpty) return '$v'.trim();
    }
    return '';
  }

  Future<void> _submitReview() async {
    final note = _noteCtrl.text.trim();
    if (note.length < 3) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('ملاحظة المراجعة إجبارية (3 أحرف على الأقل)',
              style: TextStyle(fontFamily: 'Cairo')),
        ),
      );
      return;
    }
    setState(() => _submitting = true);
    try {
      await BmApi.instance.reviewWrongSale(id: widget.id, note: note);
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('تم تسجيل المراجعة', style: TextStyle(fontFamily: 'Cairo')),
        ),
      );
      Navigator.of(context).pop(true);
    } catch (e) {
      if (!mounted) return;
      setState(() => _submitting = false);
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('$e', style: const TextStyle(fontFamily: 'Cairo'))),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final r = _request;
    return Directionality(
      textDirection: TextDirection.rtl,
      child: Scaffold(
        appBar: AppBar(title: const Text('مراجعة مبيع خاطئ')),
        body: _loading && r == null
            ? const Center(child: CircularProgressIndicator())
            : _error != null && r == null
                ? Center(
                    child: Column(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Text(_error!, style: const TextStyle(fontFamily: 'Cairo')),
                        const SizedBox(height: 12),
                        FilledButton(
                          onPressed: _load,
                          child: const Text('إعادة المحاولة',
                              style: TextStyle(fontFamily: 'Cairo')),
                        ),
                      ],
                    ),
                  )
                : ListView(
                    padding: const EdgeInsets.all(12),
                    children: [
                      Container(
                        width: double.infinity,
                        padding: const EdgeInsets.all(14),
                        decoration: BoxDecoration(
                          color: Colors.white,
                          borderRadius: BorderRadius.circular(12),
                          border: Border.all(color: const Color(0xFFE6E8EE)),
                        ),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            const Text(
                              'المبيع الخاطئ',
                              style: TextStyle(
                                fontFamily: 'Cairo',
                                fontWeight: FontWeight.w700,
                                color: BmTheme.primary,
                              ),
                            ),
                            const SizedBox(height: 10),
                            Text('الزبون: ${_s(r, ['customerName', 'CustomerName'])}',
                                style: const TextStyle(fontFamily: 'Cairo')),
                            Text('الهاتف: ${_s(r, ['customerPhone', 'CustomerPhone'])}',
                                style: const TextStyle(fontFamily: 'Cairo')),
                            Text(
                                'المدينة: ${_s(r, ['cityName', 'CityName', 'cityValue', 'CityValue'])}',
                                style: const TextStyle(fontFamily: 'Cairo')),
                            if (_s(r, ['reason', 'Reason']).isNotEmpty)
                              Text('السبب: ${_s(r, ['reason', 'Reason'])}',
                                  style: const TextStyle(fontFamily: 'Cairo')),
                          ],
                        ),
                      ),
                      const SizedBox(height: 16),
                      TextField(
                        controller: _noteCtrl,
                        maxLines: 4,
                        maxLength: 2000,
                        decoration: const InputDecoration(
                          labelText: 'ملاحظة المراجعة *',
                          alignLabelWithHint: true,
                          border: OutlineInputBorder(),
                        ),
                        style: const TextStyle(fontFamily: 'Cairo'),
                      ),
                      const SizedBox(height: 12),
                      SizedBox(
                        width: double.infinity,
                        child: FilledButton(
                          onPressed: _submitting ? null : _submitReview,
                          child: _submitting
                              ? const SizedBox(
                                  height: 20,
                                  width: 20,
                                  child: CircularProgressIndicator(
                                      strokeWidth: 2, color: Colors.white),
                                )
                              : const Text('تمت المراجعة',
                                  style: TextStyle(fontFamily: 'Cairo')),
                        ),
                      ),
                    ],
                  ),
      ),
    );
  }
}
