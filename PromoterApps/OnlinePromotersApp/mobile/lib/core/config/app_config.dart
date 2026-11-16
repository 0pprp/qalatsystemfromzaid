import 'package:flutter/foundation.dart';

/// Compile-time configuration (`--dart-define`).
///
/// ```
/// flutter run --dart-define=API_BASE_URL=http://192.168.1.10:5430/api/v1/
/// flutter build apk --release   # → http://promoters.alsaaeidy.com/api/v1/
/// ```
abstract final class AppConfig {
  static const String _apiBaseUrlDefine = String.fromEnvironment('API_BASE_URL');

  static const String debugDefaultBaseUrl = 'http://10.0.2.2:5430/api/v1/';

  /// The company servers are plain HTTP (like every field app); cleartext is allowed for alsaaeidy.com only.
  static const String prodDefaultBaseUrl = 'http://promoters.alsaaeidy.com/api/v1/';

  static String get apiBaseUrl {
    final value = _apiBaseUrlDefine.isNotEmpty ? _apiBaseUrlDefine : (kReleaseMode ? prodDefaultBaseUrl : debugDefaultBaseUrl);
    return value.endsWith('/') ? value : '$value/';
  }

  static const String androidPackageId = 'iq.qalaa.promoters_app';

  static const Duration connectTimeout = Duration(seconds: 15);
  static const Duration receiveTimeout = Duration(seconds: 30);
}
