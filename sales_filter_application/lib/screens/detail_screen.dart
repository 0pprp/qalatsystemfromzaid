import 'package:flutter/material.dart';
import 'package:sales_filter_application/models/filter_models.dart';
import 'package:sales_filter_application/services/filter_repository.dart';
import 'package:sales_filter_application/theme/app_theme.dart';

class DetailScreen extends StatefulWidget {
  const DetailScreen({
    super.key,
    required this.requestId,
    required this.cityValue,
    required this.repository,
    required this.dialer,
  });

  final int requestId;
  final String cityValue;
  final FilterRepository repository;
  final Future<bool> Function(String tel) dialer;

  @override
  State<DetailScreen> createState() => _DetailScreenState();
}

class _DetailScreenState extends State<DetailScreen> {
  FilterRequest? _row;
  bool _loading = true;
  bool _busy = false;
  String? _error;
  final _note = TextEditingController();

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    _note.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final row = await widget.repository.get(widget.cityValue, widget.requestId);
      if (!mounted) return;
      setState(() {
        _row = row;
        _loading = false;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _loading = false;
        _error = e.toString();
      });
    }
  }

  Future<void> _hold() async {
    final ok = await _confirm('تعليق الطلب؟');
    if (ok != true) return;
    await _run(() => widget.repository.hold(widget.cityValue, widget.requestId, note: _note.text.trim().isEmpty ? null : _note.text.trim()));
  }

  Future<void> _ready() async {
    final ok = await _confirm('هل تريد تحويل الطلب إلى جاهز للبيع؟ بعد ذلك سيظهر لموظف المبيعات المختص.');
    if (ok != true) return;
    await _run(() => widget.repository.ready(widget.cityValue, widget.requestId, note: _note.text.trim().isEmpty ? null : _note.text.trim()));
  }

  Future<void> _reject() async {
    final reasonCtrl = TextEditingController();
    final reason = await showModalBottomSheet<String>(
      context: context,
      isScrollControlled: true,
      builder: (ctx) => Padding(
        padding: EdgeInsets.only(bottom: MediaQuery.of(ctx).viewInsets.bottom, left: 16, right: 16, top: 16),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            const Text('سبب الرفض *', style: TextStyle(fontFamily: 'Cairo', fontWeight: FontWeight.bold)),
            const SizedBox(height: 8),
            TextField(controller: reasonCtrl, maxLines: 4, decoration: const InputDecoration(border: OutlineInputBorder())),
            const SizedBox(height: 12),
            ElevatedButton(
              onPressed: () => Navigator.pop(ctx, reasonCtrl.text.trim()),
              style: ElevatedButton.styleFrom(backgroundColor: Colors.red.shade700, foregroundColor: Colors.white),
              child: const Text('تأكيد الرفض', style: TextStyle(fontFamily: 'Cairo')),
            ),
            const SizedBox(height: 16),
          ],
        ),
      ),
    );
    if (reason == null) return;
    if (reason.trim().isEmpty) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('سبب الرفض مطلوب', style: TextStyle(fontFamily: 'Cairo'))));
      return;
    }
    await _run(() => widget.repository.reject(
          widget.cityValue,
          widget.requestId,
          reason: reason,
          note: _note.text.trim().isEmpty ? null : _note.text.trim(),
        ));
  }

  Future<bool?> _confirm(String message) {
    return showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        content: Text(message, style: const TextStyle(fontFamily: 'Cairo')),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx, false), child: const Text('إلغاء')),
          ElevatedButton(onPressed: () => Navigator.pop(ctx, true), child: const Text('تأكيد')),
        ],
      ),
    );
  }

  Future<void> _run(Future<FilterRequest> Function() action) async {
    setState(() => _busy = true);
    try {
      await action();
      if (!mounted) return;
      Navigator.pop(context, true);
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(e.toString(), style: const TextStyle(fontFamily: 'Cairo'))));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final row = _row;
    return Directionality(
      textDirection: TextDirection.rtl,
      child: Scaffold(
        appBar: AppBar(title: const Text('تفاصيل الطلب', style: TextStyle(fontFamily: 'Cairo'))),
        body: SafeArea(
          child: _loading
              ? const Center(child: CircularProgressIndicator())
              : _error != null
                  ? Center(child: Text(_error!, style: const TextStyle(fontFamily: 'Cairo', color: Colors.red)))
                  : row == null
                      ? const SizedBox.shrink()
                      : ListView(
                          padding: const EdgeInsets.all(16),
                          children: [
                            _line('اسم الزبون', row.customerName),
                            _phone(row.customerPhone),
                            _line('المحافظة', row.cityName ?? row.customerProvince ?? row.cityValue ?? '—'),
                            _line('العنوان', row.customerAddress ?? '—'),
                            _line('شنو يريد', row.wantedDescription ?? '—'),
                            const SizedBox(height: 12),
                            TextField(
                              controller: _note,
                              maxLines: 3,
                              decoration: const InputDecoration(
                                labelText: 'ملاحظات (اختياري)',
                                border: OutlineInputBorder(),
                              ),
                            ),
                            const SizedBox(height: 16),
                            if (row.filterStatus == FilterStatuses.pending || row.filterStatus == FilterStatuses.onHold) ...[
                              Row(
                                children: [
                                  Expanded(
                                    child: OutlinedButton(
                                      onPressed: _busy ? null : _hold,
                                      child: const Text('معلق', style: TextStyle(fontFamily: 'Cairo')),
                                    ),
                                  ),
                                  const SizedBox(width: 8),
                                  Expanded(
                                    child: ElevatedButton(
                                      onPressed: _busy ? null : _ready,
                                      style: ElevatedButton.styleFrom(backgroundColor: AppTheme.primary, foregroundColor: Colors.white),
                                      child: const Text('جاهز للبيع', style: TextStyle(fontFamily: 'Cairo')),
                                    ),
                                  ),
                                  const SizedBox(width: 8),
                                  Expanded(
                                    child: ElevatedButton(
                                      onPressed: _busy ? null : _reject,
                                      style: ElevatedButton.styleFrom(backgroundColor: Colors.red.shade700, foregroundColor: Colors.white),
                                      child: const Text('مرفوض', style: TextStyle(fontFamily: 'Cairo')),
                                    ),
                                  ),
                                ],
                              ),
                            ],
                          ],
                        ),
        ),
      ),
    );
  }

  Widget _line(String k, String v) => Padding(
        padding: const EdgeInsets.only(bottom: 8),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            SizedBox(width: 100, child: Text(k, style: const TextStyle(fontFamily: 'Cairo', color: Colors.grey))),
            Expanded(child: Text(v, style: const TextStyle(fontFamily: 'Cairo'))),
          ],
        ),
      );

  Widget _phone(String? phone) {
    if (phone == null || phone.trim().isEmpty) return _line('الهاتف', '—');
    return Padding(
      padding: const EdgeInsets.only(bottom: 8),
      child: Row(
        children: [
          const SizedBox(width: 100, child: Text('الهاتف', style: TextStyle(fontFamily: 'Cairo', color: Colors.grey))),
          Expanded(
            child: InkWell(
              onTap: () async {
                final cleaned = phone.replaceAll(RegExp(r'[^\d+]'), '');
                final ok = await widget.dialer(cleaned);
                if (!ok && mounted) {
                  ScaffoldMessenger.of(context).showSnackBar(
                    const SnackBar(content: Text('تعذر فتح تطبيق الاتصال', style: TextStyle(fontFamily: 'Cairo'))),
                  );
                }
              },
              child: Text(phone, style: const TextStyle(fontFamily: 'Cairo', color: AppTheme.primary, decoration: TextDecoration.underline)),
            ),
          ),
          IconButton(
            onPressed: () async {
              final cleaned = phone.replaceAll(RegExp(r'[^\d+]'), '');
              await widget.dialer(cleaned);
            },
            icon: const Icon(Icons.phone, color: AppTheme.primary),
          ),
        ],
      ),
    );
  }
}
