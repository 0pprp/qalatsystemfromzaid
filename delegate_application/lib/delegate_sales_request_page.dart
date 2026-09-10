import 'dart:convert';

import 'package:delegate_application/ui/app_safe_scaffold.dart';
import 'package:delegate_application/utils/AppTheme.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';

/// Same fields as follower_application FollowerSalesRequestPage.
/// [saleRequestType] is fixed by entry point: New (Home) or Old (customer card).
class DelegateSalesRequestPage extends StatefulWidget {
  const DelegateSalesRequestPage({super.key});

  @override
  State<DelegateSalesRequestPage> createState() =>
      _DelegateSalesRequestPageState();
}

class _DelegateSalesRequestPageState extends State<DelegateSalesRequestPage> {
  final _form = GlobalKey<FormState>();
  final _name = TextEditingController();
  final _phone = TextEditingController();
  final _address = TextEditingController();
  final _notes = TextEditingController();
  bool _saving = false;
  bool _ready = false;
  Map<String, dynamic>? _customer;
  String _saleRequestType = 'New';

  bool get _isNew => _saleRequestType == 'New';
  bool get _isOld => _saleRequestType == 'Old';

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    if (_ready) return;
    final args = ModalRoute.of(context)?.settings.arguments;
    if (args is Map) {
      _saleRequestType =
          '${args['saleRequestType'] ?? args['SaleRequestType'] ?? 'New'}';
      if (_saleRequestType != 'Old' && _saleRequestType != 'New') {
        _saleRequestType = 'New';
      }
      final c = args['customer'];
      if (c is Map) {
        _customer = Map<String, dynamic>.from(c);
        _name.text =
            '${_customer!['customerName'] ?? _customer!['CustomerName'] ?? _customer!['name'] ?? ''}';
        _phone.text =
            '${_customer!['phoneNumber'] ?? _customer!['PhoneNumber'] ?? _customer!['phone'] ?? ''}';
        _address.text =
            '${_customer!['address'] ?? _customer!['Address'] ?? ''}';
      }
    }
    _ready = true;
  }

  @override
  void dispose() {
    _name.dispose();
    _phone.dispose();
    _address.dispose();
    _notes.dispose();
    super.dispose();
  }

  bool _validPhone(String phone) {
    final p = phone.trim().replaceAll(RegExp(r'\s+'), '');
    return RegExp(r'^07\d{9}$').hasMatch(p);
  }

  Future<void> _submit() async {
    if (_saving) return;
    if (_isNew && !(_form.currentState?.validate() ?? false)) return;

    if (_isOld) {
      final id =
          int.tryParse('${_customer?['customerId'] ?? _customer?['CustomerId'] ?? 0}') ??
              0;
      if (id <= 0) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('معرف الزبون مفقود', style: TextStyle(fontFamily: 'Cairo')),
          ),
        );
        return;
      }
    } else {
      final phone = _phone.text.trim().replaceAll(RegExp(r'\s+'), '');
      if (!_validPhone(phone)) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('رقم الهاتف يجب أن يكون 11 رقماً ويبدأ بـ 07',
                style: TextStyle(fontFamily: 'Cairo')),
          ),
        );
        return;
      }
    }

    setState(() => _saving = true);
    try {
      final prefs = await SharedPreferences.getInstance();
      final asyncId = prefs.getString('AsyncId') ?? '';
      var link = prefs.getString('LinkDelegate') ?? '';
      if (!link.endsWith('/')) link = '$link/';
      if (!link.endsWith('api/') && !link.contains('/api')) {
        // keep as stored
      }
      final customerId = int.tryParse(
              '${_customer?['customerId'] ?? _customer?['CustomerId'] ?? 0}') ??
          0;
      final uri = Uri.parse('${link}Delegates/SalesRequests');
      final body = <String, dynamic>{
        'asyncId': asyncId,
        'saleRequestType': _saleRequestType,
        'notes': _notes.text.trim(),
        if (_isOld && customerId > 0) 'customerId': customerId,
        if (_isNew) ...{
          'fullName': _name.text.trim(),
          'phone': _phone.text.trim().replaceAll(RegExp(r'\s+'), ''),
          'address': _address.text.trim(),
        },
      };
      final response = await http
          .post(
            uri,
            headers: {'Content-Type': 'application/json'},
            body: json.encode(body),
          )
          .timeout(const Duration(seconds: 30));
      if (!mounted) return;
      if (response.statusCode == 200) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('تم إرسال طلب المبيع إلى مدير المبيعات',
                style: TextStyle(fontFamily: 'Cairo')),
          ),
        );
        Navigator.pop(context);
      } else {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(
              _extractApiMessage(response.body) ??
                  'تعذر الإرسال (HTTP ${response.statusCode})',
              style: const TextStyle(fontFamily: 'Cairo'),
            ),
          ),
        );
      }
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content:
                Text('تعذر إرسال الطلب', style: TextStyle(fontFamily: 'Cairo')),
          ),
        );
      }
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  String? _extractApiMessage(String body) {
    try {
      final decoded = json.decode(body);
      if (decoded is Map) {
        final msg =
            decoded['message'] ?? decoded['Message'] ?? decoded['title'];
        if (msg != null && '$msg'.trim().isNotEmpty) return '$msg'.trim();
      }
    } catch (_) {}
    return null;
  }

  @override
  Widget build(BuildContext context) {
    return Directionality(
      textDirection: TextDirection.rtl,
      child: AppSafeScaffold(
        appBar: AppBar(
          title: Text(
            _isOld ? 'طلب مبيع' : 'طلب مبيع جديد',
            style: const TextStyle(fontFamily: 'Cairo'),
          ),
          backgroundColor: AppTheme.primaryColor,
        ),
        body: Form(
          key: _form,
          child: ListView(
            padding: AppInsets.scrollPadding(context,
                horizontal: 16, top: 16, extraBottom: 24),
            children: [
              Text(
                _isOld
                    ? 'مبيع قديم — بيانات الزبون تُرسل تلقائياً من بطاقته. المحافظة من بيانات الزبون/المندوب.'
                    : 'مبيع جديد — المحافظة تُحدد تلقائياً من حساب المندوب. لن يُنشأ الزبون في Customers الآن.',
                style: const TextStyle(fontFamily: 'Cairo', color: Colors.grey),
              ),
              const SizedBox(height: 16),
              if (_isOld) ...[
                _ro('اسم الزبون', _name.text),
                _ro('الهاتف', _phone.text.isEmpty ? '—' : _phone.text),
                _ro('العنوان', _address.text.isEmpty ? '—' : _address.text),
              ] else ...[
                TextFormField(
                  controller: _name,
                  decoration: const InputDecoration(
                    labelText: 'اسم الزبون *',
                    labelStyle: TextStyle(fontFamily: 'Cairo'),
                  ),
                  validator: (v) =>
                      (v == null || v.trim().isEmpty) ? 'مطلوب' : null,
                ),
                TextFormField(
                  controller: _phone,
                  decoration: const InputDecoration(
                    labelText: 'الهاتف * (11 رقم يبدأ بـ 07)',
                    labelStyle: TextStyle(fontFamily: 'Cairo'),
                  ),
                  keyboardType: TextInputType.phone,
                  inputFormatters: [
                    FilteringTextInputFormatter.digitsOnly,
                    LengthLimitingTextInputFormatter(11),
                  ],
                  validator: (v) {
                    final p = (v ?? '').trim().replaceAll(RegExp(r'\s+'), '');
                    if (!_validPhone(p)) {
                      return 'رقم الهاتف يجب أن يكون 11 رقماً ويبدأ بـ 07';
                    }
                    return null;
                  },
                ),
                TextFormField(
                  controller: _address,
                  decoration: const InputDecoration(
                    labelText: 'العنوان *',
                    labelStyle: TextStyle(fontFamily: 'Cairo'),
                  ),
                  validator: (v) =>
                      (v == null || v.trim().isEmpty) ? 'مطلوب' : null,
                ),
              ],
              TextFormField(
                controller: _notes,
                maxLines: 3,
                decoration: const InputDecoration(
                  labelText: 'ملاحظات (اختياري)',
                  labelStyle: TextStyle(fontFamily: 'Cairo'),
                ),
              ),
              const SizedBox(height: 20),
              ElevatedButton(
                onPressed: _saving ? null : _submit,
                style: ElevatedButton.styleFrom(
                  backgroundColor: AppTheme.primaryColor,
                  minimumSize: const Size.fromHeight(48),
                ),
                child: Text(
                  _saving ? '...' : 'إرسال إلى مدير المبيعات',
                  style: const TextStyle(fontFamily: 'Cairo', color: Colors.white),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _ro(String label, String value) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text(label,
              style: const TextStyle(
                  fontFamily: 'Cairo', color: Colors.grey, fontSize: 12)),
          Text(value.isEmpty ? '—' : value,
              style: const TextStyle(
                  fontFamily: 'Cairo', fontWeight: FontWeight.bold)),
        ],
      ),
    );
  }
}
