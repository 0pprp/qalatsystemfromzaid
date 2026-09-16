import 'dart:io';

import 'package:crypto/crypto.dart';
import 'package:delegated_manager_application/core/network/api_client.dart';
import 'package:delegated_manager_application/core/update/app_version.dart';
import 'package:delegated_manager_application/core/update/update_models.dart';
import 'package:delegated_manager_application/core/utils/json_read.dart';
import 'package:http/http.dart' as http;
import 'package:open_filex/open_filex.dart';
import 'package:path_provider/path_provider.dart';

class UpdateDownloadResult {
  const UpdateDownloadResult(
      {required this.ok, required this.message, this.filePath});

  final bool ok;
  final String message;
  final String? filePath;
}

/// Update check + verified APK download. Install is always user-confirmed by
/// the system installer; the app never installs silently.
class UpdateService {
  static const String appKey = AppVersion.appKey;

  static Future<MobileUpdateInfo> check({int? currentVersionCode}) async {
    if (currentVersionCode == null) {
      await AppVersion.loadInstalled();
    }

    final installed = currentVersionCode ?? AppVersion.code;
    final json = await ApiClient.get(
      'mobile-updates/$appKey',
      {'currentVersionCode': installed.toString()},
    );
    return MobileUpdateInfo.fromJson(
      JsonRead.map(json),
      currentVersionCode: installed,
    );
  }

  /// True when [bytes] hash to [expectedSha256]. An empty expectation means the
  /// release did not publish a digest, so verification cannot pass.
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
          ok: false, message: 'رابط التحديث غير متوفر');
    }
    if ((info.sha256 ?? '').length != 64) {
      return const UpdateDownloadResult(
          ok: false, message: 'بصمة التحديث غير متوفرة، تعذر التحقق من الملف');
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

      if (!matchesDigest(bytes, info.sha256)) {
        return const UpdateDownloadResult(
          ok: false,
          message: 'بصمة الملف لا تطابق الإصدار المنشور، تم إلغاء التحديث',
        );
      }

      final directory = await getTemporaryDirectory();
      final file = File(
        '${directory.path}${Platform.pathSeparator}'
        'delegated-manager-${info.latestVersionCode}.apk',
      );
      await file.writeAsBytes(bytes, flush: true);

      final opened = await OpenFilex.open(
        file.path,
        type: 'application/vnd.android.package-archive',
      );
      if (opened.type != ResultType.done) {
        return UpdateDownloadResult(
          ok: false,
          message: 'تم تنزيل الملف لكن تعذر بدء التثبيت: ${opened.message}',
          filePath: file.path,
        );
      }

      return UpdateDownloadResult(
        ok: true,
        message: 'تم التحقق من الملف، أكمل التثبيت من النظام',
        filePath: file.path,
      );
    } catch (_) {
      return const UpdateDownloadResult(
          ok: false, message: 'تعذر تنزيل التحديث');
    } finally {
      client.close();
    }
  }
}
