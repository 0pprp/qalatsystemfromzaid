import 'dart:convert';

import 'package:http/http.dart' as http;
import 'package:sales_filter_application/config/app_env.dart';
import 'package:sales_filter_application/services/session.dart';

class ApiException implements Exception {
  ApiException(this.message, {this.statusCode});
  final String message;
  final int? statusCode;
  @override
  String toString() => message;
}

class ApiClient {
  static String resolveBase() {
    final session = Session.apiBase;
    if (session != null && session.isNotEmpty) {
      return AppEnv.normalizeBase(session);
    }
    return AppEnv.apiBase();
  }

  static String get _base => resolveBase();

  static Map<String, String> get _headers {
    final h = <String, String>{
      'Content-Type': 'application/json',
      'Accept': 'application/json',
    };
    final t = Session.token;
    if (t != null && t.isNotEmpty) h['Authorization'] = 'Bearer $t';
    return h;
  }

  static Uri _uri(String path, [Map<String, String>? query]) {
    var p = path.startsWith('/') ? path.substring(1) : path;
    while (p.toLowerCase().startsWith('api/')) {
      p = p.substring(4);
    }
    final base = _base;
    if (base.isEmpty) {
      throw ApiException('عنوان بوابة المبيعات غير مضبوط');
    }
    return Uri.parse('$base$p').replace(queryParameters: query);
  }

  static String _msg(http.Response r) {
    try {
      final body = jsonDecode(r.body);
      if (body is Map && body['message'] != null) return body['message'].toString();
    } catch (_) {}
    if (r.statusCode == 401) return 'انتهت الجلسة، سجّل الدخول مجددًا';
    if (r.statusCode == 403) return 'غير مصرح بهذا الإجراء';
    if (r.statusCode == 409) return 'تم تحديث الطلب من مستخدم آخر';
    if (r.statusCode >= 500) return 'خطأ في الخادم، حاول لاحقًا';
    return 'فشل الطلب (${r.statusCode})';
  }

  static Future<dynamic> get(String path, [Map<String, String>? query]) async {
    try {
      final r = await http.get(_uri(path, query), headers: _headers).timeout(const Duration(seconds: 25));
      if (r.statusCode < 200 || r.statusCode >= 300) throw ApiException(_msg(r), statusCode: r.statusCode);
      if (r.body.isEmpty) return null;
      return jsonDecode(r.body);
    } on ApiException {
      rethrow;
    } catch (_) {
      throw ApiException('تعذر الاتصال بالإنترنت');
    }
  }

  static Future<dynamic> post(String path, {Object? body}) async {
    try {
      final r = await http
          .post(_uri(path), headers: _headers, body: body == null ? null : jsonEncode(body))
          .timeout(const Duration(seconds: 25));
      if (r.statusCode < 200 || r.statusCode >= 300) throw ApiException(_msg(r), statusCode: r.statusCode);
      if (r.body.isEmpty) return null;
      return jsonDecode(r.body);
    } on ApiException {
      rethrow;
    } catch (_) {
      throw ApiException('تعذر الاتصال بالإنترنت');
    }
  }

  /// Central gateway login — never talks to BE_Company directly.
  static Future<Map<String, dynamic>> loginSalesFilter({
    required String userName,
    required String password,
  }) async {
    final base = AppEnv.apiBase();
    if (base.isEmpty) {
      throw ApiException('عنوان بوابة المبيعات غير مضبوط');
    }
    try {
      final r = await http
          .post(
            Uri.parse('${base}Auth/LoginSalesFilter'),
            headers: {'Content-Type': 'application/json', 'Accept': 'application/json'},
            body: jsonEncode({'userName': userName, 'password': password}),
          )
          .timeout(const Duration(seconds: 45));
      if (r.statusCode < 200 || r.statusCode >= 300) {
        throw ApiException(_msg(r), statusCode: r.statusCode);
      }
      final decoded = jsonDecode(r.body);
      if (decoded is! Map) throw ApiException('استجابة غير متوقعة');
      return Map<String, dynamic>.from(decoded);
    } on ApiException {
      rethrow;
    } catch (_) {
      throw ApiException('تعذر الاتصال بالإنترنت');
    }
  }
}
