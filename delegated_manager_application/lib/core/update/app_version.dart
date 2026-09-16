import 'package:package_info_plus/package_info_plus.dart';

class AppVersion {
  static const String appKey = 'delegated-manager';

  static String name = '-';
  static int code = 0;

  static String get display => '$name ($code)';

  static Future<void> loadInstalled() async {
    final info = await PackageInfo.fromPlatform();

    name = info.version.trim();

    code = int.tryParse(info.buildNumber.trim()) ?? 0;
  }
}
