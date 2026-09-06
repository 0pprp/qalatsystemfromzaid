import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:sales_employee_application/data/sales_repository_factory.dart';
import 'package:sales_employee_application/services/api_client.dart';
import 'package:sales_employee_application/services/session.dart';
import 'package:sales_employee_application/utils/app_theme.dart';
import 'package:sales_employee_application/utils/iraq_phone.dart';

class SubmitSalesRequestScreen extends StatefulWidget {
  const SubmitSalesRequestScreen({super.key});

  @override
  State<SubmitSalesRequestScreen> createState() => _SubmitSalesRequestScreenState();
}

class _SubmitSalesRequestScreenState extends State<SubmitSalesRequestScreen> {
  final _form = GlobalKey<FormState>();
  final _name = TextEditingController();
  final _phone = TextEditingController();
  final _province = TextEditingController();
  final _address = TextEditingController();
  final _notes = TextEditingController();
  bool _busy = false;

  @override
  void initState() {
    super.initState();
    _province.text = Session.cityName;
  }

  @override
  void dispose() {
    _name.dispose();
    _phone.dispose();
    _province.dispose();
    _address.dispose();
    _notes.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!(_form.currentState?.validate() ?? false)) return;
    setState(() => _busy = true);
    try {
      await SalesRepositoryFactory.instance.submitSalesRequest(
        fullName: _name.text.trim(),
        phone: IraqPhone.normalize(_phone.text),
        province: _province.text.trim().isEmpty ? Session.cityName : _province.text.trim(),
        address: _address.text.trim(),
        notes: _notes.text.trim(),
      );
      if (!mounted) return;
      await showDialog<void>(
        context: context,
        builder: (ctx) => AlertDialog(
          title: const Text('تم الإرسال'),
          content: const Text('تم إرسال طلب البيع إلى مدير المبيعات.'),
          actions: [
            TextButton(onPressed: () => Navigator.pop(ctx), child: const Text('موافق')),
          ],
        ),
      );
      if (mounted) Navigator.pop(context, true);
    } on ApiException catch (e) {
      if (mounted) _alert(e.message);
    } catch (_) {
      if (mounted) _alert('تعذر إرسال طلب البيع');
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  void _alert(String message) {
    showDialog<void>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('تنبيه'),
        content: Text(message),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx), child: const Text('موافق')),
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('إرسال طلب بيع')),
      body: Form(
        key: _form,
        child: ListView(
          padding: const EdgeInsets.all(AppSpacing.md),
          children: [
            TextFormField(
              controller: _name,
              textInputAction: TextInputAction.next,
              decoration: const InputDecoration(labelText: 'اسم الزبون *'),
              validator: (v) => (v == null || v.trim().isEmpty) ? 'مطلوب' : null,
            ),
            const SizedBox(height: AppSpacing.md),
            TextFormField(
              controller: _phone,
              keyboardType: TextInputType.phone,
              textInputAction: TextInputAction.next,
              inputFormatters: [
                FilteringTextInputFormatter.digitsOnly,
                LengthLimitingTextInputFormatter(11),
              ],
              decoration: const InputDecoration(
                labelText: 'رقم الهاتف *',
                helperText: '11 رقم ويبدأ بـ 07',
              ),
              validator: IraqPhone.validator,
            ),
            const SizedBox(height: AppSpacing.md),
            TextFormField(
              controller: _province,
              textInputAction: TextInputAction.next,
              decoration: const InputDecoration(labelText: 'المحافظة'),
            ),
            const SizedBox(height: AppSpacing.md),
            TextFormField(
              controller: _address,
              textInputAction: TextInputAction.next,
              decoration: const InputDecoration(labelText: 'العنوان *'),
              validator: (v) => (v == null || v.trim().isEmpty) ? 'مطلوب' : null,
            ),
            const SizedBox(height: AppSpacing.md),
            TextFormField(
              controller: _notes,
              minLines: 3,
              maxLines: 5,
              decoration: const InputDecoration(labelText: 'ملاحظات (اختياري)'),
            ),
            const SizedBox(height: AppSpacing.lg),
            SizedBox(
              height: 52,
              child: ElevatedButton(
                onPressed: _busy ? null : _submit,
                child: _busy
                    ? const SizedBox(
                        width: 22,
                        height: 22,
                        child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white),
                      )
                    : const Text('إرسال طلب بيع'),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
