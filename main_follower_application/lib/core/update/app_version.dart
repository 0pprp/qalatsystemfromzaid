import 'package:package_info_plus/package_info_plus.dart';

/// Installed build identity. Keep in sync with pubspec.yaml version.
class AppVersion {
  static const String name = '1.0.5';
  static const int code = 6;
  static const String appKey = 'main-follower';

  static int? _resolvedCode;
  static String? _resolvedName;

  static Future<void> ensureLoaded() async {
    if (_resolvedCode != null) return;
    try {
      final info = await PackageInfo.fromPlatform();
      _resolvedCode = int.tryParse(info.buildNumber) ?? code;
      _resolvedName = info.version.isEmpty ? name : info.version;
    } catch (_) {
      _resolvedCode = code;
      _resolvedName = name;
    }
  }

  static int get resolvedCode => _resolvedCode ?? code;
  static String get display => '${_resolvedName ?? name} (${_resolvedCode ?? code})';
}
