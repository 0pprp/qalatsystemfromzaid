import 'dart:convert';

import 'package:follower_application/config/app_env.dart';
import 'package:follower_application/services/follower_media_urls.dart';
import 'package:follower_application/ui/app_safe_scaffold.dart';
import 'package:follower_application/utils/AppTheme.dart';
import 'package:follower_application/utils/iraq_datetime.dart';
import 'package:follower_application/utils/thousand_separator_formatter.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';
import 'package:url_launcher/url_launcher.dart';

class CustomerProfilePage extends StatefulWidget {
  const CustomerProfilePage({
    super.key,
    required this.customer,
    required this.listId,
    this.showAuthenticate = false,
    this.hideSaleRequest = false,
    @visibleForTesting this.seedProfile,
  });

  final Map<String, dynamic> customer;
  final int listId;
  final bool showAuthenticate;

  /// When true (e.g. opened from list-stats rating drill-down), hide «إرسال طلب مبيع».
  final bool hideSaleRequest;

  /// When set (tests only), skips network load and renders this profile.
  @visibleForTesting
  final Map<String, dynamic>? seedProfile;

  @override
  State<CustomerProfilePage> createState() => _CustomerProfilePageState();
}

class _CustomerProfilePageState extends State<CustomerProfilePage> {
  Map<String, dynamic>? _profile;
  bool _loading = true;
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
                        _line(
                          'اسم البائع',
                          _pick(p, ['saleName', 'SaleName']) ??
                              _pick(widget.customer, ['saleName', 'SaleName']),
                        ),
                        _line('مندوب القائمة', _pick(p, ['delegateName', 'DelegateName'])),
                        if (_delegateShopEval(p) != null) ...[
                          const Divider(height: 28),
                          const Text(
                            'تقييم المندوب (أول تسديد)',
                            style: TextStyle(fontFamily: 'Cairo', fontWeight: FontWeight.bold),
                          ),
                          const SizedBox(height: 8),
                          Container(
                            width: double.infinity,
                            padding: const EdgeInsets.all(12),
                            decoration: BoxDecoration(
                              color: AppTheme.primaryColor.withValues(alpha: 0.08),
                              borderRadius: BorderRadius.circular(12),
                              border: Border.all(
                                color: AppTheme.primaryColor.withValues(alpha: 0.25),
                              ),
                            ),
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                _line(
                                  'المندوب',
                                  _pick(_delegateShopEval(p)!, ['delegateName', 'DelegateName']),
                                ),
                                _line(
                                  'التقييم',
                                  '${_delegateShopEval(p)!['rating'] ?? _delegateShopEval(p)!['Rating'] ?? ''}',
                                ),
                                _line(
                                  'الملاحظة',
                                  _pick(_delegateShopEval(p)!, ['note', 'Note']),
                                ),
                                _line(
                                  'قيمة بضاعة المحل',
                                  _num(
                                    _delegateShopEval(p)!['shopStockEstimatedValue'] ??
                                        _delegateShopEval(p)!['ShopStockEstimatedValue'],
                                  ),
                                ),
                                _line(
                                  'واردات المحل اليومية',
                                  _num(
                                    _delegateShopEval(p)!['estimatedDailyRevenue'] ??
                                        _delegateShopEval(p)!['EstimatedDailyRevenue'],
                                  ),
                                ),
                                _line(
                                  'سعر البيع',
                                  _num(
                                    _delegateShopEval(p)!['saleAmount'] ??
                                        _delegateShopEval(p)!['SaleAmount'],
                                  ),
                                ),
                                _line(
                                  'تاريخ التقييم',
                                  _iraqClock(
                                    _delegateShopEval(p)!['createdAtUtc'] ??
                                        _delegateShopEval(p)!['CreatedAtUtc'],
                                  ),
                                ),
                              ],
                            ),
                          ),
                        ],
                        if (_followerAuth(p) != null) ...[
                          const Divider(height: 28),
                          const Text(
                            'تقييم المتابعة',
                            style: TextStyle(fontFamily: 'Cairo', fontWeight: FontWeight.bold),
                          ),
                          const SizedBox(height: 8),
                          Container(
                            width: double.infinity,
                            padding: const EdgeInsets.all(12),
                            decoration: BoxDecoration(
                              color: AppTheme.primaryColor.withValues(alpha: 0.08),
                              borderRadius: BorderRadius.circular(12),
                              border: Border.all(
                                color: AppTheme.primaryColor.withValues(alpha: 0.25),
                              ),
                            ),
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                _line('من صادق', _pick(_followerAuth(p)!, ['followerName', 'FollowerName'])),
                                _line(
                                  'التقييم',
                                  '${_followerAuth(p)!['rating'] ?? _followerAuth(p)!['Rating'] ?? ''}',
                                ),
                                _line('الملاحظة', _pick(_followerAuth(p)!, ['note', 'Note'])),
                                _line(
                                  'قيمة بضاعة المحل',
                                  _num(
                                    _followerAuth(p)!['shopStockEstimatedValue'] ??
                                        _followerAuth(p)!['ShopStockEstimatedValue'],
                                  ),
                                ),
                                _line(
                                  'واردات المحل اليومية',
                                  _num(
                                    _followerAuth(p)!['estimatedDailyRevenue'] ??
                                        _followerAuth(p)!['EstimatedDailyRevenue'],
                                  ),
                                ),
                                _line(
                                  'تاريخ المصادقة',
                                  _iraqClock(
                                    _followerAuth(p)!['authenticatedAtUtc'] ??
                                        _followerAuth(p)!['AuthenticatedAtUtc'],
                                  ),
                                ),
                              ],
                            ),
                          ),
                        ],
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
                        if (!widget.showAuthenticate && !widget.hideSaleRequest)
                          OutlinedButton.icon(
                            onPressed: () => Navigator.pushNamed(
                              context,
                              '/FollowerSalesRequest',
                              arguments: {
                                'customer': {...widget.customer, ...?_profile},
                                'listId': widget.listId
                              },
                            ),
                            icon: const Icon(Icons.send),
                            label: const Text('إرسال طلب مبيع',
                                style: TextStyle(fontFamily: 'Cairo')),
                          ),
                        if (widget.showAuthenticate &&
                            _followerAuth(p) == null)
                          FilledButton.icon(
                            style: FilledButton.styleFrom(
                                backgroundColor: AppTheme.primaryColor),
                            onPressed: () => _authenticateSale(p),
                            icon: const Icon(Icons.verified_user),
                            label: const Text('تمت المصادقة',
                                style: TextStyle(fontFamily: 'Cairo')),
                          ),
                        const SizedBox(height: 16),
                        const Text('التسديدات', style: TextStyle(fontFamily: 'Cairo', fontWeight: FontWeight.bold)),
                        const SizedBox(height: 8),
                        ..._paymentsSection(p),
                        const SizedBox(height: 16),
                        const Text('الصور والمستمسكات', style: TextStyle(fontFamily: 'Cairo', fontWeight: FontWeight.bold)),
                        const SizedBox(height: 8),
                        if (images.isEmpty)
                          _placeholder('لا توجد صور للزبون')
                        else
                          ...images.map(_documentTile),
                        const SizedBox(height: 16),
                        const Text('ملاحظات الزبون', style: TextStyle(fontFamily: 'Cairo', fontWeight: FontWeight.bold, fontSize: 16)),
                        const SizedBox(height: 8),
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
          Material(
            color: Colors.transparent,
            child: InkWell(
              borderRadius: BorderRadius.circular(12),
              onTap: () => _openFullImage(url: url, label: label),
              child: ClipRRect(
                borderRadius: BorderRadius.circular(12),
                child: Stack(
                  alignment: Alignment.bottomLeft,
                  children: [
                    Image.network(
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
                      errorBuilder: (_, __, ___) =>
                          _placeholder('تعذر تحميل: $label'),
                    ),
                    Container(
                      margin: const EdgeInsets.all(8),
                      padding: const EdgeInsets.symmetric(
                          horizontal: 8, vertical: 4),
                      decoration: BoxDecoration(
                        color: Colors.black54,
                        borderRadius: BorderRadius.circular(8),
                      ),
                      child: const Text(
                        'اضغط للعرض الكامل',
                        style: TextStyle(
                            fontFamily: 'Cairo',
                            color: Colors.white,
                            fontSize: 11),
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }

  void _openFullImage({required String url, required String label}) {
    Navigator.of(context).push(
      PageRouteBuilder(
        opaque: false,
        barrierColor: Colors.black,
        pageBuilder: (context, animation, secondaryAnimation) {
          return _FullScreenImagePage(url: url, label: label);
        },
        transitionsBuilder: (context, animation, secondaryAnimation, child) {
          return FadeTransition(opacity: animation, child: child);
        },
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

  Map<String, dynamic>? _followerAuth(Map<String, dynamic> p) {
    final raw = p['followerAuth'] ?? p['FollowerAuth'];
    if (raw is Map) return Map<String, dynamic>.from(raw);
    return null;
  }

  Map<String, dynamic>? _delegateShopEval(Map<String, dynamic> p) {
    final raw = p['delegateShopEval'] ?? p['DelegateShopEval'];
    if (raw is Map) return Map<String, dynamic>.from(raw);
    return null;
  }

  List<Widget> _paymentsSection(Map<String, dynamic> p) {
    final raw = (p['payments'] ?? p['Payments'] ?? []) as List? ?? [];
    final payments = raw
        .whereType<Map>()
        .map((e) => Map<String, dynamic>.from(e))
        .toList();
    if (payments.isEmpty) {
      return [
        const Text('لا توجد تسديدات', style: TextStyle(fontFamily: 'Cairo', color: Colors.grey)),
      ];
    }
    return payments.map((pay) {
      final amount = pay['amount'] ?? pay['Amount'];
      final date = pay['paymentDate'] ?? pay['PaymentDate'];
      final bound = pay['boundNumber'] ?? pay['BoundNumber'];
      final note = _pick(pay, ['note', 'Note']);
      final subtitle = [
        if (date != null) _paymentDateLabel(date),
        if (bound != null && '$bound'.trim().isNotEmpty) 'سند: $bound',
        if (note != null) note,
      ].where((s) => s.isNotEmpty).join(' · ');
      return Card(
        margin: const EdgeInsets.only(bottom: 8),
        child: ListTile(
          contentPadding: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
          title: Text(
            amount == null ? '—' : '${_num(amount)} د.ع',
            style: const TextStyle(fontFamily: 'Cairo', fontWeight: FontWeight.w600),
          ),
          subtitle: subtitle.isEmpty
              ? null
              : Text(subtitle, style: const TextStyle(fontFamily: 'Cairo', fontSize: 12)),
        ),
      );
    }).toList();
  }

  /// Payment dates are calendar values from SQL — avoid UTC clock shift.
  String _paymentDateLabel(dynamic v) {
    final raw = v?.toString().trim();
    if (raw == null || raw.isEmpty) return '';
    final parsed = DateTime.tryParse(raw);
    if (parsed == null) return raw;
    final d = parsed.isUtc ? parsed.toLocal() : parsed;
    final y = d.year.toString().padLeft(4, '0');
    final m = d.month.toString().padLeft(2, '0');
    final day = d.day.toString().padLeft(2, '0');
    return '$y/$m/$day';
  }

  Future<void> _authenticateSale(Map<String, dynamic> p) async {
    var rating = 7;
    var note = '';
    final stockCtrl = TextEditingController();
    final dailyCtrl = TextEditingController();
    String? localError;
    final ok = await showDialog<bool>(
      context: context,
      builder: (ctx) => StatefulBuilder(
        builder: (ctx, setLocal) => AlertDialog(
          title: const Text('مصادقة المبيع', style: TextStyle(fontFamily: 'Cairo')),
          content: SingleChildScrollView(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(
                  _pick(p, ['shopName', 'ShopName', 'customerName', 'CustomerName']) ?? '',
                  style: const TextStyle(fontFamily: 'Cairo', fontWeight: FontWeight.bold),
                ),
                const SizedBox(height: 8),
                Text('التقييم: $rating', style: const TextStyle(fontFamily: 'Cairo')),
                Slider(
                  value: rating.toDouble(),
                  min: 1,
                  max: 10,
                  divisions: 9,
                  label: '$rating',
                  onChanged: (v) => setLocal(() => rating = v.round()),
                ),
                Text(
                  rating >= 7 ? 'الملاحظة اختيارية' : 'الملاحظة إجبارية (أقل من 7)',
                  style: TextStyle(
                    fontFamily: 'Cairo',
                    fontSize: 12,
                    color: rating >= 7 ? Colors.grey : Colors.red,
                  ),
                ),
                TextField(
                  maxLines: 3,
                  onChanged: (v) => note = v,
                  decoration: const InputDecoration(
                    labelText: 'ملاحظة',
                    border: OutlineInputBorder(),
                  ),
                  style: const TextStyle(fontFamily: 'Cairo'),
                ),
                const SizedBox(height: 10),
                TextField(
                  controller: stockCtrl,
                  keyboardType: TextInputType.number,
                  inputFormatters: [
                    FilteringTextInputFormatter.digitsOnly,
                    ThousandSeparatorInputFormatter(),
                  ],
                  decoration: const InputDecoration(
                    labelText: 'قيمة بضاعة المحل *',
                    border: OutlineInputBorder(),
                  ),
                  style: const TextStyle(fontFamily: 'Cairo'),
                ),
                const SizedBox(height: 8),
                TextField(
                  controller: dailyCtrl,
                  keyboardType: TextInputType.number,
                  inputFormatters: [
                    FilteringTextInputFormatter.digitsOnly,
                    ThousandSeparatorInputFormatter(),
                  ],
                  decoration: const InputDecoration(
                    labelText: 'قيمة الوارد اليومي *',
                    border: OutlineInputBorder(),
                  ),
                  style: const TextStyle(fontFamily: 'Cairo'),
                ),
                if (localError != null) ...[
                  const SizedBox(height: 8),
                  Text(localError!,
                      style: const TextStyle(fontFamily: 'Cairo', color: Colors.red)),
                ],
              ],
            ),
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(ctx, false),
              child: const Text('إلغاء', style: TextStyle(fontFamily: 'Cairo')),
            ),
            ElevatedButton(
              onPressed: () {
                if (rating < 7 && note.trim().isEmpty) {
                  setLocal(() => localError = 'الملاحظة إجبارية للتقييم أقل من 7');
                  return;
                }
                final stock = ThousandSeparatorInputFormatter.parse(stockCtrl.text);
                final daily = ThousandSeparatorInputFormatter.parse(dailyCtrl.text);
                if (stock == null || stock <= 0) {
                  setLocal(() => localError = 'قيمة بضاعة المحل مطلوبة');
                  return;
                }
                if (daily == null || daily <= 0) {
                  setLocal(() => localError = 'قيمة الوارد اليومي مطلوبة');
                  return;
                }
                Navigator.pop(ctx, true);
              },
              child: const Text('اعتماد', style: TextStyle(fontFamily: 'Cairo')),
            ),
          ],
        ),
      ),
    );
    final stock = ThousandSeparatorInputFormatter.parse(stockCtrl.text) ?? 0;
    final daily = ThousandSeparatorInputFormatter.parse(dailyCtrl.text) ?? 0;
    stockCtrl.dispose();
    dailyCtrl.dispose();
    if (ok != true) return;
    try {
      final s = await _session();
      final uri = Uri.parse('${s['link']}Followers/PendingSales/Authenticate');
      final res = await http
          .post(
            uri,
            headers: {'Content-Type': 'application/json'},
            body: json.encode({
              'asyncId': s['asyncId'],
              'customerId': _customerId,
              'listId': widget.listId,
              'rating': rating,
              'note': note.trim().isEmpty ? null : note.trim(),
              'shopStockEstimatedValue': stock,
              'estimatedDailyRevenue': daily,
            }),
          )
          .timeout(const Duration(seconds: 30));
      if (res.statusCode != 200) {
        throw Exception('HTTP ${res.statusCode}');
      }
      await _loadProfile();
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('تمت المصادقة', style: TextStyle(fontFamily: 'Cairo')),
          ),
        );
      }
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('تعذر حفظ المصادقة', style: TextStyle(fontFamily: 'Cairo')),
          ),
        );
      }
    }
  }
}

