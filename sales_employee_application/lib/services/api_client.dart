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
  static String get _base => AppEnv.apiBase();

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
    final base = _base;
    if (base.toLowerCase().endsWith('/api/') &&
        normalized.toLowerCase().startsWith('api/')) {
      normalized = normalized.substring(4);
    }
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
}
