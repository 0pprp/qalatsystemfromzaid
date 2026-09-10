import 'dart:convert';

import 'package:follower_application/config/app_env.dart';
import 'package:follower_application/services/follower_media_urls.dart';
import 'package:follower_application/ui/app_safe_scaffold.dart';
import 'package:follower_application/utils/AppTheme.dart';
import 'package:follower_application/utils/iraq_datetime.dart';
import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';
import 'package:url_launcher/url_launcher.dart';

class CustomerProfilePage extends StatefulWidget {
  const CustomerProfilePage({
    super.key,
    required this.customer,
    required this.listId,
    @visibleForTesting this.seedProfile,
  });

  final Map<String, dynamic> customer;
  final int listId;

  /// When set (tests only), skips network load and renders this profile.
  @visibleForTesting
  final Map<String, dynamic>? seedProfile;

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
  String _apiBase = '';
  String _asyncId = '';

  int get _customerId =>
      int.tryParse('${_profile?['customerId'] ?? _profile?['CustomerId'] ?? widget.customer['customerId'] ?? 0}') ?? 0;

  @override
  void initState() {
    super.initState();
    if (widget.seedProfile != null) {
      _profile = Map<String, dynamic>.from(widget.seedProfile!);
      _loading = false;
    } else {
      _loadProfile();
    }
  }

  @override
  void dispose() {
    _noteController.dispose();
    super.dispose();
  }

  Future<Map<String, String>> _session() async {
    final prefs = await SharedPreferences.getInstance();
    final link = AppEnv.apiBase(fallback: prefs.getString('LinkDelegate') ?? '');
    final asyncId = prefs.getString('AsyncId') ?? '';
    _apiBase = link;
    _asyncId = asyncId;
    return {
      'asyncId': asyncId,
      'link': link,
    };
  }

