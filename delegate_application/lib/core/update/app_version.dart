/// Installed build identity. Keep in sync with `pubspec.yaml` version.
class AppVersion {
  static const String name = '1.0.0';
  static const int code = 19;

  static const String appKey = 'delegate';

  static String get display => '$name ($code)';
}
