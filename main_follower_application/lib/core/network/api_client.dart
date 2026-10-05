import 'dart:async';
import 'dart:convert';

import 'package:http/http.dart' as http;
import 'package:main_follower_application/core/auth/session.dart';
import 'package:main_follower_application/core/config/app_env.dart';

class ApiException implements Exception {
  ApiException(this.message, {this.statusCode});
  final String message;
  final int? statusCode;

  bool get isUnauthorized => statusCode == 401;

  @override
  String toString() => message;
}

class ApiClient {
  static const timeout = Duration(seconds: 25);

  /// Invoked once when any authenticated call returns HTTP 401 so the UI can
  /// clear a stale JWT (e.g. signed by a different gateway instance) and
  /// return to login.
  static Future<void> Function()? onUnauthorized;

  static bool _unauthorizedHandling = false;

  static Future<void> _notifyUnauthorized() async {
    if (_unauthorizedHandling) return;
    _unauthorizedHandling = true;
    try {
      final hook = onUnauthorized;
      if (hook != null) await hook();
    } finally {
      _unauthorizedHandling = false;
    }
  }

  static Map<String, String> headers() {
    final h = <String, String>{
      'Content-Type': 'application/json',
      'Accept': 'application/json',
    };
    final t = Session.token;
    if (t != null && t.isNotEmpty) h['Authorization'] = 'Bearer $t';
    return h;
  }

  static Uri uri(String path) {
    final base = AppEnv.apiBase();
    final rel = path.startsWith('/') ? path.substring(1) : path;
    return Uri.parse('$base$rel');
  }

  static String _errorMessage(http.Response res) {
    try {
      final body = jsonDecode(utf8.decode(res.bodyBytes));
      if (body is Map && body['message'] != null) {
        return body['message'].toString();
      }
    } catch (_) {}
    if (res.statusCode == 401) {
      return 'انتهت الجلسة، سجّل الدخول مجددًا';
    }
    if (res.statusCode == 404) {
      return 'غير موجود (404) — المسار غير متاح على بوابة المتابع الرئيسي';
    }
    return 'خطأ ${res.statusCode}';
  }

  static dynamic _decode(http.Response res) {
    final raw = utf8.decode(res.bodyBytes);
    dynamic json;
    try {
      json = raw.isEmpty ? null : jsonDecode(raw);
    } catch (_) {
      json = null;
    }
    if (res.statusCode >= 200 && res.statusCode < 300) return json;
    if (res.statusCode == 401) {
      unawaited(_notifyUnauthorized());
    }
    throw ApiException(_errorMessage(res), statusCode: res.statusCode);
  }

  static Future<Map<String, dynamic>> postJson(
    String path,
    Map<String, dynamic> body,
  ) async {
    final res = await http
        .post(uri(path), headers: headers(), body: jsonEncode(body))
        .timeout(timeout);
    return _decodeMap(res);
  }

  static Future<Map<String, dynamic>> getJson(String path) async {
    final res = await http.get(uri(path), headers: headers()).timeout(timeout);
    return _decodeMap(res);
  }

  /// Like [getJson] but allows HTTP 200/204 with empty/`null` body → null map.
  static Future<Map<String, dynamic>?> getJsonNullable(String path) async {
    final res = await http.get(uri(path), headers: headers()).timeout(timeout);
    if (res.statusCode == 204) return null;
    if (res.statusCode == 401) {
      unawaited(_notifyUnauthorized());
      throw ApiException('انتهت الجلسة، سجّل الدخول مجددًا', statusCode: 401);
    }
    if (res.statusCode < 200 || res.statusCode >= 300) {
      throw ApiException(_errorMessage(res), statusCode: res.statusCode);
    }
    final raw = utf8.decode(res.bodyBytes).trim();
    if (raw.isEmpty || raw == 'null') return null;
    final decoded = jsonDecode(raw);
    if (decoded == null) return null;
    if (decoded is Map<String, dynamic>) return decoded;
    if (decoded is Map) return Map<String, dynamic>.from(decoded);
    throw ApiException('استجابة غير متوقعة');
  }

  static Future<List<dynamic>> getList(String path) async {
    final res = await http.get(uri(path), headers: headers()).timeout(timeout);
    final decoded = _decode(res);
    if (decoded is List) return decoded;
    throw ApiException('استجابة غير متوقعة');
  }

  /// Binary download (KYC / sale documents). Returns bytes + content-type.
  static Future<(List<int> bytes, String contentType)> getBytes(String path) async {
    final h = <String, String>{'Accept': '*/*'};
    final t = Session.token;
    if (t != null && t.isNotEmpty) h['Authorization'] = 'Bearer $t';
    final res = await http.get(uri(path), headers: h).timeout(timeout);
    if (res.statusCode == 401) {
      unawaited(_notifyUnauthorized());
      throw ApiException('انتهت الجلسة، سجّل الدخول مجددًا', statusCode: 401);
    }
    if (res.statusCode < 200 || res.statusCode >= 300) {
      throw ApiException(_errorMessage(res), statusCode: res.statusCode);
    }
    final type = res.headers['content-type'] ?? 'application/octet-stream';
    return (res.bodyBytes, type);
  }

  static Map<String, dynamic> _decodeMap(http.Response res) {
    final json = _decode(res);
    if (json is Map<String, dynamic>) return json;
    if (json is Map) return Map<String, dynamic>.from(json);
    throw ApiException('استجابة غير متوقعة');
  }
}
