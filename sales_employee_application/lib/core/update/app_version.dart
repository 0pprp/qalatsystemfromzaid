import 'package:package_info_plus/package_info_plus.dart';

/// Installed build identity. Keep in sync with `pubspec.yaml` version.
class AppVersion {
  static const String name = '1.0.3';
  static const int code = 4;
  static const String appKey = 'sales-employee';

  static int? _resolvedCode;
  static String? _resolvedName;

  static String get display => '${_resolvedName ?? name} (${_resolvedCode ?? code})';

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
