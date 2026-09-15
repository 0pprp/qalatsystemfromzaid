import 'dart:convert';

import 'package:delegated_manager_application/core/auth/session.dart';
import 'package:delegated_manager_application/core/config/app_env.dart';
import 'package:http/http.dart' as http;

class ApiException implements Exception {
  ApiException(this.message, {this.statusCode});

  final String message;
  final int? statusCode;

  bool get isUnauthorized => statusCode == 401;
  bool get isConflict => statusCode == 409;

  @override
  String toString() => message;
}

/// Thin JSON client over the sales gateway. Sends the Bearer JWT on every call.
class ApiClient {
  static const Duration timeout = Duration(seconds: 25);

  /// `AppEnv` already decides whether the stored session base may be used, so
  /// demo builds can never fall back to a production host (or the reverse).
  static String resolveBase() => AppEnv.apiBase();

  static Map<String, String> headers() {
    final result = <String, String>{
      'Content-Type': 'application/json',
      'Accept': 'application/json',
    };
    final token = Session.token;
    if (token != null && token.isNotEmpty) {
      result['Authorization'] = 'Bearer $token';
    }
    return result;
  }

  static Uri uri(String path, [Map<String, String>? query]) {
    var relative = path.startsWith('/') ? path.substring(1) : path;
    while (relative.toLowerCase().startsWith('api/')) {
      relative = relative.substring(4);
    }
    final base = resolveBase();
    if (base.isEmpty) {
      throw ApiException('عنوان بوابة المبيعات غير مضبوط');
    }
    final cleaned = <String, String>{};
    query?.forEach((key, value) {
      if (value.isNotEmpty) cleaned[key] = value;
    });
    final parsed = Uri.parse('$base$relative');
    return cleaned.isEmpty ? parsed : parsed.replace(queryParameters: cleaned);
  }

  static String errorMessage(http.Response response) {
    try {
      final body = jsonDecode(response.body);
      if (body is Map && body['message'] != null) {
        return body['message'].toString();
      }
    } catch (_) {
      // fall through to status based messages
    }
    switch (response.statusCode) {
      case 401:
        return 'انتهت الجلسة، سجّل الدخول مجددًا';
      case 403:
        return 'غير مصرح بهذا الإجراء';
      case 404:
        return 'العنصر غير موجود';
      case 409:
        return 'تم اتخاذ القرار من مستخدم آخر';
    }
    if (response.statusCode >= 500) return 'خطأ في الخادم، حاول لاحقًا';
    return 'فشل الطلب (${response.statusCode})';
  }

  static dynamic _decode(http.Response response) {
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw ApiException(errorMessage(response),
          statusCode: response.statusCode);
    }
    if (response.body.isEmpty) return null;
    return jsonDecode(response.body);
  }

  static Future<dynamic> get(String path, [Map<String, String>? query]) async {
    try {
      final response =
          await http.get(uri(path, query), headers: headers()).timeout(timeout);
      return _decode(response);
    } on ApiException {
      rethrow;
    } catch (_) {
      throw ApiException('تعذر الاتصال بالخادم');
    }
  }

  static Future<dynamic> post(String path, {Object? body}) async {
    try {
      final response = await http
          .post(
            uri(path),
            headers: headers(),
            body: body == null ? null : jsonEncode(body),
          )
          .timeout(timeout);
      return _decode(response);
    } on ApiException {
      rethrow;
    } catch (_) {
      throw ApiException('تعذر الاتصال بالخادم');
    }
  }

  static Future<dynamic> put(String path, {Object? body}) async {
    try {
      final response = await http
          .put(
            uri(path),
            headers: headers(),
            body: body == null ? null : jsonEncode(body),
          )
          .timeout(timeout);
      return _decode(response);
    } on ApiException {
      rethrow;
    } catch (_) {
      throw ApiException('تعذر الاتصال بالخادم');
    }
  }

  /// Login goes straight to the resolved environment base: the stored session
  /// base is ignored so a stale demo/production value can never be reused.
  static Future<Map<String, dynamic>> loginDelegatedManager({
    required String userName,
    required String password,
  }) async {
    final base = AppEnv.apiBase();
    if (base.isEmpty) {
      throw ApiException('عنوان بوابة المبيعات غير مضبوط');
    }
    try {
      final response = await http
          .post(
            Uri.parse('${base}Auth/LoginDelegatedManager'),
            headers: const {
              'Content-Type': 'application/json',
              'Accept': 'application/json',
            },
            body: jsonEncode({'userName': userName, 'password': password}),
          )
          .timeout(const Duration(seconds: 45));
      final decoded = _decode(response);
      if (decoded is! Map) throw ApiException('استجابة غير متوقعة من الخادم');
      return Map<String, dynamic>.from(decoded);
    } on ApiException {
      rethrow;
    } catch (_) {
      throw ApiException('تعذر الاتصال بالخادم');
    }
  }
}
