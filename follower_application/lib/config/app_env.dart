import 'package:flutter/foundation.dart';

class AppEnv {
  static const String name =
      String.fromEnvironment('APP_ENV', defaultValue: 'production');

  static const String demoApiBaseUrl = 'http://169.58.236.52:8081/api/';
  static const String localApiBaseUrl = 'http://127.0.0.1:5080/api/';

  static bool get isDemo => name.toLowerCase() == 'demo';
  static bool get isLocal => name.toLowerCase() == 'local';

  static String apiBase({String? fallback}) {
    if (isDemo) {
      return demoApiBaseUrl;
    }
    if (isLocal) {
      return localApiBaseUrl;
    }
    return fallback ?? '';
  }

  static void logIfDebug() {
    if (!kDebugMode) return;
    if (isDemo) {
      debugPrint('APP ENV: DEMO');
      debugPrint('API BASE URL: http://169.58.236.52:8081/api/');
    } else if (isLocal) {
      debugPrint('APP ENV: LOCAL');
      debugPrint('API BASE URL: http://127.0.0.1:5080/api/');
    }
  }
}
