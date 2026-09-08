import 'dart:convert';

import 'package:follower_application/config/app_env.dart';
import 'package:follower_application/utils/AppTheme.dart';
import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'package:intl/intl.dart' hide TextDirection;
import 'package:shared_preferences/shared_preferences.dart';

class CustomerProfilePage extends StatefulWidget {
  const CustomerProfilePage({super.key, required this.customer, required this.listId});

  final Map<String, dynamic> customer;
  final int listId;

  @override
  State<CustomerProfilePage> createState() => _CustomerProfilePageState();
}

class _CustomerProfilePageState extends State<CustomerProfilePage> {
  final _noteController = TextEditingController();
  Map<String, dynamic>? _profile;
  bool _loading = true;
  bool _saving = false;
  String? _error;
  int? _httpStatus;

  int get _customerId =>
      int.tryParse('${_profile?['customerId'] ?? _profile?['CustomerId'] ?? widget.customer['customerId'] ?? 0}') ?? 0;

  @override
  void initState() {
    super.initState();
    _loadProfile();
  }

  @override
  void dispose() {
    _noteController.dispose();
    super.dispose();
  }

  Future<Map<String, String>> _session() async {
    final prefs = await SharedPreferences.getInstance();
    return {
      'asyncId': prefs.getString('AsyncId') ?? '',
      'link': AppEnv.apiBase(fallback: prefs.getString('LinkDelegate') ?? ''),
    };
  }

  Future<void> _loadProfile() async {
    final id = int.tryParse('${widget.customer['customerId'] ?? 0}') ?? 0;
    if (id <= 0) {
      setState(() {
        _loading = false;
        _error = 'معرّف الزبون غير متوفر';
      });
      return;
    }
    setState(() {
      _loading = true;
      _error = null;
      _httpStatus = null;
    });
    try {
      final session = await _session();
      final uri = Uri.parse('${session['link']}Followers/Customers/$id/profile').replace(
        queryParameters: {
          'asyncId': session['asyncId'],
          'listId': widget.listId.toString(),
        },
      );
      final response = await http.get(uri).timeout(const Duration(seconds: 25));
      _httpStatus = response.statusCode;
      if (response.statusCode == 404) {
        throw Exception('404');
      }
      if (response.statusCode == 403) {
        throw Exception('403');
      }
      if (response.statusCode != 200) {
        throw Exception('http ${response.statusCode}');
      }
      final data = Map<String, dynamic>.from(json.decode(response.body) as Map);
      if (!mounted) return;
      setState(() {
        _profile = data;
        _loading = false;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _loading = false;
        if (e.toString().contains('404')) {
          _error = '404: مسار البروفايل غير موجود على السيرفر (Followers/Customers/{id}/profile)';
        } else if (e.toString().contains('403')) {
          _error = '403: الزبون خارج نطاق القوائم المسندة';
        } else {
          _error = 'تعذر تحميل بروفايل الزبون${_httpStatus != null ? ' (HTTP $_httpStatus)' : ''}';
        }
      });
    }
  }

