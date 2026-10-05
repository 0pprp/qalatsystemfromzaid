import 'package:flutter/foundation.dart';

class AppEnv {
  static const String name =
      String.fromEnvironment('APP_ENV', defaultValue: 'production');

  static const String _apiBaseUrlDefine =
      String.fromEnvironment('API_BASE_URL');
  static const String _apiBaseDefine = String.fromEnvironment('API_BASE');

  static const String demoApiBaseUrl = 'http://169.58.236.52:8081/api/';

  static const String demoGatewayApiBaseUrl =
      'http://169.58.236.52:8080/sales-gw/api/';

  static const String productionGatewayApiBaseUrl =
      'http://admincompany.alsaaeidy.com/sales-gw/api/';

  static const String demoHostMarker = '169.58.236.52';

  static bool get isDemo => name.toLowerCase() == 'demo';
  static bool get isLocal => name.toLowerCase() == 'local';

  static const String localApiBaseUrl = 'http://127.0.0.1:5080/api/';

  static String apiBase({String? fallback}) {
    if (isDemo) return demoApiBaseUrl;
    if (isLocal) return localApiBaseUrl;
    return fallback ?? '';
  }

  /// Central gateway for mobile-updates (never mixes demo/prod).
  static String gatewayApiBase() {
    if (isDemo) return _normalize(demoGatewayApiBaseUrl);
    final explicit =
        _apiBaseDefine.isNotEmpty ? _apiBaseDefine : _apiBaseUrlDefine;
    if (explicit.isNotEmpty && !explicit.contains(demoHostMarker)) {
      return _normalize(explicit);
    }
    return _normalize(productionGatewayApiBaseUrl);
  }

  static String _normalize(String url) {
    var value = url.trim().replaceAll('\\', '/');
    if (value.isEmpty) return value;
    if (!value.endsWith('/')) value = '$value/';
    return value;
  }

  static void logIfDebug() {
    if (!kDebugMode) return;
    debugPrint('APP ENV: $name');
    debugPrint('GATEWAY API BASE: ${gatewayApiBase()}');
  }
}
