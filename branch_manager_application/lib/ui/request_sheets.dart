import 'package:branch_manager_application/core/api.dart';
import 'package:branch_manager_application/theme/bm_theme.dart';
import 'package:flutter/material.dart';

class SalesRequestSheet extends StatefulWidget {
  const SalesRequestSheet({super.key});

  static Future<void> show(BuildContext context) {
    return showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.white,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(22)),
      ),
      builder: (_) => const SalesRequestSheet(),
    );
  }

  @override
  State<SalesRequestSheet> createState() => _SalesRequestSheetState();
}

class _SalesRequestSheetState extends State<SalesRequestSheet> {
  final _name = TextEditingController();
  final _phone = TextEditingController();
  final _address = TextEditingController();
  final _notes = TextEditingController();
  bool _saving = false;
  String? _error;

  @override
  void dispose() {
    _name.dispose();
    _phone.dispose();
    _address.dispose();
    _notes.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (_saving) return;
    final name = _name.text.trim();
    final phone = _phone.text.trim();
    if (name.isEmpty || phone.isEmpty) {
      setState(() => _error = 'الاسم والهاتف مطلوبان');
      return;
    }
    setState(() {
      _saving = true;
      _error = null;
    });
    try {
      await BmApi.instance.createSalesRequest(
        fullName: name,
        phone: phone,
        address: _address.text.trim().isEmpty ? null : _address.text.trim(),
        notes: _notes.text.trim().isEmpty ? null : _notes.text.trim(),
      );
      if (!mounted) return;
      Navigator.pop(context);
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('تم إرسال طلب المبيع إلى مدير المبيعات')),
      );
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _saving = false;
        _error = '$e';
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    return Directionality(
      textDirection: TextDirection.rtl,
      child: Padding(
        padding: EdgeInsets.only(
          left: 20,
          right: 20,
          top: 16,
          bottom: MediaQuery.of(context).viewInsets.bottom + 20,
        ),
        child: SingleChildScrollView(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            mainAxisSize: MainAxisSize.min,
            children: [
              Center(
                child: Container(
                  width: 42,
                  height: 4,
                  decoration: BoxDecoration(
                    color: Colors.black26,
                    borderRadius: BorderRadius.circular(4),
                  ),
                ),
              ),
              const SizedBox(height: 14),
              const Text(
                'طلب مبيع جديد → مدير المبيعات',
                style: TextStyle(
                  fontFamily: 'Cairo',
                  fontWeight: FontWeight.bold,
                  fontSize: 16,
                ),
              ),
              const SizedBox(height: 12),
              TextField(
                controller: _name,
                decoration: const InputDecoration(labelText: 'اسم الزبون'),
              ),
              const SizedBox(height: 10),
              TextField(
                controller: _phone,
                keyboardType: TextInputType.phone,
                decoration: const InputDecoration(labelText: 'الهاتف'),
              ),
              const SizedBox(height: 10),
              TextField(
                controller: _address,
                decoration: const InputDecoration(labelText: 'العنوان (اختياري)'),
              ),
              const SizedBox(height: 10),
              TextField(
                controller: _notes,
                minLines: 2,
                maxLines: 4,
                decoration: const InputDecoration(labelText: 'ملاحظات (اختياري)'),
              ),
              if (_error != null) ...[
                const SizedBox(height: 8),
                Text(_error!,
                    style: const TextStyle(fontFamily: 'Cairo', color: BmTheme.danger)),
              ],
              const SizedBox(height: 14),
              FilledButton(
                onPressed: _saving ? null : _submit,
                child: Text(_saving ? 'جارٍ الإرسال…' : 'إرسال الطلب'),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class DelegatedManagerRequestSheet extends StatefulWidget {
  const DelegatedManagerRequestSheet({super.key});

  static Future<void> show(BuildContext context) {
    return showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.white,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(22)),
      ),
      builder: (_) => const DelegatedManagerRequestSheet(),
    );
  }

  @override
  State<DelegatedManagerRequestSheet> createState() =>
      _DelegatedManagerRequestSheetState();
}

class _DelegatedManagerRequestSheetState
    extends State<DelegatedManagerRequestSheet> {
  final _ctrl = TextEditingController();
  bool _saving = false;
  String? _error;

  @override
  void dispose() {
    _ctrl.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (_saving) return;
    final text = _ctrl.text.trim();
    if (text.length < 10) {
      setState(() => _error = 'اكتب 10 أحرف على الأقل');
      return;
    }
    setState(() {
      _saving = true;
      _error = null;
    });
    try {
      await BmApi.instance.createDelegatedManagerRequest(message: text);
      if (!mounted) return;
      Navigator.pop(context);
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('تم إرسال الطلب إلى المدير المفوض')),
      );
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _saving = false;
        _error = '$e';
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    return Directionality(
      textDirection: TextDirection.rtl,
      child: Padding(
        padding: EdgeInsets.only(
          left: 20,
          right: 20,
          top: 16,
          bottom: MediaQuery.of(context).viewInsets.bottom + 20,
        ),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            const Text(
              'طلب من المدير المفوض',
              style: TextStyle(
                fontFamily: 'Cairo',
                fontWeight: FontWeight.bold,
                fontSize: 16,
              ),
            ),
            const SizedBox(height: 8),
            const Text(
              'يصل لنفس صندوق المدير المفوض (بدون تسميته شكوى).',
              style: TextStyle(fontFamily: 'Cairo', fontSize: 12, color: BmTheme.muted),
            ),
            const SizedBox(height: 12),
            TextField(
              controller: _ctrl,
              minLines: 4,
              maxLines: 7,
              decoration: const InputDecoration(hintText: 'اكتب طلبك هنا…'),
            ),
            if (_error != null) ...[
              const SizedBox(height: 8),
              Text(_error!,
                  style: const TextStyle(fontFamily: 'Cairo', color: BmTheme.danger)),
            ],
            const SizedBox(height: 14),
            FilledButton(
              onPressed: _saving ? null : _submit,
              child: Text(_saving ? 'جارٍ الإرسال…' : 'إرسال'),
            ),
          ],
        ),
      ),
    );
  }
}
