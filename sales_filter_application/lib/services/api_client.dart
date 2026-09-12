import 'dart:convert';

import 'package:http/http.dart' as http;
import 'package:sales_filter_application/services/session.dart';

class ApiException implements Exception {
  ApiException(this.message, {this.statusCode});
  final String message;
  final int? statusCode;
  @override
  String toString() => message;
}

class ApiClient {
  static String get _base {
    var b = (Session.apiBase ?? '').trim();
    if (b.isEmpty) return '';
    if (!b.endsWith('/')) b = '$b/';
    return b;
  }

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
    return Uri.parse('$_base$p').replace(queryParameters: query);
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

  static Future<Map<String, dynamic>> login({
    required String baseUrl,
    required String userName,
    required String password,
  }) async {
    var base = baseUrl.trim();
    if (!base.endsWith('/')) base = '$base/';
    try {
      final r = await http
          .post(
            Uri.parse('${base}Users/Users_LoginEmployee'),
            headers: {'Content-Type': 'application/json', 'Accept': 'application/json'},
            body: jsonEncode({'userName': userName, 'password': password}),
          )
          .timeout(const Duration(seconds: 25));
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
