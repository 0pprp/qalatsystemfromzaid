import 'dart:convert';

import 'package:branch_manager_application/config/app_env.dart';
import 'package:branch_manager_application/core/session.dart';
import 'package:http/http.dart' as http;

class BmApiException implements Exception {
  BmApiException(this.message, {this.statusCode});
  final String message;
  final int? statusCode;
  @override
  String toString() => message;
}

class BmApi {
  BmApi._();
  static final BmApi instance = BmApi._();

  Map<String, String> get _authHeaders {
    final t = BmSession.instance.token;
    return {
      'Content-Type': 'application/json',
      'Accept': 'application/json',
      if ((t ?? '').isNotEmpty) 'Authorization': 'Bearer $t',
    };
  }

  String get _base {
    final b = BmSession.instance.apiBase;
    if (b == null || b.isEmpty) {
      throw BmApiException('لم يتم تحديد فرع العمل');
    }
    return b;
  }

  Future<List<Map<String, dynamic>>> fetchCities() async {
    if (AppEnv.isDemo) {
      return [
        {
          'name': 'النجف - DEMO',
          'link': AppEnv.demoApiBaseUrl,
          'database': 'DatabaseCompanyNajaf_DEMO',
          'value': 'DatabaseCompanyNajaf_DEMO',
        },
      ];
    }
    final res = await http
        .get(Uri.parse(AppEnv.citiesUrl), headers: {'Accept': 'application/json'})
        .timeout(const Duration(seconds: 25));
    if (res.statusCode != 200) {
      throw BmApiException('تعذر تحميل الفروع', statusCode: res.statusCode);
    }
    final data = jsonDecode(res.body);
    final list = data is List ? data : (data['data'] ?? data['Data'] ?? []) as List;
    return list.map((e) => Map<String, dynamic>.from(e as Map)).toList();
  }

  Future<void> login({
    required String apiBase,
    required String cityName,
    required String userName,
    required String password,
  }) async {
    final uri = Uri.parse('${BmSession.normalizeApiBase(apiBase)}Users/Users_LoginAdmin');
    final res = await http
        .post(
          uri,
          headers: {'Content-Type': 'application/json', 'Accept': 'application/json'},
          body: jsonEncode({'userName': userName.trim(), 'password': password}),
        )
        .timeout(const Duration(seconds: 30));

    Map<String, dynamic> body = {};
    try {
      body = Map<String, dynamic>.from(jsonDecode(res.body) as Map);
    } catch (_) {}

    if (res.statusCode != 200 || body['token'] == null) {
      final msg = '${body['message'] ?? body['Message'] ?? 'فشل تسجيل الدخول'}';
      throw BmApiException(msg, statusCode: res.statusCode);
    }

    final token = '${body['token']}';
    final claims = BmSession.decodeJwtPayload(token);
    final userType = '${claims['UserType'] ?? claims['userType'] ?? ''}'.trim();
    if (userType != 'مدير فرع') {
      throw BmApiException('هذا التطبيق مخصص لحسابات مدير الفرع فقط');
    }

    await BmSession.instance.save(
      token: token,
      apiBase: apiBase,
      cityName: cityName,
      userName: '${claims['UserName'] ?? claims['userName'] ?? userName}',
      userType: userType,
      userId: '${claims['UserID'] ?? claims['userID'] ?? ''}',
    );
  }

  Future<List<Map<String, dynamic>>> weakWeekPayers() async {
    final res = await http
        .get(Uri.parse('${_base}CustomerDecisions/WeakWeekPayers'), headers: _authHeaders)
        .timeout(const Duration(seconds: 90));
    _ensureOk(res);
    final data = jsonDecode(res.body);
    final list = data is List ? data : [];
    return list.map((e) => Map<String, dynamic>.from(e as Map)).toList();
  }

  /// Payments for one list (delegate) in a date range.
  Future<List<Map<String, dynamic>>> listPayments({
    required DateTime from,
    required DateTime to,
    required int delegateId,
    String textSearch = 'null',
  }) async {
    final fromDate = from.toIso8601String().split('T').first;
    final toDate = to.toIso8601String().split('T').first;
    final q = textSearch.trim().isEmpty ? 'null' : textSearch.trim();
    final path =
        'CustomersPayments/CustomersPayments_GetAll/$fromDate&&$toDate&&$delegateId&&${Uri.encodeComponent(q)}';
    final res = await http
        .get(Uri.parse('$_base$path'), headers: _authHeaders)
        .timeout(const Duration(seconds: 60));
    _ensureOk(res);
    final data = jsonDecode(res.body);
    final list = data is List ? data : [];
    return list.map((e) => Map<String, dynamic>.from(e as Map)).toList();
  }

