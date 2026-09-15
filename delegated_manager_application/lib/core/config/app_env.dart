import 'package:delegated_manager_application/core/auth/session.dart';
import 'package:flutter/foundation.dart';

/// Gateway targeting for تطبيق المدير المفوض.
///
/// Mirrors `sales_filter_application`: demo and production keep the
/// `sales-gw` path segment, and the two environments never leak into each
/// other (a demo host is never used in production and vice versa).
class AppEnv {
  static const String name =
      String.fromEnvironment('APP_ENV', defaultValue: 'production');

  static const String _apiBaseUrlDefine =
      String.fromEnvironment('API_BASE_URL');
  static const String _apiBaseDefine = String.fromEnvironment('API_BASE');

  static const String demoHostFallback =
      'http://169.58.236.52:8080/sales-gw/api/';
  static const String localApiBaseUrl = 'http://127.0.0.1:5280/api/';
  static const String productionHostFallback =
      'http://admincompany.alsaaeidy.com/sales-gw/api/';

  static const String demoHostMarker = '169.58.236.52';

  static bool isDemoEnv(String env) => env.trim().toLowerCase() == 'demo';
  static bool isLocalEnv(String env) => env.trim().toLowerCase() == 'local';

  static bool get isDemo => isDemoEnv(name);
  static bool get isLocal => isLocalEnv(name);
  static bool get isProduction => !isDemo && !isLocal;

  static String get displayName {
    if (isDemo) return 'تجريبي';
    if (isLocal) return 'محلي';
    return 'إنتاج';
  }

  static String apiBase() => resolveApiBase(
        env: name,
        apiBaseDefine: _apiBaseDefine,
        apiBaseUrlDefine: _apiBaseUrlDefine,
        sessionBase: Session.apiBase,
      );

  /// Pure resolver so environment targeting stays unit-testable.
  static String resolveApiBase({
    required String env,
    String apiBaseDefine = '',
    String apiBaseUrlDefine = '',
    String? sessionBase,
  }) {
    if (isDemoEnv(env)) return normalizeBase(demoHostFallback);
    if (isLocalEnv(env)) return normalizeBase(localApiBaseUrl);

    final explicit =
        apiBaseDefine.isNotEmpty ? apiBaseDefine : apiBaseUrlDefine;
    if (explicit.isNotEmpty && !isDemoHost(explicit)) {
      return normalizeBase(explicit);
    }

    if (sessionBase != null &&
        sessionBase.isNotEmpty &&
        !isDemoHost(sessionBase)) {
      return normalizeBase(sessionBase);
    }

    return normalizeBase(productionHostFallback);
  }

  static bool isDemoHost(String url) => url.contains(demoHostMarker);

  /// Keeps the gateway path (`sales-gw`) intact and only fixes duplicated
  /// `/api/api` segments plus a missing trailing slash.
  static String normalizeBase(String url) {
    var value = url.trim();
    if (value.isEmpty) return value;
    value = value.replaceAll('\\', '/');
    while (value.contains('/api/api')) {
      value = value.replaceAll('/api/api', '/api');
    }
    if (isDemoHost(value)) {
      return demoHostFallback;
    }
    if (!value.endsWith('/')) value = '$value/';
    if (value.endsWith(':8080/')) value = '${value}api/';
    return value;
  }

  static void logIfDebug() {
    if (!kDebugMode) return;
    debugPrint('APP ENV: $name');
    debugPrint('API BASE URL: ${apiBase()}');
  }
}
