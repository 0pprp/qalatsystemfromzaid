/// Build-time environment for delegate_application.
///
/// Branch business APIs still use session `LinkDelegate`.
/// Central gateway calls (mobile updates) use [gatewayApiBase] and never
/// cross demo ↔ production hosts.
///
/// Build:
///   flutter build apk --release --dart-define=APP_ENV=demo
///   flutter build apk --release --dart-define=APP_ENV=production
library;

import 'package:flutter/foundation.dart';

class AppEnv {
  static const String name =
      String.fromEnvironment('APP_ENV', defaultValue: 'production');

  static const String _apiBaseUrlDefine =
      String.fromEnvironment('API_BASE_URL');
  static const String _apiBaseDefine = String.fromEnvironment('API_BASE');

  /// Demo branch API (LinkDelegate fallback / login catalog).
  static const String demoApiBaseUrl = 'http://169.58.236.52:8081/api/';

  /// Demo central sales gateway (updates + other central APIs).
  static const String demoGatewayApiBaseUrl =
      'http://169.58.236.52:8080/sales-gw/api/';

  static const String productionGatewayApiBaseUrl =
      'http://admincompany.alsaaeidy.com/sales-gw/api/';

  static const String demoHostMarker = '169.58.236.52';

  static bool get isDemo => name.toLowerCase() == 'demo';

  static bool get isProduction => !isDemo;

  static String get displayName => isDemo ? 'تجريبي' : 'إنتاج';

  /// Central gateway base for update checks (never mixes demo/prod).
  static String gatewayApiBase() => resolveGatewayApiBase(
        env: name,
        apiBaseDefine: _apiBaseDefine,
        apiBaseUrlDefine: _apiBaseUrlDefine,
      );

  static String resolveGatewayApiBase({
    required String env,
    String apiBaseDefine = '',
    String apiBaseUrlDefine = '',
  }) {
    if (env.trim().toLowerCase() == 'demo') {
      return normalizeBase(demoGatewayApiBaseUrl);
    }

    final explicit =
        apiBaseDefine.isNotEmpty ? apiBaseDefine : apiBaseUrlDefine;
    if (explicit.isNotEmpty && !isDemoHost(explicit)) {
      return normalizeBase(explicit);
    }

    return normalizeBase(productionGatewayApiBaseUrl);
  }

  static bool isDemoHost(String url) => url.contains(demoHostMarker);

  static String normalizeBase(String url) {
    var value = url.trim().replaceAll('\\', '/');
    if (value.isEmpty) return value;
    while (value.contains('/api/api')) {
      value = value.replaceAll('/api/api', '/api');
    }
    if (!value.endsWith('/')) value = '$value/';
    return value;
  }

  static void logIfDebug() {
    if (!kDebugMode) return;
    debugPrint('APP ENV: ${isDemo ? 'DEMO' : 'PRODUCTION'}');
    debugPrint('GATEWAY API BASE: ${gatewayApiBase()}');
    if (isDemo) {
      debugPrint('DEMO BRANCH API BASE: $demoApiBaseUrl');
    }
  }
}