  Future<void> _dial(String? phone) async {
    final digits = (phone ?? '').replaceAll(RegExp(r'[^\d+]'), '');
    if (digits.isEmpty) return;
    final uri = Uri(scheme: 'tel', path: digits);
    final ok = await launchUrl(uri, mode: LaunchMode.externalApplication);
    if (!ok && mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('تعذر فتح تطبيق الاتصال', style: TextStyle(fontFamily: 'Cairo'))),
      );
    }
  }

  Future<void> _loadProfile({bool silent = false}) async {
    final id = int.tryParse('${widget.customer['customerId'] ?? 0}') ?? 0;
    if (id <= 0) {
      setState(() {
        _loading = false;
        _error = 'معرّف الزبون غير متوفر';
      });
      return;
    }
    if (!silent) {
      setState(() {
        _loading = true;
        _error = null;
        _httpStatus = null;
      });
    }
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
        _error = null;
      });
    } catch (e) {
      if (!mounted) return;
      if (silent && _profile != null) {
        setState(() {});
        return;
      }
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
      if (!mounted) return;
      if (response.statusCode != 200) {
        throw Exception(response.statusCode);
      }
      Map<String, dynamic>? saved;
      try {
        final decoded = json.decode(response.body);
        if (decoded is Map) saved = Map<String, dynamic>.from(decoded);
      } catch (_) {}
      _noteController.clear();
      if (saved != null) {
        setState(() {
          final notes = List<dynamic>.from((_profile?['notes'] ?? _profile?['Notes'] ?? []) as List? ?? []);
          notes.insert(0, saved);
          _profile = {
            ...?_profile,
            'notes': notes,
            'Notes': notes,
          };
        });
      }
      await _loadProfile(silent: true);
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

  String _iraqClock(dynamic utc) => IraqDateTime.formatClock(utc);

  String _num(dynamic v) => IraqDateTime.formatNumber(v);

  String? _pick(Map m, List<String> keys) {
    for (final k in keys) {
      final v = m[k];
      if (v != null && '$v'.trim().isNotEmpty) return '$v'.trim();
    }
    return null;
  }

  List<Map<String, dynamic>> _normalizeImages(Map<String, dynamic>? p) {
    final raw = (p?['images'] ?? p?['Images'] ?? p?['documents'] ?? p?['Documents'] ?? []) as List? ?? [];
    final list = <Map<String, dynamic>>[];
    for (final item in raw.whereType<Map>()) {
      final m = Map<String, dynamic>.from(item);
      final resolved = FollowerMediaUrls.resolve(
        apiBase: _apiBase.isNotEmpty ? _apiBase : AppEnv.apiBase(fallback: AppEnv.demoApiBaseUrl),
        customerId: _customerId,
        asyncId: _asyncId,
        listId: widget.listId,
        image: m,
      );
      if (resolved == null || resolved.isEmpty) continue;
      m['url'] = resolved;
      list.add(m);
    }
    return list;
  }

  @override
  Widget build(BuildContext context) {
    final p = _profile;
    final notes = (p?['notes'] ?? p?['Notes'] ?? []) as List? ?? [];
    final images = _normalizeImages(p);

    return Directionality(
      textDirection: TextDirection.rtl,
      child: AppSafeScaffold(
        appBar: AppBar(
          title: const Text('ملف الزبون', style: TextStyle(fontFamily: 'Cairo')),
          backgroundColor: AppTheme.primaryColor,
          actions: [
            IconButton(onPressed: _loading ? null : () => _loadProfile(), icon: const Icon(Icons.refresh)),
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
                      padding: AppInsets.scrollPadding(context, horizontal: 16, top: 16, extraBottom: 16),
                      children: [
                        Text(
                          _pick(p!, ['customerName', 'CustomerName']) ?? '',
                          style: const TextStyle(fontFamily: 'Cairo', fontWeight: FontWeight.bold, fontSize: 18),
                        ),
                        _phoneLine(_pick(p, ['phoneNumber', 'PhoneNumber'])),
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
                        const SizedBox(height: 16),
                        const Text('الصور والمستمسكات', style: TextStyle(fontFamily: 'Cairo', fontWeight: FontWeight.bold)),
                        const SizedBox(height: 8),
                        if (images.isEmpty)
                          _placeholder('لا توجد صور للزبون')
                        else
                          ...images.map(_documentTile),
                        const SizedBox(height: 24),
                        const Text('الملاحظات', style: TextStyle(fontFamily: 'Cairo', fontWeight: FontWeight.bold, fontSize: 16)),
                        const SizedBox(height: 8),
                        TextField(
                          controller: _noteController,
                          maxLines: 3,
                          decoration: const InputDecoration(
                            border: OutlineInputBorder(),
                            hintText: 'أضف ملاحظة جديدة...',
                            hintStyle: TextStyle(fontFamily: 'Cairo'),
                          ),
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

  Widget _documentTile(Map<String, dynamic> img) {
    final url = _pick(img, ['url', 'Url'])!;
    final label = _pick(img, ['label', 'Label', 'kind', 'Kind']) ?? 'مستمسك';
    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(label, style: const TextStyle(fontFamily: 'Cairo', fontWeight: FontWeight.w600)),
          const SizedBox(height: 6),
          ClipRRect(
            borderRadius: BorderRadius.circular(12),
            child: Image.network(
              url,
              height: 160,
              width: double.infinity,
              fit: BoxFit.cover,
              loadingBuilder: (context, child, progress) {
                if (progress == null) return child;
                return Container(
                  height: 160,
                  alignment: Alignment.center,
                  color: Colors.grey.shade200,
                  child: const CircularProgressIndicator(strokeWidth: 2),
                );
              },
              errorBuilder: (_, __, ___) => _placeholder('تعذر تحميل: $label'),
            ),
          ),
        ],
      ),
    );
  }

  Widget _phoneLine(String? phone) {
    if (phone == null || phone.isEmpty) return const SizedBox.shrink();
    final dialable = phone.replaceAll(RegExp(r'[^\d+]'), '').isNotEmpty;
    return Padding(
      padding: const EdgeInsets.only(top: 4),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.center,
        children: [
          const SizedBox(width: 110, child: Text('الهاتف', style: TextStyle(fontFamily: 'Cairo', color: Colors.grey))),
          Expanded(
            child: InkWell(
              onTap: dialable ? () => _dial(phone) : null,
              child: Row(
                children: [
                  Flexible(
                    child: Text(
                      phone,
                      style: TextStyle(
                        fontFamily: 'Cairo',
                        color: dialable ? AppTheme.primaryColor : null,
                        decoration: dialable ? TextDecoration.underline : null,
                      ),
                    ),
                  ),
                  if (dialable) ...[
                    const SizedBox(width: 8),
                    Icon(Icons.phone, size: 18, color: AppTheme.primaryColor),
                  ],
                ],
              ),
            ),
          ),
        ],
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
