import 'package:package_info_plus/package_info_plus.dart';

/// Installed build identity. Constants must match `pubspec.yaml` version.
class AppVersion {
  static const String name = '1.0.16';
  static const int code = 17;

  static const String appKey = 'follower';

  static int? _resolvedCode;
  static String? _resolvedName;

  static String get display => '${_resolvedName ?? name} (${_resolvedCode ?? code})';

  /// Prefer real package versionCode so update gate never sticks on a stale constant.
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
}
