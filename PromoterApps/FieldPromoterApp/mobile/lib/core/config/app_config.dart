import 'package:flutter/foundation.dart';

/// Compile-time configuration, supplied with `--dart-define`.
///
/// ```
/// flutter run --dart-define=USE_FAKE_API=true
/// flutter run --dart-define=API_BASE_URL=http://192.168.1.10:5380/api/v1/
/// flutter build apk --release --dart-define=API_BASE_URL=http://agents.alsaaeidy.com/api/v1/
/// ```
abstract final class AppConfig {
  static const String _apiBaseUrlDefine = String.fromEnvironment('API_BASE_URL');

  /// Debug default targets the local backend through the Android emulator host alias.
  static const String debugDefaultBaseUrl = 'http://10.0.2.2:5380/api/v1/';
  /// The live agents API (plain HTTP, like every company app). A release build without API_BASE_URL must still reach it:
  /// 1.4.9 was built without the define and pointed at a placeholder host, so no phone could connect.
  static const String prodDefaultBaseUrl = 'http://agents.alsaaeidy.com/api/v1/';

  static String get apiBaseUrl {
    final value = _apiBaseUrlDefine.isNotEmpty
        ? _apiBaseUrlDefine
        : (kReleaseMode ? prodDefaultBaseUrl : debugDefaultBaseUrl);
    return value.endsWith('/') ? value : '$value/';
  }

  /// Serve realistic Arabic sample data from an in-memory fake backend (no network).
  static const bool useFakeApi = bool.fromEnvironment('USE_FAKE_API');

  /// Public web store (used for links shown to the user).
  static const String storeWebUrl =
      String.fromEnvironment('STORE_WEB_URL', defaultValue: 'https://store.qalaa.iq');

  static const String androidPackageId = 'iq.qalaa.customer_app';

  /// The app is shared as an APK (not through the stores). Fallback download page when the server sends none.
  static String get apkDownloadUrl => '$storeWebUrl/download';

  static const Duration connectTimeout = Duration(seconds: 15);
  static const Duration receiveTimeout = Duration(seconds: 30);
}
