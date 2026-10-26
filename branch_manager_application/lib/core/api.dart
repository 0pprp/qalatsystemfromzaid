import 'dart:convert';
import 'dart:io';

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

class BranchLoginResult {
  BranchLoginResult({
    required this.token,
    required this.userType,
    required this.userName,
    required this.userId,
  });
  final String token;
  final String userType;
  final String userName;
  final String userId;
}

class BmDiscoverResult {
  BmDiscoverResult({
    required this.loginUserName,
    required this.displayName,
    required this.cities,
    this.gatewayToken,
    this.userId,
  });
  final String loginUserName;
  final String displayName;
  final List<BmAssignedCity> cities;

  /// Gateway JWT from `api/branch-manager-app/login` (assigned-tasks auth).
  final String? gatewayToken;
  final String? userId;
}

class BmAttachmentBytes {
  BmAttachmentBytes({
    required this.bytes,
    required this.fileName,
    this.contentType,
  });
  final List<int> bytes;
  final String fileName;
  final String? contentType;
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

  /// Bearer for gateway endpoints (`api/branch-manager/assigned-tasks`, …).
  Map<String, String> get _gatewayAuthHeaders {
    final t = BmSession.instance.gatewayToken;
    return {
      'Content-Type': 'application/json',
      'Accept': 'application/json',
      if ((t ?? '').isNotEmpty) 'Authorization': 'Bearer $t',
    };
  }

  String get _gatewayBase => AppEnv.gatewayApiBase();

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
    final result = await tryLoginBranch(
      apiBase: apiBase,
      userName: userName,
      password: password,
    );
    if (result == null) {
      throw BmApiException('فشل تسجيل الدخول');
    }
    if (result.userType != 'مدير فرع') {
      throw BmApiException('هذا التطبيق مخصص لحسابات إدارة الفرع فقط');
    }

