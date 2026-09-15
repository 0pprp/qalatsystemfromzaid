import 'package:delegate_application/core/update/app_version.dart';
import 'package:delegate_application/core/update/mandatory_update_cache.dart';
import 'package:delegate_application/core/update/update_models.dart';
import 'package:delegate_application/core/update/update_rules.dart';
import 'package:delegate_application/core/update/update_service.dart';
import 'package:delegate_application/utils/AppTheme.dart';
import 'package:flutter/material.dart';

/// Shows optional/mandatory update UI.
/// Offline: blocks only when a previously trusted mandatory response was cached.
class AppUpdateGate {
  static bool _running = false;

  static Future<void> checkAndPrompt(BuildContext context) async {
    if (_running) return;
    _running = true;
    try {
      final installed = AppVersion.code;
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

      if (!context.mounted) return;

      if (info != null && info.mandatory) {
        await _showMandatory(context, info);
        return;
      }

      if (info != null && info.kind == MobileUpdateKind.optional) {
        await _showOptional(context, info);
        return;
      }

      // Network miss / 404: enforce cached mandatory only.
      final cached = await MandatoryUpdateCache.read();
      if (MandatoryUpdatePolicy.shouldBlockFromCache(
        cache: cached,
        installedVersionCode: installed,
      )) {
        if (!context.mounted) return;
        await _showMandatory(
          context,
          cached!.toUpdateInfo(currentVersionCode: installed),
        );
      }
    } finally {
      _running = false;
    }
  }

  static Future<void> _showOptional(
      BuildContext context, MobileUpdateInfo info) {
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
              style: FilledButton.styleFrom(
                backgroundColor: AppTheme.primaryColor,
              ),
              onPressed: () async {
                Navigator.of(ctx).pop();
                if (!context.mounted) return;
                await showDialog<void>(
                  context: context,
                  barrierDismissible: true,
                  builder: (_) =>
                      _DownloadDialog(info: info, mandatory: false),
                );
              },
              child: const Text(
                'تحديث الآن',
                style: TextStyle(fontFamily: 'Cairo'),
              ),
            ),
          ],
        ),
      ),
    );
  }

  static Future<void> _showMandatory(
      BuildContext context, MobileUpdateInfo info) {
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
                ],
              ),
            ),
            actions: [
              FilledButton(
                style: FilledButton.styleFrom(
                  backgroundColor: AppTheme.primaryColor,
                ),
                onPressed: () async {
                  await showDialog<void>(
                    context: ctx,
                    barrierDismissible: false,
                    builder: (_) =>
                        _DownloadDialog(info: info, mandatory: true),
                  );
                },
                child: const Text(
                  'تحديث الآن',
                  style: TextStyle(fontFamily: 'Cairo'),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _DownloadDialog extends StatefulWidget {
  const _DownloadDialog({required this.info, required this.mandatory});

  final MobileUpdateInfo info;
  final bool mandatory;

  @override
  State<_DownloadDialog> createState() => _DownloadDialogState();
}

class _DownloadDialogState extends State<_DownloadDialog> {
  double _progress = 0;
  String? _error;
  bool _started = false;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) => _start());
  }

  Future<void> _start() async {
    if (_started) return;
    _started = true;
    final result = await UpdateService.downloadAndInstall(
      widget.info,
      onProgress: (p) {
        if (mounted) setState(() => _progress = p);
      },
    );
    if (!mounted) return;
    if (!result.ok) {
      setState(() => _error = result.message);
      return;
    }
    if (!widget.mandatory) {
      Navigator.of(context).pop();
    }
  }

  @override
  Widget build(BuildContext context) {
    return PopScope(
      canPop: !widget.mandatory,
      child: Directionality(
        textDirection: TextDirection.rtl,
        child: AlertDialog(
          title: const Text(
            'جاري تنزيل التحديث',
            style: TextStyle(fontFamily: 'Cairo'),
          ),
          content: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              LinearProgressIndicator(
                value: _progress <= 0 && _error == null ? null : _progress,
                color: AppTheme.primaryColor,
              ),
              const SizedBox(height: 12),
              Text(
                _error ??
                    (_progress <= 0
                        ? 'جارٍ التحضير...'
                        : '${(_progress * 100).clamp(0, 100).toStringAsFixed(0)}%'),
                style: TextStyle(
                  fontFamily: 'Cairo',
                  color: _error == null ? Colors.black87 : Colors.red,
                ),
                textAlign: TextAlign.center,
              ),
            ],
          ),
          actions: [
            if (_error != null)
              TextButton(
                onPressed: () {
                  setState(() {
                    _error = null;
                    _progress = 0;
                    _started = false;
                  });
                  _start();
                },
                child: const Text(
                  'إعادة المحاولة',
                  style: TextStyle(fontFamily: 'Cairo'),
                ),
              ),
            if (!widget.mandatory)
              TextButton(
                onPressed: () => Navigator.of(context).pop(),
                child: const Text('إلغاء', style: TextStyle(fontFamily: 'Cairo')),
              ),
          ],
        ),
      ),
    );
  }
}
