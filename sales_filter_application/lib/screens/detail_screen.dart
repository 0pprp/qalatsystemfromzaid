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
    final noteCtrl = TextEditingController();
    final note = await showModalBottomSheet<String>(
      context: context,
      isScrollControlled: true,
      builder: (ctx) => Padding(
        padding: EdgeInsets.only(bottom: MediaQuery.of(ctx).viewInsets.bottom, left: 16, right: 16, top: 16),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            const Text('ملاحظة التعليق *', style: TextStyle(fontFamily: 'Cairo', fontWeight: FontWeight.bold, fontSize: 16)),
            const SizedBox(height: 8),
            TextField(
              controller: noteCtrl,
              maxLines: 4,
              maxLength: 1000,
              decoration: const InputDecoration(border: OutlineInputBorder(), hintText: 'اكتب سبب/ملاحظة التعليق'),
            ),
            const SizedBox(height: 8),
            ElevatedButton(
              onPressed: () {
                final t = noteCtrl.text.trim();
                if (t.isEmpty) {
                  ScaffoldMessenger.of(ctx).showSnackBar(
                    const SnackBar(content: Text('ملاحظة التعليق مطلوبة', style: TextStyle(fontFamily: 'Cairo'))),
                  );
                  return;
                }
                Navigator.pop(ctx, t);
              },
              style: ElevatedButton.styleFrom(backgroundColor: AppColors.holdAmber, foregroundColor: Colors.white),
              child: const Text('تأكيد التعليق', style: TextStyle(fontFamily: 'Cairo', fontWeight: FontWeight.bold)),
            ),
            const SizedBox(height: 16),
          ],
        ),
      ),
    );
    if (note == null) return;
    if (note.trim().isEmpty) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('ملاحظة التعليق مطلوبة', style: TextStyle(fontFamily: 'Cairo'))),
      );
      return;
    }
    await _run(() => widget.repository.hold(widget.cityValue, widget.requestId, note: note.trim()));
  }

  Future<void> _ready() async {
    final ok = await _confirm('هل تريد تحويل الطلب إلى جاهز للبيع؟ بعد التأكيد سيظهر لموظف المبيعات المختص.');
    if (ok != true) return;
    await _run(() => widget.repository.ready(widget.cityValue, widget.requestId));
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
            const Text('سبب الرفض *', style: TextStyle(fontFamily: 'Cairo', fontWeight: FontWeight.bold, fontSize: 16)),
            const SizedBox(height: 8),
            TextField(controller: reasonCtrl, maxLines: 4, maxLength: 400, decoration: const InputDecoration(border: OutlineInputBorder())),
            const SizedBox(height: 8),
            ElevatedButton(
              onPressed: () {
                final t = reasonCtrl.text.trim();
                if (t.isEmpty) {
                  ScaffoldMessenger.of(ctx).showSnackBar(
                    const SnackBar(content: Text('سبب الرفض مطلوب', style: TextStyle(fontFamily: 'Cairo'))),
                  );
                  return;
                }
                Navigator.pop(ctx, t);
              },
              style: ElevatedButton.styleFrom(backgroundColor: AppColors.danger, foregroundColor: Colors.white),
              child: const Text('تأكيد الرفض', style: TextStyle(fontFamily: 'Cairo', fontWeight: FontWeight.bold)),
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
    await _run(() => widget.repository.reject(widget.cityValue, widget.requestId, reason: reason));
  }

  Future<bool?> _confirm(String message) {
    return showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        content: Text(message, style: const TextStyle(fontFamily: 'Cairo')),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx, false), child: const Text('إلغاء', style: TextStyle(fontFamily: 'Cairo'))),
          TextButton(onPressed: () => Navigator.pop(ctx, true), child: const Text('تأكيد', style: TextStyle(fontFamily: 'Cairo'))),
        ],
      ),
    );
  }

  Future<void> _run(Future<FilterRequest> Function() action) async {
    setState(() => _busy = true);
    try {
      await action();
      if (!mounted) return;
      Navigator.of(context).pop(true);
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(e.toString(), style: const TextStyle(fontFamily: 'Cairo'))));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _dial() async {
    final phone = _row?.customerPhone ?? '';
    final cleaned = phone.replaceAll(RegExp(r'[^\d+]'), '');
    if (cleaned.isEmpty) return;
    final ok = await widget.dialer(cleaned);
    if (!ok && mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('تعذر فتح تطبيق الاتصال', style: TextStyle(fontFamily: 'Cairo'))),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final row = _row;
    final pending = row?.filterStatus == FilterStatuses.pending;
    final onHold = row?.filterStatus == FilterStatuses.onHold;

    return Directionality(
      textDirection: TextDirection.rtl,
      child: Scaffold(
        appBar: AppBar(title: const Text('تفاصيل الطلب')),
        body: SafeArea(
          child: _loading
              ? const Center(child: CircularProgressIndicator())
              : _error != null
                  ? Center(child: Text(_error!, style: const TextStyle(fontFamily: 'Cairo', color: AppColors.danger)))
                  : row == null
                      ? const SizedBox.shrink()
                      : ListView(
                          padding: const EdgeInsets.all(AppSpacing.md),
                          children: [
                            _line('اسم الزبون', row.customerName),
                            _line('رقم الهاتف', row.customerPhone ?? '—'),
                            _line('المحافظة', row.cityName ?? row.customerProvince ?? row.cityValue ?? '—'),
                            _line('العنوان', row.customerAddress ?? '—'),
                            _line('نوع المبيع', row.wantedDescription ?? '—'),
                            if (onHold && (row.filterNote ?? '').trim().isNotEmpty) _line('ملاحظة التعليق', row.filterNote!),
                            if (row.filterStatus == FilterStatuses.rejected && (row.rejectReason ?? '').trim().isNotEmpty)
                              _line('سبب الرفض', row.rejectReason!),
                            const SizedBox(height: AppSpacing.md),
                            ElevatedButton.icon(
                              onPressed: _busy ? null : _dial,
                              icon: const Icon(Icons.phone),
                              label: const Text('اتصال', style: TextStyle(fontFamily: 'Cairo', fontWeight: FontWeight.bold)),
                              style: ElevatedButton.styleFrom(
                                backgroundColor: AppColors.darkGreen,
                                foregroundColor: Colors.white,
                                padding: const EdgeInsets.symmetric(vertical: 14),
                              ),
                            ),
                            const SizedBox(height: AppSpacing.lg),
                            if (pending || onHold) ...[
                              if (pending)
                                ElevatedButton(
                                  onPressed: _busy ? null : _hold,
                                  style: ElevatedButton.styleFrom(backgroundColor: AppColors.holdAmber, foregroundColor: Colors.white),
                                  child: const Text('معلق', style: TextStyle(fontFamily: 'Cairo', fontWeight: FontWeight.bold)),
                                ),
                              const SizedBox(height: 8),
                              ElevatedButton(
                                onPressed: _busy ? null : _ready,
                                style: ElevatedButton.styleFrom(backgroundColor: AppColors.readyGreen, foregroundColor: Colors.white),
                                child: const Text('جاهز للبيع', style: TextStyle(fontFamily: 'Cairo', fontWeight: FontWeight.bold)),
                              ),
                              const SizedBox(height: 8),
                              ElevatedButton(
                                onPressed: _busy ? null : _reject,
                                style: ElevatedButton.styleFrom(backgroundColor: AppColors.danger, foregroundColor: Colors.white),
                                child: const Text('مرفوض', style: TextStyle(fontFamily: 'Cairo', fontWeight: FontWeight.bold)),
                              ),
                            ],
                          ],
                        ),
        ),
      ),
    );
  }

  Widget _line(String label, String value) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(label, style: const TextStyle(fontFamily: 'Cairo', color: AppColors.muted, fontWeight: FontWeight.w700, fontSize: 13)),
          const SizedBox(height: 2),
          Text(value, style: const TextStyle(fontFamily: 'Cairo', fontSize: 16, fontWeight: FontWeight.w600, color: AppColors.text)),
        ],
      ),
    );
  }
}