    await BmSession.instance.saveCredentials(
      userName: result.userName,
      userType: result.userType,
      userId: result.userId,
      password: password,
      cities: [
        BmAssignedCity(
          name: cityName,
          link: BmSession.normalizeApiBase(apiBase),
          token: result.token,
        ),
      ],
    );
    await BmSession.instance.selectCity(
      BmSession.instance.assignedCities.first,
      token: result.token,
    );
  }

  /// Gate: only DM-created accounts may open the app. Then login to assigned cities.
  Future<BmDiscoverResult> discoverAssignedCities({
    required String userName,
    required String password,
  }) async {
    final gate = await loginBranchManagerAppGate(
      userName: userName,
      password: password,
    );
    final loginName =
        '${gate['userName'] ?? gate['UserName'] ?? userName}'.trim();
    final display =
        '${gate['displayName'] ?? gate['DisplayName'] ?? loginName}'.trim();
    final gatewayToken =
        '${gate['token'] ?? gate['Token'] ?? ''}'.trim();
    final gateUserId =
        '${gate['userId'] ?? gate['UserId'] ?? gate['UserID'] ?? ''}'.trim();

    final allowed = <BmAssignedCity>[];
    final rawCities = gate['cities'] ?? gate['Cities'] ?? [];
    if (rawCities is List) {
      for (final c in rawCities) {
        if (c is! Map) continue;
        final m = Map<String, dynamic>.from(c);
        final name = '${m['cityName'] ?? m['CityName'] ?? m['name'] ?? 'فرع'}';
        final link = '${m['link'] ?? m['Link'] ?? m['cityLink'] ?? ''}'.trim();
        if (link.isEmpty) continue;
        allowed.add(BmAssignedCity(
          name: name,
          link: BmSession.normalizeApiBase(link),
          value: m['cityValue']?.toString() ?? m['value']?.toString(),
          database: m['database']?.toString(),
        ));
      }
    }
    if (allowed.isEmpty) {
      throw BmApiException('لا توجد محافظات معيّنة لهذا الحساب من المدير المفوض');
    }

    // Branch accounts are provisioned during gate login — retry once if first pass is empty.
    var found = await _loginCities(allowed, loginName, password);
    if (found.isEmpty) {
      await Future<void>.delayed(const Duration(milliseconds: 700));
      found = await _loginCities(allowed, loginName, password);
    }

    return BmDiscoverResult(
      loginUserName: loginName.isNotEmpty ? loginName : userName.trim(),
      displayName: display.isNotEmpty ? display : loginName,
      cities: found.isNotEmpty ? found : allowed,
      gatewayToken: gatewayToken.isNotEmpty ? gatewayToken : null,
      userId: gateUserId.isNotEmpty ? gateUserId : null,
    );
  }

  /// Re-fetch gateway JWT when missing (e.g. legacy sessions before assigned-tasks).
  Future<String> ensureGatewayToken() async {
    final existing = (BmSession.instance.gatewayToken ?? '').trim();
    if (existing.isNotEmpty) return existing;

    final user = (BmSession.instance.userName ?? '').trim();
    final pass = BmSession.instance.password ?? '';
    if (user.isEmpty || pass.isEmpty) {
      throw BmApiException('انتهت الجلسة — أعد تسجيل الدخول');
    }
    final gate = await loginBranchManagerAppGate(
      userName: user,
      password: pass,
    );
    final token = '${gate['token'] ?? gate['Token'] ?? ''}'.trim();
    if (token.isEmpty) {
      throw BmApiException('تعذر الحصول على رمز الدخول للمهام');
    }
    await BmSession.instance.setGatewayToken(token);
    final uid = '${gate['userId'] ?? gate['UserId'] ?? gate['UserID'] ?? ''}'
        .trim();
    if (uid.isNotEmpty) {
      BmSession.instance.userId = uid;
    }
    return token;
  }

  Future<List<BmAssignedCity>> _loginCities(
    List<BmAssignedCity> cities,
    String userName,
    String password,
  ) async {
    final found = <BmAssignedCity>[];
    const chunk = 4;
    for (var i = 0; i < cities.length; i += chunk) {
      final slice = cities.skip(i).take(chunk).toList();
      final parts = await Future.wait(slice.map((c) async {
        final login = await tryLoginBranch(
          apiBase: c.link,
          userName: userName,
          password: password,
        );
        if (login == null || login.userType != 'مدير فرع') return null;
        return c.copyWith(token: login.token);
      }));
      for (final p in parts) {
        if (p != null) found.add(p);
      }
    }
    return found;
  }

  Future<Map<String, dynamic>> loginBranchManagerAppGate({
    required String userName,
    required String password,
  }) async {
    final res = await http
        .post(
          Uri.parse('${AppEnv.gatewayApiBase()}branch-manager-app/login'),
          headers: {
            'Content-Type': 'application/json',
            'Accept': 'application/json',
          },
          body: jsonEncode({
            'userName': userName.trim(),
            'password': password,
          }),
        )
        .timeout(const Duration(seconds: 60));
    if (res.statusCode == 401) {
      throw BmApiException(
        'هذا الحساب غير مفعّل لتطبيق إدارة الفرع. أنشئه من تطبيق المدير المفوض.',
        statusCode: 401,
      );
    }
    if (res.statusCode < 200 || res.statusCode >= 300) {
      try {
        final j = jsonDecode(res.body);
        throw BmApiException(
          '${j['message'] ?? j['Message'] ?? 'تعذر التحقق من الحساب'}',
          statusCode: res.statusCode,
        );
      } catch (e) {
        if (e is BmApiException) rethrow;
        throw BmApiException('تعذر التحقق من الحساب (${res.statusCode})',
            statusCode: res.statusCode);
      }
    }
    final data = jsonDecode(res.body);
    return data is Map ? Map<String, dynamic>.from(data) : <String, dynamic>{};
  }

  Future<BranchLoginResult?> tryLoginBranch({
    required String apiBase,
    required String userName,
    required String password,
  }) async {
    try {
      final uri =
          Uri.parse('${BmSession.normalizeApiBase(apiBase)}Users/Users_LoginAdmin');
      final res = await http
          .post(
            uri,
            headers: {
              'Content-Type': 'application/json',
              'Accept': 'application/json'
            },
            body: jsonEncode(
                {'userName': userName.trim(), 'password': password}),
          )
          .timeout(const Duration(seconds: 20));

      Map<String, dynamic> body = {};
      try {
        body = Map<String, dynamic>.from(jsonDecode(res.body) as Map);
      } catch (_) {}
      final tokenRaw = body['token'] ?? body['Token'];
      if (res.statusCode != 200 || tokenRaw == null) return null;

      final token = '$tokenRaw';
      final claims = BmSession.decodeJwtPayload(token);
      final userType =
          '${claims['UserType'] ?? claims['userType'] ?? ''}'.trim();
      return BranchLoginResult(
        token: token,
        userType: userType,
        userName:
            '${claims['UserName'] ?? claims['userName'] ?? userName}'.trim(),
        userId: '${claims['UserID'] ?? claims['userID'] ?? ''}',
      );
    } catch (_) {
      return null;
    }
  }

  Future<void> openAssignedCity(BmAssignedCity city) async {
    final user = (BmSession.instance.userName ?? '').trim();
    final pass = BmSession.instance.password ?? '';
    if (user.isEmpty || pass.isEmpty) {
      throw BmApiException('انتهت الجلسة — أعد تسجيل الدخول');
    }

    // Prefer cached token from discovery if still present.
    if ((city.token ?? '').isNotEmpty) {
      final claims = BmSession.decodeJwtPayload(city.token!);
      final type = '${claims['UserType'] ?? ''}'.trim();
      if (type == 'مدير فرع') {
        await BmSession.instance.selectCity(city, token: city.token!);
        return;
      }
    }

    BranchLoginResult? login = await tryLoginBranch(
      apiBase: city.link,
      userName: user,
      password: pass,
    );
    // One retry — account may have just been provisioned on the branch.
    if (login == null || login.userType != 'مدير فرع') {
      await Future<void>.delayed(const Duration(milliseconds: 800));
      login = await tryLoginBranch(
        apiBase: city.link,
        userName: user,
        password: pass,
      );
    }
    if (login == null || login.userType != 'مدير فرع') {
      throw BmApiException(
        'تعذر فتح محافظة ${city.name}. تأكد أن الحساب مُنشأ من المدير المفوض وأن كلمة المرور صحيحة، أو أعد حفظ المستخدم من حسابات إدارة الفرع.',
      );
    }
    await BmSession.instance.selectCity(city, token: login.token);
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

  /// Note to المدير (حيدر) — not المدير المفوض.
  Future<void> createDirectorNote({
    required String message,
    String? subject,
  }) async {
    final res = await http
        .post(
          Uri.parse('${_base}branch-manager/director-notes'),
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
    bool phantom = false,
  }) async {
    final qp = <String, String>{
      'scope': scope,
      'id': '$id',
      'fromDate': from.toIso8601String().split('T').first,
      'toDate': to.toIso8601String().split('T').first,
      'phantom': '$phantom',
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

  Future<Map<String, dynamic>> statsBranchMonth({
    required String scope,
    required int id,
  }) async {
    final uri = Uri.parse('${_base}branch-manager/stats/branch-month').replace(
      queryParameters: {
        'scope': scope,
        'id': '$id',
      },
    );
    final res = await http.get(uri, headers: _authHeaders).timeout(const Duration(seconds: 60));
    _ensureOk(res);
    final data = jsonDecode(res.body);
    return data is Map ? Map<String, dynamic>.from(data) : <String, dynamic>{};
  }

  Future<List<Map<String, dynamic>>> listDeclineAlerts({
    bool unreadOnly = false,
    bool refresh = true,
  }) async {
    final uri = Uri.parse('${_base}branch-manager/stats/decline-alerts').replace(
      queryParameters: {
        'unreadOnly': '$unreadOnly',
        'refresh': '$refresh',
      },
    );
    final res = await http.get(uri, headers: _authHeaders).timeout(const Duration(seconds: 90));
    _ensureOk(res);
    final data = jsonDecode(res.body);
    if (data is List) {
      return data.map((e) => Map<String, dynamic>.from(e as Map)).toList();
    }
    return [];
  }

  Future<Map<String, dynamic>> getDeclineAlert(int id) async {
    final res = await http
        .get(
          Uri.parse('${_base}branch-manager/stats/decline-alerts/$id'),
          headers: _authHeaders,
        )
        .timeout(const Duration(seconds: 30));
    _ensureOk(res);
    final data = jsonDecode(res.body);
    return data is Map ? Map<String, dynamic>.from(data) : <String, dynamic>{};
  }

  Future<void> markDeclineAlertRead(int id) async {
    final res = await http
        .post(
          Uri.parse('${_base}branch-manager/stats/decline-alerts/$id/read'),
          headers: _authHeaders,
        )
        .timeout(const Duration(seconds: 20));
    _ensureOk(res);
  }

  /// زبائن انخفض تقييمهم من ممتاز إلى سيء/متلكئ خلال 15 يوماً.
  Future<List<Map<String, dynamic>>> listDecliningCustomers({
    bool refresh = true,
  }) async {
    final uri = Uri.parse('${_base}branch-manager/customers/declining').replace(
      queryParameters: {
        'refresh': '$refresh',
      },
    );
    final res = await http.get(uri, headers: _authHeaders).timeout(const Duration(seconds: 120));
    _ensureOk(res);
    final data = jsonDecode(res.body);
    if (data is List) {
      return data.map((e) => Map<String, dynamic>.from(e as Map)).toList();
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

  // —— Branch visits (gateway) ——

  Future<Map<String, dynamic>> openBranchVisit({
    required String cityValue,
    String? cityName,
  }) async {
    await ensureGatewayToken();
    final res = await http
        .post(
          Uri.parse('${_gatewayBase}branch-manager/branch-visits/open'),
          headers: _gatewayAuthHeaders,
          body: jsonEncode({
            'cityValue': cityValue,
            if ((cityName ?? '').trim().isNotEmpty) 'cityName': cityName!.trim(),
          }),
        )
        .timeout(const Duration(seconds: 45));
    _ensureOk(res);
    final data = jsonDecode(res.body);
    return data is Map ? Map<String, dynamic>.from(data) : <String, dynamic>{};
  }

  Future<Map<String, dynamic>?> activeBranchVisit() async {
    await ensureGatewayToken();
    final res = await http
        .get(
          Uri.parse('${_gatewayBase}branch-manager/branch-visits/active'),
          headers: _gatewayAuthHeaders,
        )
        .timeout(const Duration(seconds: 30));
    _ensureOk(res);
    if (res.body.trim().isEmpty || res.body.trim() == 'null') return null;
    final data = jsonDecode(res.body);
    if (data == null) return null;
    return data is Map ? Map<String, dynamic>.from(data) : null;
  }

  Future<Map<String, dynamic>> saveBranchVisitRatings({
    required String visitId,
    required List<Map<String, dynamic>> ratings,
  }) async {
    await ensureGatewayToken();
    final res = await http
        .put(
          Uri.parse(
              '${_gatewayBase}branch-manager/branch-visits/$visitId/delegate-ratings'),
          headers: _gatewayAuthHeaders,
          body: jsonEncode({'ratings': ratings}),
        )
        .timeout(const Duration(seconds: 45));
    _ensureOk(res);
    // Mirror into company so المراقب board «تقييم لجنة الإشراف» lights up.
    try {
      final ratedBy = (BmSession.instance.greetingName.isNotEmpty)
          ? BmSession.instance.greetingName
          : (BmSession.instance.userName ?? '');
      final payload = {
        'ratings': ratings
            .map((r) => {
                  'listId': r['delegateId'] ?? r['ListId'] ?? r['listId'],
                  'score': r['score'] ?? r['Score'],
                  'note': r['note'] ?? r['Note'],
                  'ratedBy': ratedBy,
                })
            .toList(),
      };
      await http
          .post(
            Uri.parse('${_base}branch-manager/list-supervision-ratings'),
            headers: _authHeaders,
            body: jsonEncode(payload),
          )
          .timeout(const Duration(seconds: 30));
    } catch (_) {
      // Non-fatal: gateway ratings still saved.
    }
    final data = jsonDecode(res.body);
    return data is Map ? Map<String, dynamic>.from(data) : <String, dynamic>{};
  }

  Future<Map<String, dynamic>> departBranchVisit({
    required String visitId,
    required String notes,
    List<File>? files,
    List<int>? requiredDelegateIds,
  }) async {
    await ensureGatewayToken();
    final uri =
        Uri.parse('${_gatewayBase}branch-manager/branch-visits/$visitId/depart');
    final req = http.MultipartRequest('POST', uri);
    final t = BmSession.instance.gatewayToken;
    if ((t ?? '').isNotEmpty) {
      req.headers['Authorization'] = 'Bearer $t';
    }
    req.headers['Accept'] = 'application/json';
    req.fields['notes'] = notes;
    final ids = (requiredDelegateIds ?? const <int>[])
        .where((id) => id > 0)
        .toSet()
        .toList();
    if (ids.isNotEmpty) {
      req.fields['requiredDelegateIds'] = ids.join(',');
    }
    for (final f in files ?? const <File>[]) {
      if (!await f.exists()) continue;
      req.files.add(await http.MultipartFile.fromPath('files', f.path));
    }
    final streamed =
        await req.send().timeout(const Duration(seconds: 120));
    final res = await http.Response.fromStream(streamed);
    _ensureOk(res);
    final data = jsonDecode(res.body);
    return data is Map ? Map<String, dynamic>.from(data) : <String, dynamic>{};
  }

  /// Lists for rating (delegate lists) from branch stats catalog.
  Future<List<Map<String, dynamic>>> statsCatalogLists() async {
    final cat = await statsCatalog();
    final lists = cat['lists'] ?? cat['Lists'] ?? [];
    if (lists is! List) return [];
    return lists.map((e) => Map<String, dynamic>.from(e as Map)).toList();
  }

  /// Lists for BM visit 0–10 ratings — same set as المراقب متابعة المندوبين
  /// (payment within 40 days). Falls back to empty when endpoint unavailable.
  Future<List<Map<String, dynamic>>> visitRatingLists() async {
    try {
      final res = await http
          .get(
            Uri.parse('${_base}branch-manager/stats/visit-rating-lists'),
            headers: _authHeaders,
          )
          .timeout(const Duration(seconds: 45));
      _ensureOk(res);
      final data = jsonDecode(res.body);
      if (data is! List) return [];
      return data.map((e) => Map<String, dynamic>.from(e as Map)).toList();
    } on BmApiException catch (e) {
      // Older company builds without the endpoint — do not rate every DelegateState list.
      if (e.statusCode == 404) return [];
      rethrow;
    }
  }

  // —— Director assigned tasks (gateway) ——

  Future<List<Map<String, dynamic>>> assignedTasksInbox() async {
    await ensureGatewayToken();
    final city = (BmSession.instance.activeCityValue ?? '').trim();
    final uri = Uri.parse('${_gatewayBase}branch-manager/assigned-tasks/inbox')
        .replace(queryParameters: city.isEmpty ? null : {'cityValue': city});
    final res = await http
        .get(
          uri,
          headers: _gatewayAuthHeaders,
        )
        .timeout(const Duration(seconds: 45));
    _ensureOk(res);
    final data = jsonDecode(res.body);
    final list = data is List ? data : [];
    return list.map((e) => Map<String, dynamic>.from(e as Map)).toList();
  }

  Future<Map<String, dynamic>> acceptAssignedTask(String id) async {
    await ensureGatewayToken();
    final res = await http
        .post(
          Uri.parse('${_gatewayBase}branch-manager/assigned-tasks/$id/accept'),
          headers: _gatewayAuthHeaders,
          body: '{}',
        )
        .timeout(const Duration(seconds: 30));
    _ensureOk(res);
    final data = jsonDecode(res.body);
    return data is Map ? Map<String, dynamic>.from(data) : <String, dynamic>{};
  }

  Future<Map<String, dynamic>> rejectAssignedTask(
    String id, {
    required String note,
  }) async {
    final trimmed = note.trim();
    if (trimmed.isEmpty) {
      throw BmApiException('ملاحظة الرفض مطلوبة');
    }
    await ensureGatewayToken();
    final res = await http
        .post(
          Uri.parse('${_gatewayBase}branch-manager/assigned-tasks/$id/reject'),
          headers: _gatewayAuthHeaders,
          body: jsonEncode({'note': trimmed}),
        )
        .timeout(const Duration(seconds: 30));
    _ensureOk(res);
    final data = jsonDecode(res.body);
    return data is Map ? Map<String, dynamic>.from(data) : <String, dynamic>{};
  }

  Future<Map<String, dynamic>> completeAssignedTask(String id) async {
    await ensureGatewayToken();
    final res = await http
        .post(
          Uri.parse('${_gatewayBase}branch-manager/assigned-tasks/$id/complete'),
          headers: _gatewayAuthHeaders,
          body: '{}',
        )
        .timeout(const Duration(seconds: 30));
    _ensureOk(res);
    final data = jsonDecode(res.body);
    return data is Map ? Map<String, dynamic>.from(data) : <String, dynamic>{};
  }

  /// Downloads an assigned-task attachment as raw bytes (gateway).
  Future<BmAttachmentBytes> downloadAssignedTaskAttachment({
    required String taskId,
    required String fileId,
    String? preferredFileName,
  }) async {
    await ensureGatewayToken();
    final t = BmSession.instance.gatewayToken;
    final res = await http
        .get(
          Uri.parse(
            '${_gatewayBase}branch-manager/assigned-tasks/$taskId/attachments/$fileId',
          ),
          headers: {
            'Accept': '*/*',
            if ((t ?? '').isNotEmpty) 'Authorization': 'Bearer $t',
          },
        )
        .timeout(const Duration(seconds: 120));
    _ensureOk(res);
    var name = (preferredFileName ?? '').trim();
    if (name.isEmpty) {
      final cd = res.headers['content-disposition'] ??
          res.headers['Content-Disposition'] ??
          '';
      final m = RegExp(
        r'''filename\*?=(?:UTF-8''|")?([^";]+)''',
        caseSensitive: false,
      ).firstMatch(cd);
      name = (m?.group(1) ?? 'attachment').trim();
    }
    if (name.isEmpty) name = 'attachment';
    return BmAttachmentBytes(
      bytes: res.bodyBytes,
      fileName: name,
      contentType: res.headers['content-type'] ?? res.headers['Content-Type'],
    );
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

  Future<Map<String, dynamic>> listWrongSales({
    bool reviewed = false,
    int page = 1,
    int pageSize = 50,
  }) async {
    final uri = Uri.parse('${_base}branch-manager/wrong-sales').replace(queryParameters: {
      'reviewed': reviewed ? 'true' : 'false',
      'page': '$page',
      'pageSize': '$pageSize',
    });
    final res = await http.get(uri, headers: _authHeaders).timeout(const Duration(seconds: 45));
    _ensureOk(res);
    final data = jsonDecode(res.body);
    return data is Map ? Map<String, dynamic>.from(data) : <String, dynamic>{};
  }

  Future<Map<String, dynamic>> getWrongSale(String id) async {
    final res = await http
        .get(Uri.parse('${_base}branch-manager/wrong-sales/$id'), headers: _authHeaders)
        .timeout(const Duration(seconds: 45));
    _ensureOk(res);
    final data = jsonDecode(res.body);
    return data is Map ? Map<String, dynamic>.from(data) : <String, dynamic>{};
  }

  Future<Map<String, dynamic>> reviewWrongSale({
    required String id,
    required String note,
  }) async {
    final res = await http
        .post(
          Uri.parse('${_base}branch-manager/wrong-sales/$id/review'),
          headers: _authHeaders,
          body: jsonEncode({'note': note.trim()}),
        )
        .timeout(const Duration(seconds: 45));
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
