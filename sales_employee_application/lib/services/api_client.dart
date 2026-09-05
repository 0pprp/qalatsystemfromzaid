import 'dart:convert';

import 'package:http/http.dart' as http;
import 'package:sales_employee_application/config/app_env.dart';
import 'package:sales_employee_application/services/session.dart';

class ApiException implements Exception {
  ApiException(this.message, {this.statusCode, this.body});
  final String message;
  final int? statusCode;
  final String? body;

  @override
  String toString() => message;
}

class ApiClient {
  static String get _base => resolveBase();

  /// Demo always uses published BE_Company :8080/api/. Never Session, Gateway, or /api/api.
  static String resolveBase() {
    if (AppEnv.isDemo) {
      return AppEnv.normalizeBase(AppEnv.demoHostFallback);
    }
    if (AppEnv.isLocal) {
      return AppEnv.apiBase();
    }
    final session = Session.apiBase;
    if (session != null && session.isNotEmpty) {
      return AppEnv.normalizeBase(session);
    }
    return AppEnv.apiBase();
  }

  static Map<String, String> get _headers {
    final headers = <String, String>{
      'Content-Type': 'application/json',
      'Accept': 'application/json',
    };
    final token = Session.token;
    if (token != null && token.isNotEmpty) {
      headers['Authorization'] = 'Bearer $token';
    }
    return headers;
  }

  static Uri _uri(String path, [Map<String, String>? query]) {
    var normalized = path.startsWith('/') ? path.substring(1) : path;
    normalized = normalized.replaceAll(RegExp(r'sales-gw/', caseSensitive: false), '');
    while (normalized.toLowerCase().startsWith('api/')) {
      normalized = normalized.substring(4);
    }
    final base = _base;
    return Uri.parse('$base$normalized').replace(queryParameters: query);
  }

  static dynamic _decode(http.Response response) {
    if (response.body.isEmpty) return null;
    try {
      return jsonDecode(response.body);
    } catch (_) {
      return response.body;
    }
  }

  static String _errorMessage(http.Response response) {
    final body = _decode(response);
    if (body is Map && body['message'] != null) {
      return body['message'].toString();
    }
    if (body is String && body.isNotEmpty) return body;
    return 'فشل الطلب (${response.statusCode})';
  }

  static Future<dynamic> get(String path, {Map<String, String>? query}) async {
    final response = await http
        .get(_uri(path, query), headers: _headers)
        .timeout(const Duration(seconds: 25));
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw ApiException(
        _errorMessage(response),
        statusCode: response.statusCode,
        body: response.body,
      );
    }
    return _decode(response);
  }

  static Future<dynamic> post(String path, {Object? body}) async {
    final response = await http
        .post(
          _uri(path),
          headers: _headers,
          body: body == null ? null : jsonEncode(body),
        )
        .timeout(const Duration(seconds: 40));
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw ApiException(
        _errorMessage(response),
        statusCode: response.statusCode,
        body: response.body,
      );
    }
    return _decode(response);
  }

  static Future<dynamic> postMultipart(
    String path, {
    required List<int> bytes,
    required String fileName,
    String field = 'file',
  }) async {
    final request = http.MultipartRequest('POST', _uri(path));
    final token = Session.token;
    if (token != null && token.isNotEmpty) {
      request.headers['Authorization'] = 'Bearer $token';
    }
    request.files.add(http.MultipartFile.fromBytes(field, bytes, filename: fileName));
    final streamed = await request.send().timeout(const Duration(seconds: 60));
    final response = await http.Response.fromStream(streamed);
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw ApiException(
        _errorMessage(response),
        statusCode: response.statusCode,
        body: response.body,
      );
    }
    return _decode(response);
  }

  static Future<List<int>> getBytes(String path) async {
    final headers = Map<String, String>.from(_headers)..remove('Content-Type');
    final response = await http
        .get(_uri(path), headers: headers)
        .timeout(const Duration(seconds: 60));
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw ApiException(
        _errorMessage(response),
        statusCode: response.statusCode,
        body: response.body,
      );
    }
    return response.bodyBytes;
  }

  /// GET against an absolute branch URL. Used for global customer search.
  /// Sends the employee's JWT only as proof of a logged-in sales user.
  /// Callers must never POST writes through this helper to a foreign branch.
  static Future<dynamic> getAbsolute(
    String absoluteUrl, {
    Map<String, String>? query,
    Duration timeout = const Duration(seconds: 5),
    bool withAuth = true,
  }) async {
    var uri = Uri.parse(absoluteUrl);
    if (query != null && query.isNotEmpty) {
      uri = uri.replace(queryParameters: {
        ...uri.queryParameters,
        ...query,
      });
    }
    final headers = <String, String>{
      'Accept': 'application/json',
    };
    if (withAuth) {
      final token = Session.token;
      if (token != null && token.isNotEmpty) {
        headers['Authorization'] = 'Bearer $token';
      }
    }
    if (AppEnv.directorySearchKey.isNotEmpty) {
      headers['X-Sales-Directory-Key'] = AppEnv.directorySearchKey;
    }
    final response = await http.get(uri, headers: headers).timeout(timeout);
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw ApiException(
        _errorMessage(response),
        statusCode: response.statusCode,
        body: response.body,
      );
    }
    return _decode(response);
  }
}

