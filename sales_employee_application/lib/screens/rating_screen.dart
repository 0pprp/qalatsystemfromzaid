import 'package:flutter/material.dart';
import 'package:sales_employee_application/services/api_client.dart';
import 'package:sales_employee_application/services/app_state.dart';
import 'package:sales_employee_application/utils/app_theme.dart';

class RatingScreen extends StatefulWidget {
  const RatingScreen({super.key});

  @override
  State<RatingScreen> createState() => _RatingScreenState();
}

class _RatingScreenState extends State<RatingScreen> {
  int _level = 3;
  final _notes = TextEditingController();
  final _reason = TextEditingController();
  bool _saving = false;

  static const labels = {
    1: '1 — مرفوض (سبب إلزامي، لا بيع)',
    2: '2 — مقبول (سعر البيع ×2)',
    3: '3 — عادي + ملاحظة',
    4: '4 — عادي + ملاحظة',
    5: '5 — عادي + ملاحظة',
  };

  @override
  void initState() {
    super.initState();
    AppState.instance.addListener(_onCustomer);
  }

  @override
  void dispose() {
    AppState.instance.removeListener(_onCustomer);
    _notes.dispose();
    _reason.dispose();
    super.dispose();
  }

  void _onCustomer() {
    if (mounted) setState(() {});
  }

  Future<void> _save() async {
    if (_level == 1 && _reason.text.trim().isEmpty) {
      _toast('سبب الرفض إلزامي');
      return;
    }
    final customer = AppState.instance.selectedCustomer;
    setState(() => _saving = true);
    try {
      await ApiClient.post('Ratings', body: {
        'customerID': customer?['customerId'] ?? customer?['customerID'],
        'customerName': customer?['customerName'] ?? '',
        'phoneNumber': customer?['phoneNumber'] ?? '',
        'ratingLevel': _level,
        'notes': _notes.text.trim(),
        'rejectionReason': _reason.text.trim(),
      });
      AppState.instance.setRating(
        level: _level,
        notes: _notes.text.trim(),
        reason: _reason.text.trim(),
      );
      if (!mounted) return;
      _toast(_level == 1
          ? 'تم حفظ الرفض — لا يمكن إنشاء مبيع'
          : _level == 2
              ? 'تم التقييم — الأسعار ستُضاعف في المبيع والعقد والوصل'
              : 'تم حفظ التقييم');
      if (_level != 1) {
        AppState.instance.goTo(2);
      }
    } on ApiException catch (e) {
      _toast(e.message);
    } catch (_) {
      _toast('تعذر حفظ التقييم');
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  void _toast(String message) {
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(message)));
  }

  @override
  Widget build(BuildContext context) {
    final customer = AppState.instance.selectedCustomer;
    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        if (customer != null)
          Card(
            child: ListTile(
              title: Text('${customer['customerName'] ?? ''}'),
              subtitle: Text('${customer['phoneNumber'] ?? ''} — ${customer['cityName'] ?? ''}'),
            ),
          )
        else
          const Text('اختر زبوناً من البحث أو اترك التقييم لمبيع جديد.'),
        const SizedBox(height: 12),
        RadioGroup<int>(
          groupValue: _level,
          onChanged: (v) => setState(() => _level = v ?? 3),
          child: Column(
            children: [
              for (final e in labels.entries)
                RadioListTile<int>(
                  value: e.key,
                  title: Text(e.value),
                  activeColor: AppTheme.primaryColor,
                ),
            ],
          ),
        ),
        TextField(
          controller: _notes,
          maxLines: 3,
          decoration: const InputDecoration(hintText: 'ملاحظة (كل المستويات)'),
        ),
        if (_level == 1) ...[
          const SizedBox(height: 12),
          TextField(
            controller: _reason,
            maxLines: 3,
            decoration: const InputDecoration(hintText: 'سبب الرفض *'),
          ),
        ],
        const SizedBox(height: 20),
        ElevatedButton(
          onPressed: _saving ? null : _save,
          child: _saving
              ? const CircularProgressIndicator(color: Colors.white)
              : const Text('حفظ التقييم'),
        ),
      ],
    );
  }
}
