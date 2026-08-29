import 'package:flutter/foundation.dart';

class AppEnv {
  static const String name =
      String.fromEnvironment('APP_ENV', defaultValue: 'production');

  static const String demoApiBaseUrl = 'http://169.58.236.52:8080/api/';

  static bool get isDemo => name.toLowerCase() == 'demo';

  static String apiBase({String? fallback}) {
    if (isDemo) {
      return demoApiBaseUrl;
    }
    return fallback ?? '';
  }

  static void logIfDebug() {
    if (kDebugMode && isDemo) {
      debugPrint('APP ENV: DEMO');
      debugPrint('API BASE URL: http://169.58.236.52:8080/api/');
    }
  }
}
