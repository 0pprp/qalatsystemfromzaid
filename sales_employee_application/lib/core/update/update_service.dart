import 'dart:convert';

import 'package:http/http.dart' as http;
import 'package:sales_employee_application/config/app_env.dart';
import 'package:sales_employee_application/core/update/app_version.dart';
import 'package:sales_employee_application/core/update/update_models.dart';
import 'package:url_launcher/url_launcher.dart';

class UpdateService {
  static const String appKey = AppVersion.appKey;

  static Future<MobileUpdateInfo?> check({int? currentVersionCode}) async {
    await AppVersion.ensureLoaded();
    final installed = currentVersionCode ?? AppVersion.resolvedCode;
    final base = AppEnv.gatewayApiBase();
    final uri = Uri.parse('${base}mobile-updates/$appKey').replace(
      queryParameters: {'currentVersionCode': '$installed'},
    );

    final response = await http.get(uri).timeout(const Duration(seconds: 20));
    if (response.statusCode == 404) return null;
    if (response.statusCode != 200) {
      throw Exception('update-check-${response.statusCode}');
    }

    final decoded = jsonDecode(response.body);
    if (decoded is! Map) {
      throw Exception('update-check-invalid');
    }
    return MobileUpdateInfo.fromJson(
      Map<String, dynamic>.from(decoded),
      currentVersionCode: installed,
    );
  }

  /// Opens APK URL in external browser / downloader (no native install channel yet).
  static Future<bool> openApkDownload(String apkUrl) async {
    final uri = Uri.tryParse(apkUrl);
    if (uri == null) return false;
    return launchUrl(uri, mode: LaunchMode.externalApplication);
  }
}
