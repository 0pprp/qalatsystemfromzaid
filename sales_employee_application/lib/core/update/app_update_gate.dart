import 'package:flutter/material.dart';
import 'package:sales_employee_application/core/update/app_version.dart';
import 'package:sales_employee_application/core/update/mandatory_update_cache.dart';
import 'package:sales_employee_application/core/update/update_models.dart';
import 'package:sales_employee_application/core/update/update_rules.dart';
import 'package:sales_employee_application/core/update/update_service.dart';
import 'package:sales_employee_application/utils/app_theme.dart';

class AppUpdateGate {
  static bool _running = false;

  /// Returns `true` if the user may continue into the app (not blocked by mandatory update).
  static Future<bool> checkAndPrompt(BuildContext context) async {
    if (_running) return true;
    _running = true;
    try {
      await AppVersion.ensureLoaded();
      final installed = AppVersion.resolvedCode;
      await MandatoryUpdateCache.clearIfSatisfied(installed);

      MobileUpdateInfo? info;
      try {
        info = await UpdateService.check(currentVersionCode: installed);
        if (info != null) {
          await MandatoryUpdateCache.syncFromServer(
            info,
            installedVersionCode: installed,
          );
        }
      } catch (_) {
        info = null;
      }

      if (!context.mounted) return true;

      if (info != null && info.mandatory) {
        await _showMandatory(context, info);
        return false;
      }

      if (info != null && info.kind == MobileUpdateKind.optional) {
        await _showOptional(context, info);
        return true;
      }

      final cached = await MandatoryUpdateCache.read();
      if (MandatoryUpdatePolicy.shouldBlockFromCache(
        cache: cached,
        installedVersionCode: installed,
      )) {
        if (!context.mounted) return true;
        await _showMandatory(
          context,
          cached!.toUpdateInfo(currentVersionCode: installed),
        );
        return false;
      }
      return true;
    } finally {
      _running = false;
    }
  }

  static Future<void> _showOptional(BuildContext context, MobileUpdateInfo info) {
    return showDialog<void>(
      context: context,
      barrierDismissible: true,
      builder: (ctx) => Directionality(
        textDirection: TextDirection.rtl,
        child: AlertDialog(
          title: const Text(
            'يتوفر تحديث جديد',
            style: TextStyle(fontFamily: 'Cairo', fontWeight: FontWeight.bold),
          ),
          content: SingleChildScrollView(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(
                  'الإصدار ${info.latestVersionName}',
                  style: const TextStyle(fontFamily: 'Cairo'),
                ),
                if ((info.releaseNotes ?? '').isNotEmpty) ...[
                  const SizedBox(height: 8),
                  Text(
                    info.releaseNotes!,
                    style: const TextStyle(fontFamily: 'Cairo', fontSize: 13),
                  ),
                ],
              ],
            ),
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.of(ctx).pop(),
              child: const Text('لاحقاً', style: TextStyle(fontFamily: 'Cairo')),
            ),
            FilledButton(
              style: FilledButton.styleFrom(backgroundColor: AppTheme.primaryColor),
              onPressed: () async {
                Navigator.of(ctx).pop();
                if (info.canDownload) {
                  await UpdateService.openApkDownload(info.apkUrl!);
                }
              },
              child: const Text('تحديث الآن', style: TextStyle(fontFamily: 'Cairo')),
            ),
          ],
        ),
      ),
    );
  }

  static Future<void> _showMandatory(BuildContext context, MobileUpdateInfo info) {
    return showDialog<void>(
      context: context,
      barrierDismissible: false,
      builder: (ctx) => PopScope(
        canPop: false,
        child: Directionality(
          textDirection: TextDirection.rtl,
          child: AlertDialog(
            title: const Text(
              'يجب تحديث التطبيق للاستمرار',
              style: TextStyle(fontFamily: 'Cairo', fontWeight: FontWeight.bold),
            ),
            content: SingleChildScrollView(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text(
                    'الإصدار ${info.latestVersionName}',
                    style: const TextStyle(fontFamily: 'Cairo'),
                  ),
                  if ((info.releaseNotes ?? '').isNotEmpty) ...[
                    const SizedBox(height: 8),
                    Text(
                      info.releaseNotes!,
                      style: const TextStyle(fontFamily: 'Cairo', fontSize: 13),
                    ),
                  ],
                  const SizedBox(height: 12),
                  const Text(
                    'حدّث التطبيق ثم افتحه من جديد للمتابعة.',
                    style: TextStyle(fontFamily: 'Cairo', fontSize: 13),
                  ),
                ],
              ),
            ),
            actions: [
              FilledButton(
                style: FilledButton.styleFrom(backgroundColor: AppTheme.primaryColor),
                onPressed: () async {
                  if (info.canDownload) {
                    await UpdateService.openApkDownload(info.apkUrl!);
                  }
                },
                child: const Text('تحديث الآن', style: TextStyle(fontFamily: 'Cairo')),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
