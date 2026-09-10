import 'dart:convert';

import 'package:follower_application/config/app_env.dart';
import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';

class FollowerApiException implements Exception {
  FollowerApiException(this.message, {this.statusCode, this.body});
  final String message;
  final int? statusCode;
  final String? body;

  @override
  String toString() => message;
}

class FollowerTrackingApi {
  static Future<Map<String, String>> session() async {
    final prefs = await SharedPreferences.getInstance();
    return {
      'asyncId': prefs.getString('AsyncId') ?? '',
      'base': AppEnv.apiBase(fallback: prefs.getString('LinkDelegate') ?? ''),
    };
  }

  static String resolveBase() {
    return AppEnv.apiBase();
  }

  static String normalizeBase(String raw) {
    var value = raw.trim().replaceAll('\\', '/');
    if (value.isEmpty) return value;
    while (value.contains('/api/api')) {
      value = value.replaceAll('/api/api', '/api');
    }
    if (!value.endsWith('/')) value = '$value/';
    if (value.endsWith(':8080/')) value = '${value}api/';
    return value;
  }

  static Future<Uri> uri(String path, {Map<String, String>? extraQuery}) async {
    final s = await session();
    final base = normalizeBase(s['base'] ?? '');
    final merged = <String, String>{
      'asyncId': s['asyncId'] ?? '',
      if (extraQuery != null) ...extraQuery,
    };
    return Uri.parse('$base$path').replace(queryParameters: merged);
  }

  static Future<dynamic> get(String path) async {
    final response =
        await http.get(await uri(path)).timeout(const Duration(seconds: 20));
    return _decode(response);
  }

  static Future<dynamic> post(String path, {Map<String, dynamic>? body}) async {
    final response = await http
        .post(
          await uri(path),
          headers: const {
            'Content-Type': 'application/json',
            'Accept': 'application/json',
          },
          body: body == null ? null : json.encode(body),
        )
        .timeout(const Duration(seconds: 30));
    return _decode(response);
  }

  static dynamic _decode(http.Response response) {
    final text = response.body;
    dynamic decoded;
    if (text.isNotEmpty) {
      try {
        decoded = json.decode(text);
      } catch (_) {
        decoded = text;
      }
    }
    if (response.statusCode >= 200 && response.statusCode < 300) {
      return decoded;
    }
    final message = decoded is Map
        ? '${decoded['message'] ?? decoded['Message'] ?? text}'
        : text;
    throw FollowerApiException(
      message.isEmpty ? 'HTTP ${response.statusCode}' : message,
      statusCode: response.statusCode,
      body: text,
    );
  }
}
