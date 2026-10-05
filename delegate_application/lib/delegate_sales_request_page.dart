import 'dart:convert';

import 'package:delegate_application/ui/app_safe_scaffold.dart';
import 'package:delegate_application/utils/AppTheme.dart';
import 'package:delegate_application/utils/thousands_separator_input_formatter.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';

/// Same fields as follower_application FollowerSalesRequestPage.
/// [saleRequestType] is fixed by entry point: New (Home) or Old (customer card).
/// New sales require one-time store evaluation (rating + shop money).
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
  final _shopNote = TextEditingController();
  final _shopStock = TextEditingController();
  final _shopDaily = TextEditingController();
  bool _saving = false;
  bool _ready = false;
  Map<String, dynamic>? _customer;
  String _saleRequestType = 'New';
  int _shopRating = 7;

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
    _shopNote.dispose();
    _shopStock.dispose();
    _shopDaily.dispose();
    super.dispose();
  }

  bool _validPhone(String phone) {
    final p = phone.trim().replaceAll(RegExp(r'\s+'), '');
    return RegExp(r'^07\d{9}$').hasMatch(p);
  }

  double? _parseMoney(String raw) =>
      ThousandsSeparatorInputFormatter.parseMoney(raw);

  String? _validateNewShop() {
    if (_shopRating < 7 && _shopNote.text.trim().isEmpty) {
      return 'الملاحظة إجبارية عندما يكون تقييم المحل أقل من 7';
    }
    final stock = _parseMoney(_shopStock.text);
    if (stock == null || stock <= 0) {
      return 'قيمة بضاعة المحل مطلوبة';
    }
    final daily = _parseMoney(_shopDaily.text);
    if (daily == null || daily <= 0) {
      return 'واردات المحل اليومية مطلوبة';
    }
    return null;
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
      final shopErr = _validateNewShop();
      if (shopErr != null) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(shopErr, style: const TextStyle(fontFamily: 'Cairo'))),
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
          'shopRating': _shopRating,
          'shopRatingNote':
              _shopNote.text.trim().isEmpty ? null : _shopNote.text.trim(),
          'shopStockEstimatedValue': _parseMoney(_shopStock.text),
          'estimatedDailyRevenue': _parseMoney(_shopDaily.text),
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
            content: Text(
              'تم إرسال طلب المبيع إلى مدير المبيعات',
              style: TextStyle(fontFamily: 'Cairo'),
            ),
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

  Widget _newSaleEvaluationContainer() {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: AppTheme.primaryColor.withValues(alpha: 0.06),
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: AppTheme.primaryColor.withValues(alpha: 0.25)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          const Text(
            'تقييم المحل — المبيعات الجديدة',
            style: TextStyle(
              fontFamily: 'Cairo',
              fontWeight: FontWeight.bold,
              fontSize: 16,
            ),
            textAlign: TextAlign.right,
          ),
          const SizedBox(height: 4),
          Text(
            _shopRating < 7
                ? 'التقييم إجباري مع ملاحظة إجبارية'
                : 'التقييم إجباري مع ملاحظة اختيارية',
            style: TextStyle(
              fontFamily: 'Cairo',
              fontSize: 12,
              color: _shopRating < 7 ? Colors.orange.shade800 : Colors.grey,
            ),
            textAlign: TextAlign.right,
          ),
          const SizedBox(height: 8),
          Text(
            'التقييم: $_shopRating',
            style: const TextStyle(fontFamily: 'Cairo', fontWeight: FontWeight.w700),
            textAlign: TextAlign.right,
          ),
          Slider(
            value: _shopRating.toDouble(),
            min: 1,
            max: 10,
            divisions: 9,
            label: '$_shopRating',
            onChanged: (v) => setState(() => _shopRating = v.round()),
          ),
          TextFormField(
            controller: _shopNote,
            maxLines: 3,
            decoration: InputDecoration(
              labelText: _shopRating < 7 ? 'ملاحظة *' : 'ملاحظة (اختياري)',
              labelStyle: const TextStyle(fontFamily: 'Cairo'),
            ),
            validator: (v) {
              if (_shopRating < 7 && (v == null || v.trim().isEmpty)) {
                return 'الملاحظة إجبارية للتقييم أقل من 7';
              }
              return null;
            },
          ),
          TextFormField(
            controller: _shopStock,
            keyboardType: TextInputType.number,
            inputFormatters: [ThousandsSeparatorInputFormatter()],
            decoration: const InputDecoration(
              labelText: 'قيمة بضاعة المحل *',
              labelStyle: TextStyle(fontFamily: 'Cairo'),
            ),
            validator: (v) {
              final n = _parseMoney(v ?? '');
              if (n == null || n <= 0) return 'مطلوب';
              return null;
            },
          ),
          TextFormField(
            controller: _shopDaily,
            keyboardType: TextInputType.number,
            inputFormatters: [ThousandsSeparatorInputFormatter()],
            decoration: const InputDecoration(
              labelText: 'واردات المحل اليومية *',
              labelStyle: TextStyle(fontFamily: 'Cairo'),
            ),
            validator: (v) {
              final n = _parseMoney(v ?? '');
              if (n == null || n <= 0) return 'مطلوب';
              return null;
            },
          ),
        ],
      ),
    );
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
                    : 'مبيع جديد — املأ تقييم المحل مرة واحدة مع قيمة البضاعة والوارد اليومي.',
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
                const SizedBox(height: 16),
                _newSaleEvaluationContainer(),
              ],
              TextFormField(
                controller: _notes,
                maxLines: 3,
                decoration: const InputDecoration(
                  labelText: 'ملاحظات إضافية (اختياري)',
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
