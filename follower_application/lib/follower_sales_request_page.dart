import 'dart:convert';

import 'package:follower_application/config/app_env.dart';
import 'package:follower_application/utils/AppTheme.dart';
import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';

class FollowerSalesRequestPage extends StatefulWidget {
  const FollowerSalesRequestPage({super.key});

  @override
  State<FollowerSalesRequestPage> createState() => _FollowerSalesRequestPageState();
}

class _FollowerSalesRequestPageState extends State<FollowerSalesRequestPage> {
  final _form = GlobalKey<FormState>();
  final _name = TextEditingController();
  final _phone = TextEditingController();
  final _province = TextEditingController();
  final _address = TextEditingController();
  final _notes = TextEditingController();
  bool _saving = false;
  Map<String, dynamic>? _customer;
  int _listId = 0;

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    final args = ModalRoute.of(context)?.settings.arguments;
    if (args is Map && _customer == null) {
      _customer = Map<String, dynamic>.from(args['customer'] as Map);
      _listId = int.tryParse('${args['listId'] ?? 0}') ?? 0;
      _name.text = '${_customer!['customerName'] ?? ''}';
      _phone.text = '${_customer!['phoneNumber'] ?? ''}';
      _address.text = '${_customer!['address'] ?? ''}';
      _province.text = '${_customer!['cityName'] ?? ''}';
    }
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
    if (!_form.currentState!.validate() || _saving) return;
    final customerId = int.tryParse('${_customer?['customerId'] ?? 0}') ?? 0;
    if (customerId <= 0 || _listId <= 0) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('بيانات الزبون غير مكتملة', style: TextStyle(fontFamily: 'Cairo'))),
      );
      return;
    }
    setState(() => _saving = true);
    try {
      final prefs = await SharedPreferences.getInstance();
      final asyncId = prefs.getString('AsyncId') ?? '';
      final link = AppEnv.apiBase(fallback: prefs.getString('LinkDelegate') ?? '');
      final uri = Uri.parse('${link}Followers/SalesRequests');
      final response = await http
          .post(
            uri,
            headers: {'Content-Type': 'application/json'},
            body: json.encode({
              'asyncId': asyncId,
              'listId': _listId,
              'customerId': customerId,
              'fullName': _name.text.trim(),
              'phone': _phone.text.trim(),
              'province': _province.text.trim(),
              'address': _address.text.trim(),
              'notes': _notes.text.trim(),
              'createdByUserId': 111,
            }),
          )
          .timeout(const Duration(seconds: 30));
      if (!mounted) return;
      if (response.statusCode == 200) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('تم إرسال طلب المبيع إلى مدير المبيعات', style: TextStyle(fontFamily: 'Cairo'))),
        );
        Navigator.pop(context);
      } else {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('تعذر الإرسال (${response.statusCode})', style: const TextStyle(fontFamily: 'Cairo'))),
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

  @override
  Widget build(BuildContext context) {
    return Directionality(
      textDirection: TextDirection.rtl,
      child: Scaffold(
        appBar: AppBar(
          title: const Text('إرسال طلب مبيع', style: TextStyle(fontFamily: 'Cairo')),
          backgroundColor: AppTheme.primaryColor,
        ),
        body: Form(
          key: _form,
          child: ListView(
            padding: const EdgeInsets.all(16),
            children: [
              const Text(
                'سيصل الطلب إلى مدير المبيعات. المصدر: المتابع',
                style: TextStyle(fontFamily: 'Cairo', color: Colors.grey),
              ),
              const SizedBox(height: 16),
              TextFormField(
                controller: _name,
                decoration: const InputDecoration(labelText: 'اسم الزبون *', labelStyle: TextStyle(fontFamily: 'Cairo')),
                validator: (v) => (v == null || v.trim().isEmpty) ? 'مطلوب' : null,
              ),
              TextFormField(
                controller: _phone,
                decoration: const InputDecoration(labelText: 'الهاتف', labelStyle: TextStyle(fontFamily: 'Cairo')),
                keyboardType: TextInputType.phone,
              ),
              TextFormField(
                controller: _province,
                decoration: const InputDecoration(labelText: 'المحافظة', labelStyle: TextStyle(fontFamily: 'Cairo')),
              ),
              TextFormField(
                controller: _address,
                decoration: const InputDecoration(labelText: 'العنوان', labelStyle: TextStyle(fontFamily: 'Cairo')),
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
