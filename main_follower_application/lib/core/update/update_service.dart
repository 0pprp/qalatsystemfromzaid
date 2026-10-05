import 'dart:convert';
import 'dart:io';

import 'package:crypto/crypto.dart';
import 'package:flutter/services.dart';
import 'package:http/http.dart' as http;
import 'package:main_follower_application/core/config/app_env.dart';
import 'package:main_follower_application/core/update/app_version.dart';
import 'package:main_follower_application/core/update/update_models.dart';
import 'package:path_provider/path_provider.dart';
import 'package:url_launcher/url_launcher.dart';

class UpdateDownloadResult {
  const UpdateDownloadResult({
    required this.ok,
    required this.message,
    this.filePath,
    this.needsInstallPermission = false,
  });

  final bool ok;
  final String message;
  final String? filePath;
  final bool needsInstallPermission;
}

class UpdateService {
  static const String appKey = AppVersion.appKey;
  static const MethodChannel _installChannel =
      MethodChannel('main_follower/apk_install');

  static Future<MobileUpdateInfo?> check({int? currentVersionCode}) async {
    await AppVersion.ensureLoaded();
    final installed = currentVersionCode ?? AppVersion.resolvedCode;
    final base = AppEnv.apiBase();
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

  static bool matchesDigest(List<int> bytes, String? expectedSha256) {
    final expected = expectedSha256?.trim().toLowerCase() ?? '';
    if (expected.length != 64) return false;
    return sha256.convert(bytes).toString() == expected;
  }

  static Future<UpdateDownloadResult> downloadAndInstall(
    MobileUpdateInfo info, {
    void Function(double progress)? onProgress,
  }) async {
    if (!info.canDownload) {
      return const UpdateDownloadResult(
        ok: false,
        message: 'رابط التحديث غير متوفر',
      );
    }
    if ((info.sha256 ?? '').length != 64) {
      return const UpdateDownloadResult(
        ok: false,
        message: 'بصمة التحديث غير متوفرة، تعذر التحقق من الملف',
      );
    }

    final client = http.Client();
    try {
      final response =
          await client.send(http.Request('GET', Uri.parse(info.apkUrl!)));
      if (response.statusCode != 200) {
        return UpdateDownloadResult(
          ok: false,
          message: 'فشل تنزيل التحديث (${response.statusCode})',
        );
      }

      final total = response.contentLength ?? 0;
      final bytes = <int>[];
      await for (final chunk in response.stream) {
        bytes.addAll(chunk);
        if (total > 0) onProgress?.call(bytes.length / total);
      }
      onProgress?.call(1.0);

      if (!matchesDigest(bytes, info.sha256)) {
        return const UpdateDownloadResult(
          ok: false,
          message: 'بصمة الملف لا تطابق الإصدار المنشور، تم إلغاء التحديث',
        );
      }

      final directory = await getTemporaryDirectory();
      final file = File(
        '${directory.path}${Platform.pathSeparator}'
        'main-follower-${info.latestVersionCode}.apk',
      );
      await file.writeAsBytes(bytes, flush: true);
      return await openApkInstaller(file.path);
    } catch (_) {
      return const UpdateDownloadResult(
        ok: false,
        message: 'تعذر تنزيل التحديث',
      );
    } finally {
      client.close();
    }
  }

  static Future<UpdateDownloadResult> openApkInstaller(String filePath) async {
    try {
      final raw = await _installChannel.invokeMethod<dynamic>(
        'installApk',
        {'path': filePath},
      );
      if (raw is Map && raw['ok'] == true) {
        return UpdateDownloadResult(
          ok: true,
          message: 'opened',
          filePath: filePath,
        );
      }
      if (raw is Map && raw['code'] == 'need_permission') {
        return UpdateDownloadResult(
          ok: false,
          message: '${raw['message'] ?? 'يلزم إذن التثبيت'}',
          filePath: filePath,
          needsInstallPermission: true,
        );
      }
      final msg = raw is Map
          ? '${raw['message'] ?? 'تعذر فتح المثبت'}'
          : 'تعذر فتح المثبت';
      return UpdateDownloadResult(
        ok: false,
        message: msg,
        filePath: filePath,
      );
    } on PlatformException catch (e) {
      return UpdateDownloadResult(
        ok: false,
        message: 'تعذر فتح المثبت: ${e.message ?? e.code}',
        filePath: filePath,
      );
    } catch (e) {
      return UpdateDownloadResult(
        ok: false,
        message: 'تعذر فتح المثبت: $e',
        filePath: filePath,
      );
    }
  }

  static Future<bool> openApkInBrowser(String apkUrl) async {
    final uri = Uri.tryParse(apkUrl);
    if (uri == null) return false;
    return launchUrl(uri, mode: LaunchMode.externalApplication);
  }
}
