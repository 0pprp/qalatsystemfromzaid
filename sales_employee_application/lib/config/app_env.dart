import 'package:flutter/foundation.dart';
import 'package:sales_employee_application/services/session.dart';

class AppEnv {
  static const String name =
      String.fromEnvironment('APP_ENV', defaultValue: 'production');

  static const String _apiBaseUrlDefine = String.fromEnvironment('API_BASE_URL');
  static const String _apiBaseDefine = String.fromEnvironment('API_BASE');
  static const String directorySearchKey =
      String.fromEnvironment('DIRECTORY_SEARCH_KEY');

  static const String demoHostFallback = 'http://169.58.236.52:8080/api/';
  static const String localApiBaseUrl = 'http://127.0.0.1:5280/api/';

  static String get demoApiBaseUrl =>
      _apiBaseUrlDefine.isNotEmpty ? _apiBaseUrlDefine : demoHostFallback;

  static bool get isDemo => name.toLowerCase() == 'demo';
  static bool get isLocal => name.toLowerCase() == 'local';
  static bool get isProduction => !isDemo && !isLocal;

  static String apiBase() {
    if (isDemo) return normalizeBase(demoApiBaseUrl);
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

  static String loginCity() {
    if (isDemo) return 'najaf-demo';
    return Session.branchValue ?? '';
  }

  static String loginCityLabel() {
    if (isDemo) return 'النجف - DEMO';
    final name = Session.branchName;
    if (name != null && name.isNotEmpty) return name;
    return Session.cityName;
  }

  /// Release never mocks. Demo/local talk to the API. Production never mocks
  /// unless USE_SALES_MOCK=true is passed explicitly.
  static bool get useMockSalesRepository {
    const flag = String.fromEnvironment('USE_SALES_MOCK', defaultValue: '');
    if (flag == 'true') return true;
    if (flag == 'false') return false;
    return false;
  }

  static void logIfDebug() {
    if (!kDebugMode) return;
    debugPrint('APP ENV: $name');
    debugPrint('API BASE URL: ${apiBase()}');
    debugPrint('LOGIN CITY: ${loginCity()}');
    debugPrint('SALES MOCK: $useMockSalesRepository');
  }

  static bool _isDemoHost(String url) => url.contains('169.58.236.52');

  static String normalizeBase(String url) {
    var value = url.trim();
    if (value.isEmpty) return value;
    while (value.contains('/api/api')) {
      value = value.replaceAll('/api/api', '/api');
    }
    if (!value.endsWith('/')) value = '$value/';
    return value;
  }
}