  Future<Map<String, dynamic>> decide({
    required int customerId,
    required String decisionType,
    String? note,
  }) async {
    final res = await http
        .post(
          Uri.parse('${_base}CustomerDecisions/Decide'),
          headers: _authHeaders,
          body: jsonEncode({
            'customerID': customerId,
            'decisionType': decisionType,
            'note': note,
          }),
        )
        .timeout(const Duration(seconds: 30));
    _ensureOk(res);
    final data = jsonDecode(res.body);
    return data is Map ? Map<String, dynamic>.from(data) : <String, dynamic>{};
  }

  Future<List<Map<String, dynamic>>> decisions({
    DateTime? from,
    DateTime? to,
    int? customerId,
  }) async {
    final qp = <String, String>{};
    if (from != null) qp['fromDate'] = from.toIso8601String();
    if (to != null) qp['toDate'] = to.toIso8601String();
    if (customerId != null && customerId > 0) {
      qp['customerID'] = '$customerId';
    }
    final uri = Uri.parse('${_base}CustomerDecisions/Decisions')
        .replace(queryParameters: qp.isEmpty ? null : qp);
    final res = await http.get(uri, headers: _authHeaders).timeout(const Duration(seconds: 45));
    _ensureOk(res);
    final data = jsonDecode(res.body);
    final list = data is List ? data : [];
    return list.map((e) => Map<String, dynamic>.from(e as Map)).toList();
  }

  /// Branch customers search. Empty [text] uses server "null" token (all).
  Future<List<Map<String, dynamic>>> searchCustomers({
    String text = '',
    int delegateId = 0,
    String showType = 'الجميع',
  }) async {
    final q = text.trim().isEmpty ? 'null' : text.trim();
    final path =
        'Customers/Customers_GetAll/$delegateId&&${Uri.encodeComponent(q)}&&${Uri.encodeComponent(showType)}';
    final res = await http
        .get(Uri.parse('$_base$path'), headers: _authHeaders)
        .timeout(const Duration(seconds: 60));
    _ensureOk(res);
    final data = jsonDecode(res.body);
    final list = data is List ? data : [];
    return list.map((e) => Map<String, dynamic>.from(e as Map)).toList();
  }

  Future<Map<String, dynamic>?> customerInfoSimple(int customerId) async {
    final res = await http
        .get(
          Uri.parse('${_base}Customers/Customers_InfoSimple/$customerId'),
          headers: _authHeaders,
        )
        .timeout(const Duration(seconds: 30));
    if (res.statusCode == 404) return null;
    _ensureOk(res);
    final data = jsonDecode(res.body);
    if (data is Map) return Map<String, dynamic>.from(data);
    return null;
  }

  Future<List<Map<String, dynamic>>> customerNotes(int customerId) async {
    final res = await http
        .get(
          Uri.parse('${_base}CustomerDecisions/Notes/$customerId'),
          headers: _authHeaders,
        )
        .timeout(const Duration(seconds: 30));
    _ensureOk(res);
    final data = jsonDecode(res.body);
    final list = data is List ? data : [];
    return list.map((e) => Map<String, dynamic>.from(e as Map)).toList();
  }

  Future<Map<String, dynamic>> postCustomerNote({
    required int customerId,
    required String noteText,
  }) async {
    final res = await http
        .post(
          Uri.parse('${_base}CustomerDecisions/Notes'),
          headers: _authHeaders,
          body: jsonEncode({
            'customerID': customerId,
            'noteText': noteText.trim(),
          }),
        )
        .timeout(const Duration(seconds: 30));
    _ensureOk(res);
    final data = jsonDecode(res.body);
    return data is Map ? Map<String, dynamic>.from(data) : <String, dynamic>{};
  }

