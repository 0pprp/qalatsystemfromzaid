import 'package:flutter/foundation.dart';

/// Build-time environment for delegate_application.
/// Pass with: --dart-define=APP_ENV=demo
class AppEnv {
  static const String name =
      String.fromEnvironment('APP_ENV', defaultValue: 'production');

  static const String demoApiBaseUrl = 'http://169.58.236.52:8081/api/';

  static bool get isDemo => name.toLowerCase() == 'demo';

  static bool get isProduction => !isDemo;

  static void logIfDebug() {
    if (!kDebugMode) return;
    debugPrint('APP ENV: ${isDemo ? 'DEMO' : 'PRODUCTION'}');
    if (isDemo) {
      debugPrint('API BASE URL: $demoApiBaseUrl');
    }
  }
}
