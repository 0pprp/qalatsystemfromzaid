import 'dart:convert';
import 'dart:io';

import 'package:crypto/crypto.dart';
import 'package:delegate_application/config/app_env.dart';
import 'package:delegate_application/core/update/app_version.dart';
import 'package:delegate_application/core/update/update_models.dart';
import 'package:http/http.dart' as http;
import 'package:open_filex/open_filex.dart';
import 'package:path_provider/path_provider.dart';

class UpdateDownloadResult {
  const UpdateDownloadResult({
    required this.ok,
    required this.message,
    this.filePath,
  });

  final bool ok;
  final String message;
  final String? filePath;
}

/// Update check + SHA-256 verified APK download. Never silent-installs.
class UpdateService {
  static const String appKey = AppVersion.appKey;

  static Future<MobileUpdateInfo?> check({int? currentVersionCode}) async {
    final installed = currentVersionCode ?? AppVersion.code;
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

      if (!matchesDigest(bytes, info.sha256)) {
        return const UpdateDownloadResult(
          ok: false,
          message: 'بصمة الملف لا تطابق الإصدار المنشور، تم إلغاء التحديث',
        );
      }

      final directory = await getTemporaryDirectory();
      final file = File(
        '${directory.path}${Platform.pathSeparator}'
        'delegate-${info.latestVersionCode}.apk',
      );
      await file.writeAsBytes(bytes, flush: true);

      final opened = await OpenFilex.open(
        file.path,
        type: 'application/vnd.android.package-archive',
      );
      if (opened.type != ResultType.done) {
        return UpdateDownloadResult(
          ok: false,
          message:
              'تم تنزيل الملف لكن تعذر بدء التثبيت. فعّل تثبيت التطبيقات غير المعروفة إن لزم. ${opened.message}',
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
        ok: false,
        message: 'تعذر تنزيل التحديث',
      );
    } finally {
      client.close();
    }
  }
}
