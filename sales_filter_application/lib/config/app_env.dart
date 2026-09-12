import 'package:flutter/foundation.dart';
import 'package:sales_filter_application/services/session.dart';

/// Mirrors sales_employee_application gateway targeting.
class AppEnv {
  static const String name =
      String.fromEnvironment('APP_ENV', defaultValue: 'production');

  static const String _apiBaseUrlDefine = String.fromEnvironment('API_BASE_URL');
  static const String _apiBaseDefine = String.fromEnvironment('API_BASE');

  static const String demoHostFallback = 'http://169.58.236.52:8080/sales-gw/api/';
  static const String localApiBaseUrl = 'http://127.0.0.1:5280/api/';

  static bool get isDemo => name.toLowerCase() == 'demo';
  static bool get isLocal => name.toLowerCase() == 'local';
  static bool get isProduction => !isDemo && !isLocal;

  static String apiBase() {
    if (isDemo) return normalizeBase(demoHostFallback);
    if (isLocal) return normalizeBase(localApiBaseUrl);

    final explicit = _apiBaseDefine.isNotEmpty ? _apiBaseDefine : _apiBaseUrlDefine;
    if (explicit.isNotEmpty && !_isDemoHost(explicit)) {
      return normalizeBase(explicit);
    }

    final session = Session.apiBase;
    if (session != null && session.isNotEmpty) {
      return normalizeBase(session);
    }
    return '';
  }

  static void logIfDebug() {
    if (!kDebugMode) return;
    debugPrint('APP ENV: $name');
    debugPrint('API BASE URL: ${apiBase()}');
  }

  static bool _isDemoHost(String url) => url.contains('169.58.236.52');

  static String normalizeBase(String url) {
    var value = url.trim();
    if (value.isEmpty) return value;
    value = value.replaceAll('\\', '/');
    value = value.replaceAll(RegExp(r'/sales-gw/?', caseSensitive: false), '/');
    while (value.contains('/api/api')) {
      value = value.replaceAll('/api/api', '/api');
    }
    if (_isDemoHost(value)) {
      return demoHostFallback;
    }
    if (!value.endsWith('/')) value = '$value/';
    if (value.endsWith(':8080/')) value = '${value}api/';
    return value;
  }
}