  Future<void> _saveNote() async {
    final text = _noteController.text.trim();
    if (text.isEmpty || _saving || _customerId <= 0) return;
    setState(() => _saving = true);
    try {
      final session = await _session();
      final uri = Uri.parse('${session['link']}Followers/Customers/$_customerId/notes');
      final response = await http
          .post(
            uri,
            headers: {'Content-Type': 'application/json'},
            body: json.encode({
              'asyncId': session['asyncId'],
              'listId': widget.listId,
              'noteText': text,
            }),
          )
          .timeout(const Duration(seconds: 20));
      if (response.statusCode != 200) {
        throw Exception(response.statusCode);
      }
      _noteController.clear();
      await _loadProfile();
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('تعذر حفظ الملاحظة', style: TextStyle(fontFamily: 'Cairo'))),
        );
      }
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  String _iraqClock(dynamic utc) {
    final parsed = DateTime.tryParse(utc?.toString() ?? '')?.toUtc();
    if (parsed == null) return '—';
    final iraq = parsed.add(const Duration(hours: 3));
    return DateFormat('yyyy/MM/dd hh:mm a', 'en').format(iraq).replaceAll('AM', 'صباحاً').replaceAll('PM', 'مساءً');
  }

  String _num(dynamic v) {
    final n = double.tryParse('${v ?? ''}');
    if (n == null) return '—';
    return NumberFormat('#,##0').format(n);
  }

  String? _pick(Map m, List<String> keys) {
    for (final k in keys) {
      final v = m[k];
      if (v != null && '$v'.trim().isNotEmpty) return '$v'.trim();
    }
    return null;
  }

  @override
  Widget build(BuildContext context) {
    final p = _profile;
    final notes = (p?['notes'] ?? p?['Notes'] ?? []) as List? ?? [];
    final images = (p?['images'] ?? p?['Images'] ?? []) as List? ?? [];
    final imageUrl = _pick(p ?? {}, ['customerImageUrl', 'CustomerImageUrl']);

    return Directionality(
      textDirection: TextDirection.rtl,
      child: Scaffold(
        appBar: AppBar(
          title: const Text('ملف الزبون', style: TextStyle(fontFamily: 'Cairo')),
          backgroundColor: AppTheme.primaryColor,
          actions: [
            IconButton(onPressed: _loading ? null : _loadProfile, icon: const Icon(Icons.refresh)),
          ],
        ),
        body: _loading
            ? const Center(child: CircularProgressIndicator())
            : _error != null
                ? Center(
                    child: Padding(
                      padding: const EdgeInsets.all(24),
                      child: Text(_error!, textAlign: TextAlign.center, style: const TextStyle(fontFamily: 'Cairo', color: Colors.red)),
                    ),
                  )
                : ListView(
                    padding: const EdgeInsets.all(16),
                    children: [
                      if (imageUrl != null)
                        ClipRRect(
                          borderRadius: BorderRadius.circular(12),
                          child: Image.network(
                            imageUrl,
                            height: 180,
                            width: double.infinity,
                            fit: BoxFit.cover,
                            errorBuilder: (_, __, ___) => _placeholder('لا توجد صورة زبون'),
                          ),
                        )
                      else
                        _placeholder('لا توجد صورة زبون'),
                      const SizedBox(height: 12),
                      Text(_pick(p!, ['customerName', 'CustomerName']) ?? '', style: const TextStyle(fontFamily: 'Cairo', fontWeight: FontWeight.bold, fontSize: 18)),
                      _line('الهاتف', _pick(p, ['phoneNumber', 'PhoneNumber'])),
                      _line('العنوان', _pick(p, ['address', 'Address'])),
                      _line('المحافظة', _pick(p, ['cityName', 'CityName'])),
                      _line('المحل', _pick(p, ['shopName', 'ShopName'])),
                      _line('عنوان المحل', _pick(p, ['storeAddress', 'StoreAddress'])),
                      _line('هاتف المحل', _pick(p, ['storePhoneNumber', 'StorePhoneNumber'])),
                      _line('أقرب نقطة', _pick(p, ['nearestFunctionPoint', 'NearestFunctionPoint'])),
                      _line('الحي', _pick(p, ['neighborhood', 'Neighborhood'])),
                      _line('الموقع', () {
                        final lat = p['latitude'] ?? p['Latitude'];
                        final lng = p['longitude'] ?? p['Longitude'];
                        if (lat == null || lng == null) return null;
                        return '$lat , $lng';
                      }()),
                      _line('مندوب القائمة', _pick(p, ['delegateName', 'DelegateName'])),
                      const Divider(height: 28),
                      const Text('بيانات البيع', style: TextStyle(fontFamily: 'Cairo', fontWeight: FontWeight.bold)),
                      _line('سعر الشراء', _num(p['costTotalSales'] ?? p['CostTotalSales'])),
                      _line('سعر البيع', _num(p['amountTotalSales'] ?? p['AmountTotalSales'])),
                      _line('القسط اليومي', _num(p['amountDaySales'] ?? p['AmountDaySales'])),
                      _line('المدفوع', _num(p['receiptsTotal'] ?? p['ReceiptsTotal'])),
                      _line('الرصيد/الباقي', _num(p['amountRemaining'] ?? p['AmountRemaining'])),
                      _line('المقدم/اليومي المستلم', _num(p['amountReceverDay'] ?? p['AmountReceverDay'])),
                      _line('المواد', _pick(p, ['itemsNames', 'ItemsNames'])),
                      _line('ملاحظات النظام', _pick(p, ['customerSystemNotes', 'CustomerSystemNotes'])),
                      const SizedBox(height: 12),
                      OutlinedButton.icon(
                        onPressed: () => Navigator.pushNamed(
                          context,
                          '/FollowerSalesRequest',
                          arguments: {'customer': {...widget.customer, ...?_profile}, 'listId': widget.listId},
                        ),
                        icon: const Icon(Icons.send),
                        label: const Text('إرسال طلب مبيع', style: TextStyle(fontFamily: 'Cairo')),
                      ),
                      if (images.length > 1) ...[
                        const SizedBox(height: 16),
                        const Text('الصور', style: TextStyle(fontFamily: 'Cairo', fontWeight: FontWeight.bold)),
                        ...images.skip(1).map((raw) {
                          final img = Map<String, dynamic>.from(raw as Map);
                          final url = _pick(img, ['url', 'Url']);
                          if (url == null) return const SizedBox.shrink();
                          return Padding(
                            padding: const EdgeInsets.only(top: 8),
                            child: Image.network(url, height: 140, fit: BoxFit.cover, errorBuilder: (_, __, ___) => _placeholder('تعذر تحميل الصورة')),
                          );
                        }),
                      ],
                      const SizedBox(height: 24),
                      const Text('الملاحظات', style: TextStyle(fontFamily: 'Cairo', fontWeight: FontWeight.bold, fontSize: 16)),
                      const SizedBox(height: 8),
                      TextField(
                        controller: _noteController,
                        maxLines: 3,
                        decoration: const InputDecoration(border: OutlineInputBorder(), hintText: 'أضف ملاحظة جديدة...', hintStyle: TextStyle(fontFamily: 'Cairo')),
                      ),
                      const SizedBox(height: 8),
                      ElevatedButton(
                        onPressed: _saving ? null : _saveNote,
                        style: ElevatedButton.styleFrom(backgroundColor: AppTheme.primaryColor),
                        child: Text(_saving ? '...' : 'حفظ الملاحظة', style: const TextStyle(fontFamily: 'Cairo', color: Colors.white)),
                      ),
                      const SizedBox(height: 12),
                      if (notes.isEmpty)
                        const Text('لا توجد ملاحظات', style: TextStyle(fontFamily: 'Cairo', color: Colors.grey))
                      else
                        ...notes.map((raw) {
                          final n = Map<String, dynamic>.from(raw as Map);
                          return Card(
                            margin: const EdgeInsets.only(bottom: 8),
                            child: Padding(
                              padding: const EdgeInsets.all(12),
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  Text('${n['noteText'] ?? n['NoteText'] ?? ''}', style: const TextStyle(fontFamily: 'Cairo')),
                                  const SizedBox(height: 8),
                                  Text('الكاتب: ${n['createdByName'] ?? n['CreatedByName'] ?? ''}', style: const TextStyle(fontFamily: 'Cairo', fontSize: 12, color: Colors.grey)),
                                  const Text('الصفة: متابع', style: TextStyle(fontFamily: 'Cairo', fontSize: 12, color: Colors.grey)),
                                  Text('التاريخ: ${_iraqClock(n['createdAtUtc'] ?? n['CreatedAtUtc'])}', style: const TextStyle(fontFamily: 'Cairo', fontSize: 12, color: Colors.grey)),
                                ],
                              ),
                            ),
                          );
                        }),
                    ],
                  ),
      ),
    );
  }

  Widget _placeholder(String text) => Container(
        height: 120,
        alignment: Alignment.center,
        decoration: BoxDecoration(color: Colors.grey.shade200, borderRadius: BorderRadius.circular(12)),
        child: Text(text, style: const TextStyle(fontFamily: 'Cairo', color: Colors.grey)),
      );

  Widget _line(String label, String? value) {
    if (value == null || value.isEmpty || value == '—') {
      return const SizedBox.shrink();
    }
    return Padding(
      padding: const EdgeInsets.only(top: 4),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SizedBox(width: 110, child: Text(label, style: const TextStyle(fontFamily: 'Cairo', color: Colors.grey))),
          Expanded(child: Text(value, style: const TextStyle(fontFamily: 'Cairo'))),
        ],
      ),
    );
  }
}