  Future<List<Map<String, dynamic>>> newSales({bool unreadOnly = false}) async {
    final uri = Uri.parse('${_base}branch-manager/new-sales').replace(
      queryParameters: {'unreadOnly': '$unreadOnly'},
    );
    final res = await http.get(uri, headers: _authHeaders).timeout(const Duration(seconds: 45));
    _ensureOk(res);
    final data = jsonDecode(res.body);
    final list = data is List ? data : [];
    return list.map((e) => Map<String, dynamic>.from(e as Map)).toList();
  }

  Future<int> newSalesUnreadCount() async {
    final res = await http
        .get(Uri.parse('${_base}branch-manager/new-sales/unread-count'), headers: _authHeaders)
        .timeout(const Duration(seconds: 20));
    _ensureOk(res);
    final data = jsonDecode(res.body);
    if (data is Map) {
      final c = data['count'] ?? data['Count'];
      if (c is int) return c;
      return int.tryParse('$c') ?? 0;
    }
    return 0;
  }

  Future<Map<String, dynamic>?> newSaleDetail(int saleId) async {
    final res = await http
        .get(Uri.parse('${_base}branch-manager/new-sales/$saleId'), headers: _authHeaders)
        .timeout(const Duration(seconds: 30));
    if (res.statusCode == 404) return null;
    _ensureOk(res);
    final data = jsonDecode(res.body);
    return data is Map ? Map<String, dynamic>.from(data) : null;
  }

  Future<void> markNewSaleRead(int saleId) async {
    final res = await http
        .post(
          Uri.parse('${_base}branch-manager/new-sales/$saleId/mark-read'),
          headers: _authHeaders,
          body: '{}',
        )
        .timeout(const Duration(seconds: 20));
    _ensureOk(res);
  }

  Future<int> markAllNewSalesRead() async {
    final res = await http
        .post(
          Uri.parse('${_base}branch-manager/new-sales/mark-all-read'),
          headers: _authHeaders,
          body: '{}',
        )
        .timeout(const Duration(seconds: 30));
    _ensureOk(res);
    final data = jsonDecode(res.body);
    if (data is Map) {
      final m = data['marked'] ?? data['Marked'];
      if (m is int) return m;
      return int.tryParse('$m') ?? 0;
    }
    return 0;
  }

  Future<Map<String, dynamic>> createSalesRequest({
    required String fullName,
    required String phone,
    String? province,
    String? address,
    String? notes,
    int? existingCustomerId,
  }) async {
    final res = await http
        .post(
          Uri.parse('${_base}branch-manager/sales-requests'),
          headers: _authHeaders,
          body: jsonEncode({
            if (existingCustomerId != null && existingCustomerId > 0)
              'existingCustomerId': existingCustomerId,
            'customer': {
              'fullName': fullName.trim(),
              'phone': phone.trim(),
              if ((province ?? '').trim().isNotEmpty) 'province': province!.trim(),
              if ((address ?? '').trim().isNotEmpty) 'address': address!.trim(),
            },
            if ((notes ?? '').trim().isNotEmpty) 'notes': notes!.trim(),
          }),
        )
        .timeout(const Duration(seconds: 30));
    _ensureOk(res);
    final data = jsonDecode(res.body);
    return data is Map ? Map<String, dynamic>.from(data) : <String, dynamic>{};
  }

  Future<void> createDelegatedManagerRequest({
    required String message,
    String? subject,
  }) async {
    final res = await http
        .post(
          Uri.parse('${_base}branch-manager/delegated-manager-requests'),
          headers: _authHeaders,
          body: jsonEncode({
            'message': message.trim(),
            if ((subject ?? '').trim().isNotEmpty) 'subject': subject!.trim(),
          }),
        )
        .timeout(const Duration(seconds: 30));
    _ensureOk(res);
  }

  Future<Map<String, dynamic>> statsCatalog() async {
    final res = await http
        .get(Uri.parse('${_base}branch-manager/stats/catalog'), headers: _authHeaders)
        .timeout(const Duration(seconds: 45));
    _ensureOk(res);
    final data = jsonDecode(res.body);
    return data is Map ? Map<String, dynamic>.from(data) : <String, dynamic>{};
  }