/// Full-screen image: tap background / رجوع / زر النظام للإغلاق.
class _FullScreenImagePage extends StatelessWidget {
  const _FullScreenImagePage({required this.url, required this.label});

  final String url;
  final String label;

  void _close(BuildContext context) {
    if (Navigator.of(context).canPop()) {
      Navigator.of(context).pop();
    }
  }

  @override
  Widget build(BuildContext context) {
    return Directionality(
      textDirection: TextDirection.rtl,
      child: Scaffold(
        backgroundColor: Colors.black,
        body: SafeArea(
          child: Stack(
            children: [
              // Tap empty area to close.
              Positioned.fill(
                child: GestureDetector(
                  behavior: HitTestBehavior.opaque,
                  onTap: () => _close(context),
                  child: const ColoredBox(color: Colors.black),
                ),
              ),
              // Image (pinch-zoom). Tap image also closes when not zooming.
              Center(
                child: GestureDetector(
                  onTap: () => _close(context),
                  child: InteractiveViewer(
                    minScale: 1,
                    maxScale: 4,
                    child: Image.network(
                      url,
                      fit: BoxFit.contain,
                      loadingBuilder: (context, child, progress) {
                        if (progress == null) return child;
                        return const SizedBox(
                          width: 48,
                          height: 48,
                          child: CircularProgressIndicator(color: Colors.white),
                        );
                      },
                      errorBuilder: (_, __, ___) => const Padding(
                        padding: EdgeInsets.all(24),
                        child: Text(
                          'تعذر عرض الصورة',
                          style: TextStyle(
                              fontFamily: 'Cairo', color: Colors.white),
                          textAlign: TextAlign.center,
                        ),
                      ),
                    ),
                  ),
                ),
              ),
              Positioned(
                top: 8,
                right: 8,
                left: 8,
                child: Row(
                  children: [
                    IconButton(
                      onPressed: () => _close(context),
                      icon: const Icon(Icons.arrow_back, color: Colors.white, size: 28),
                      tooltip: 'رجوع',
                    ),
                    Expanded(
                      child: Text(
                        label,
                        style: const TextStyle(
                          fontFamily: 'Cairo',
                          color: Colors.white,
                          fontWeight: FontWeight.bold,
                        ),
                        overflow: TextOverflow.ellipsis,
                      ),
                    ),
                    TextButton(
                      onPressed: () => _close(context),
                      child: const Text(
                        'رجوع',
                        style: TextStyle(
                            fontFamily: 'Cairo', color: Colors.white),
                      ),
                    ),
                  ],
                ),
              ),
              const Positioned(
                bottom: 16,
                left: 16,
                right: 16,
                child: Text(
                  'اضغط على الشاشة أو رجوع للإغلاق',
                  textAlign: TextAlign.center,
                  style: TextStyle(
                    fontFamily: 'Cairo',
                    color: Colors.white70,
                    fontSize: 12,
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

