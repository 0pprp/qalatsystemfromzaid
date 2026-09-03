import 'package:flutter/foundation.dart';

class AppEnv {
  static const String name =
      String.fromEnvironment('APP_ENV', defaultValue: 'production');

  static const String demoApiBaseUrl = String.fromEnvironment(
    'API_BASE_URL',
    defaultValue: 'http://169.58.236.52:8080/api/',
  );
  static const String localApiBaseUrl = 'http://127.0.0.1:5280/api/';
  static const String productionApiBaseUrl = String.fromEnvironment(
    'API_BASE',
    defaultValue: 'http://127.0.0.1:5280/api/',
  );

  static bool get isDemo => name.toLowerCase() == 'demo';
  static bool get isLocal => name.toLowerCase() == 'local';

  static String apiBase() {
    if (isDemo) return _normalizeBase(demoApiBaseUrl);
    if (isLocal) return _normalizeBase(localApiBaseUrl);
    return _normalizeBase(productionApiBaseUrl);
  }

  /// Gateway lab city: Najaf DEMO branch API.
  static String loginCity() => 'najaf-demo';

  static String loginCityLabel() => 'النجف - DEMO';

  /// Debug UI can use mock. Release never does. Demo/local talk to the API.
  static bool get useMockSalesRepository {
    const flag = String.fromEnvironment('USE_SALES_MOCK', defaultValue: '');
    if (flag == 'true') return true;
    if (flag == 'false') return false;
    if (!kDebugMode) return false;
    if (isDemo || isLocal) return false;
    return true;
  }

  static void logIfDebug() {
    if (!kDebugMode) return;
    debugPrint('APP ENV: $name');
    debugPrint('API BASE URL: ${apiBase()}');
    debugPrint('LOGIN CITY: ${loginCity()}');
    debugPrint('SALES MOCK: $useMockSalesRepository');
  }

  static String _normalizeBase(String url) {
    var value = url.trim();
    if (value.isEmpty) return value;
    while (value.contains('/api/api')) {
      value = value.replaceAll('/api/api', '/api');
    }
    if (!value.endsWith('/')) value = '$value/';
    return value;
  }
}