  Future<Map<String, dynamic>> statsTrend({
    required String scope,
    required int id,
    required DateTime from,
    required DateTime to,
  }) async {
    final uri = Uri.parse('${_base}branch-manager/stats/trend').replace(
      queryParameters: {
        'scope': scope,
        'id': '$id',
        'fromDate': from.toIso8601String().split('T').first,
        'toDate': to.toIso8601String().split('T').first,
      },
    );
    final res = await http.get(uri, headers: _authHeaders).timeout(const Duration(seconds: 60));
    _ensureOk(res);
    final data = jsonDecode(res.body);
    return data is Map ? Map<String, dynamic>.from(data) : <String, dynamic>{};
  }

  /// زبائن التقييم حسب النطاق والمستوى (اختياري) للفترة.
  Future<List<Map<String, dynamic>>> statsRatingCustomers({
    required String scope,
    required int id,
    required DateTime from,
    required DateTime to,
    String? level,
  }) async {
    final qp = <String, String>{
      'scope': scope,
      'id': '$id',
      'fromDate': from.toIso8601String().split('T').first,
      'toDate': to.toIso8601String().split('T').first,
    };
    if (level != null && level.trim().isNotEmpty) {
      qp['level'] = level.trim();
    }
    final uri = Uri.parse('${_base}branch-manager/stats/rating-customers')
        .replace(queryParameters: qp);
    final res = await http.get(uri, headers: _authHeaders).timeout(const Duration(seconds: 60));
    _ensureOk(res);
    final data = jsonDecode(res.body);
    if (data is List) {
      return data.map((e) => Map<String, dynamic>.from(e as Map)).toList();
    }
    if (data is Map) {
      final list = data['data'] ?? data['Data'];
      if (list is List) {
        return list.map((e) => Map<String, dynamic>.from(e as Map)).toList();
      }
    }
    return [];
  }

  Future<Map<String, dynamic>> statsOverview({
    required DateTime from,
    required DateTime to,
  }) async {
    // Kept for compatibility; prefer statsTrend.
    return statsTrend(scope: 'branch', id: 0, from: from, to: to);
  }

  Future<Map<String, dynamic>> createWaiver({
    required int customerId,
    String? customerName,
    String? customerPhone,
    required String note,
  }) async {
    final res = await http
        .post(
          Uri.parse('${_base}branch-manager/waivers'),
          headers: _authHeaders,
          body: jsonEncode({
            'customerId': customerId,
            if ((customerName ?? '').trim().isNotEmpty) 'customerName': customerName!.trim(),
            if ((customerPhone ?? '').trim().isNotEmpty) 'customerPhone': customerPhone!.trim(),
            'note': note.trim(),
          }),
        )
        .timeout(const Duration(seconds: 45));
    _ensureOk(res);
    final data = jsonDecode(res.body);
    return data is Map ? Map<String, dynamic>.from(data) : <String, dynamic>{};
  }

  Future<Map<String, dynamic>> listWaivers({
    String? status,
    int page = 1,
    int pageSize = 50,
  }) async {
    final qp = <String, String>{
      'page': '$page',
      'pageSize': '$pageSize',
    };
    if ((status ?? '').trim().isNotEmpty) {
      qp['status'] = status!.trim();
    }
    final uri = Uri.parse('${_base}branch-manager/waivers').replace(queryParameters: qp);
    final res = await http.get(uri, headers: _authHeaders).timeout(const Duration(seconds: 45));
    _ensureOk(res);
    final data = jsonDecode(res.body);
    return data is Map ? Map<String, dynamic>.from(data) : <String, dynamic>{};
  }

  void _ensureOk(http.Response res) {
    if (res.statusCode == 401) {
      throw BmApiException('انتهت الجلسة، سجّل الدخول مجدداً', statusCode: 401);
    }
    if (res.statusCode == 403) {
      throw BmApiException('ليست لديك صلاحية لهذا الإجراء', statusCode: 403);
    }
    if (res.statusCode < 200 || res.statusCode >= 300) {
      try {
        final j = jsonDecode(res.body);
        final msg = '${j['message'] ?? j['Message'] ?? res.body}';
        throw BmApiException(msg, statusCode: res.statusCode);
      } catch (e) {
        if (e is BmApiException) rethrow;
        throw BmApiException('خطأ من السيرفر (${res.statusCode})', statusCode: res.statusCode);
      }
    }
  }
}
