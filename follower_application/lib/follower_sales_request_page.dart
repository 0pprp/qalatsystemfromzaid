import 'dart:convert';

import 'package:follower_application/config/app_env.dart';
import 'package:follower_application/services/follower_auth_rules.dart';
import 'package:follower_application/ui/app_safe_scaffold.dart';
import 'package:follower_application/utils/AppTheme.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';

/// Sales request form for follower: existing customer OR brand-new customer.
/// Province is NOT collected — Backend sets it from the authenticated follower.
class FollowerSalesRequestPage extends StatefulWidget {
  const FollowerSalesRequestPage({super.key});

  @override
  State<FollowerSalesRequestPage> createState() => _FollowerSalesRequestPageState();
}

class _FollowerSalesRequestPageState extends State<FollowerSalesRequestPage> {
  final _form = GlobalKey<FormState>();
  final _name = TextEditingController();
  final _phone = TextEditingController();
  final _address = TextEditingController();
  final _notes = TextEditingController();
  bool _saving = false;
  bool _ready = false;
  Map<String, dynamic>? _customer;
  int _listId = 0;
  bool get _isNew => (_customer == null) || ((int.tryParse('${_customer?['customerId'] ?? 0}') ?? 0) <= 0);

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    if (_ready) return;
    final args = ModalRoute.of(context)?.settings.arguments;
    if (args is Map) {
      final c = args['customer'];
      if (c is Map) {
        _customer = Map<String, dynamic>.from(c);
      }
      _listId = int.tryParse('${args['listId'] ?? 0}') ?? 0;
      if (_customer != null) {
        _name.text = '${_customer!['customerName'] ?? _customer!['CustomerName'] ?? ''}';
        _phone.text = '${_customer!['phoneNumber'] ?? _customer!['PhoneNumber'] ?? ''}';
        _address.text = '${_customer!['address'] ?? _customer!['Address'] ?? ''}';
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

  Future<void> _submit() async {
    if (!_form.currentState!.validate() || _saving) return;
    if (_listId <= 0) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('اختر قائمة مسندة أولاً', style: TextStyle(fontFamily: 'Cairo'))),
      );
      return;
    }
    final phone = _phone.text.trim().replaceAll(RegExp(r'\s+'), '');
    if (!FollowerAuthRules.isValidFollowerPhone(phone)) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(
            FollowerAuthRules.phoneValidationMessage(phone) ?? 'رقم الهاتف غير صالح',
            style: const TextStyle(fontFamily: 'Cairo'),
          ),
        ),
      );
      return;
    }
    setState(() => _saving = true);
    try {
      final prefs = await SharedPreferences.getInstance();
      final asyncId = prefs.getString('AsyncId') ?? '';
      final link = AppEnv.apiBase(fallback: prefs.getString('LinkDelegate') ?? '');
      final customerId = int.tryParse('${_customer?['customerId'] ?? _customer?['CustomerId'] ?? 0}') ?? 0;
      final uri = Uri.parse('${link}Followers/SalesRequests');
      final response = await http
          .post(
            uri,
            headers: {'Content-Type': 'application/json'},
            body: json.encode({
              'asyncId': asyncId,
              'listId': _listId,
              if (customerId > 0) 'customerId': customerId,
              'fullName': _name.text.trim(),
              'phone': phone,
              'address': _address.text.trim(),
              'notes': _notes.text.trim(),
            }),
          )
          .timeout(const Duration(seconds: 30));
      if (!mounted) return;
      if (response.statusCode == 200) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('تم إرسال طلب المبيع إلى مدير المبيعات', style: TextStyle(fontFamily: 'Cairo'))),
        );
        Navigator.pop(context);
      } else if (response.statusCode == 404) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('404: مسار Followers/SalesRequests غير موجود على السيرفر', style: TextStyle(fontFamily: 'Cairo'))),
        );
      } else {
        final detail = _extractApiMessage(response.body);
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(
              detail ?? 'تعذر الإرسال (HTTP ${response.statusCode})',
              style: const TextStyle(fontFamily: 'Cairo'),
            ),
          ),
        );
      }
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('تعذر إرسال الطلب', style: TextStyle(fontFamily: 'Cairo'))),
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
        final msg = decoded['message'] ?? decoded['Message'] ?? decoded['title'] ?? decoded['Title'];
        if (msg != null && '$msg'.trim().isNotEmpty) return '$msg'.trim();
      }
    } catch (_) {}
    final trimmed = body.trim();
    if (trimmed.isNotEmpty && trimmed.length < 240 && !trimmed.startsWith('<')) return trimmed;
    return null;
  }

  @override
  Widget build(BuildContext context) {
    return Directionality(
      textDirection: TextDirection.rtl,
      child: AppSafeScaffold(
        appBar: AppBar(
          title: Text(_isNew ? 'طلب مبيع جديد' : 'إرسال طلب مبيع', style: const TextStyle(fontFamily: 'Cairo')),
          backgroundColor: AppTheme.primaryColor,
        ),
        body: Form(
          key: _form,
          child: ListView(
            padding: AppInsets.scrollPadding(context, horizontal: 16, top: 16, extraBottom: 24),
            children: [
              Text(
                _isNew
                    ? 'زبون جديد — لن يُنشأ في Customers الآن. المصدر: المتابع. المحافظة تُحدد تلقائياً من حسابك.'
                    : 'طلب لزبون موجود. المصدر: المتابع. المحافظة تُحدد تلقائياً من حسابك.',
                style: const TextStyle(fontFamily: 'Cairo', color: Colors.grey),
              ),
              const SizedBox(height: 16),
              TextFormField(
                controller: _name,
                decoration: const InputDecoration(labelText: 'اسم الزبون *', labelStyle: TextStyle(fontFamily: 'Cairo')),
                validator: (v) => (v == null || v.trim().isEmpty) ? 'مطلوب' : null,
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
                validator: (v) => FollowerAuthRules.phoneValidationMessage(v),
              ),
              TextFormField(
                controller: _address,
                decoration: const InputDecoration(labelText: 'العنوان *', labelStyle: TextStyle(fontFamily: 'Cairo')),
                validator: (v) => (v == null || v.trim().isEmpty) ? 'مطلوب' : null,
              ),
              TextFormField(
                controller: _notes,
                maxLines: 3,
                decoration: const InputDecoration(labelText: 'ملاحظات', labelStyle: TextStyle(fontFamily: 'Cairo')),
              ),
              const SizedBox(height: 20),
              ElevatedButton(
                onPressed: _saving ? null : _submit,
                style: ElevatedButton.styleFrom(backgroundColor: AppTheme.primaryColor, minimumSize: const Size.fromHeight(48)),
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
}
