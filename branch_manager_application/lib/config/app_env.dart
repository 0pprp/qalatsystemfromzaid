import 'package:flutter/foundation.dart';

class AppEnv {
  /// Production by default (same as sales employee / follower apps).
  /// Use `--dart-define=APP_ENV=demo` only for Najaf DEMO builds.
  static const String name =
      String.fromEnvironment('APP_ENV', defaultValue: 'production');

  /// Najaf DEMO company API (same as FE_Company demo).
  static const String demoApiBaseUrl = 'http://169.58.236.52:8080/api/';

  static const String productionGatewayApiBaseUrl =
      'http://admincompany.alsaaeidy.com/sales-gw/api/';

  static const String demoGatewayApiBaseUrl =
      'http://169.58.236.52:8080/sales-gw/api/';

  static const String citiesUrl = 'http://defaultdata.alsaaeidy.com/GetAdmin';

  static const String demoHostMarker = '169.58.236.52';

  static bool get isDemo => name.toLowerCase() == 'demo';

  static String gatewayApiBase() {
    if (isDemo) return _normalize(demoGatewayApiBaseUrl);
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
    debugPrint('APP_ENV=$name gateway=${gatewayApiBase()}');
  }
}
