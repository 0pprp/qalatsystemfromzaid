import 'package:follower_application/core/update/app_version.dart';
import 'package:follower_application/core/update/mandatory_update_cache.dart';
import 'package:follower_application/core/update/update_models.dart';
import 'package:follower_application/core/update/update_rules.dart';
import 'package:follower_application/core/update/update_service.dart';
import 'package:follower_application/utils/AppTheme.dart';
import 'package:flutter/material.dart';

class AppUpdateGate {
  static bool _running = false;

  static Future<void> checkAndPrompt(BuildContext context) async {
    if (_running) return;
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

      if (!context.mounted) return;

      if (info != null && info.mandatory) {
        await _showMandatory(context, info);
        return;
      }

      if (info != null && info.kind == MobileUpdateKind.optional) {
        await _showOptional(context, info);
        return;
      }

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

class _DownloadDialogState extends State<_DownloadDialog>
    with WidgetsBindingObserver {
  double _progress = 0;
  String? _error;
  String _status = 'جارٍ التحضير...';
  bool _started = false;
  bool _waitingPermission = false;
  bool _installerLaunched = false;
  String? _apkPath;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
    WidgetsBinding.instance.addPostFrameCallback((_) => _start());
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    super.dispose();
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed &&
        _waitingPermission &&
        (_apkPath ?? '').isNotEmpty) {
      _resumeInstallAfterPermission();
    }
  }

  Future<void> _resumeInstallAfterPermission() async {
    final path = _apkPath;
    if (path == null || path.isEmpty) return;
    setState(() {
      _status = 'جاري فتح شاشة التثبيت...';
      _error = null;
    });
    final opened = await UpdateService.openApkInstaller(path);
    if (!mounted) return;
    _applyInstallResult(opened);
  }

  void _applyInstallResult(UpdateDownloadResult result) {
    if (result.ok) {
      setState(() {
        _waitingPermission = false;
        _installerLaunched = true;
        _error = null;
        _progress = 1;
        _status = 'أكمل التثبيت من شاشة النظام ثم افتح التطبيق';
        _apkPath = result.filePath ?? _apkPath;
      });
      if (!widget.mandatory) {
        Navigator.of(context).pop();
      }
      return;
    }

    if (result.needsInstallPermission) {
      setState(() {
        _waitingPermission = true;
        _installerLaunched = false;
        _error = null;
        _apkPath = result.filePath ?? _apkPath;
        _status =
            'فعّل السماح بتثبيت التطبيقات من مصدر «تطبيق مسؤول اللجنة» ثم ارجع للتطبيق — سيُفتح المثبت تلقائياً';
      });
      return;
    }

    setState(() {
      _waitingPermission = false;
      _installerLaunched = false;
      _error = result.message;
      _status = result.message;
      if ((result.filePath ?? '').isNotEmpty) {
        _apkPath = result.filePath;
      }
    });
  }

  Future<void> _start() async {
    if (_started) return;
    _started = true;
    setState(() {
      _error = null;
      _waitingPermission = false;
      _installerLaunched = false;
      _progress = 0;
      _status = 'جارٍ التنزيل...';
    });

    final result = await UpdateService.downloadAndInstall(
      widget.info,
      onProgress: (p) {
        if (!mounted) return;
        setState(() {
          _progress = p;
          if (p >= 1.0) {
            _status = 'اكتمل التنزيل — جاري فتح التثبيت...';
          } else {
            _status = '${(p * 100).clamp(0, 100).toStringAsFixed(0)}%';
          }
        });
      },
    );
    if (!mounted) return;
    _applyInstallResult(result);
  }

  Future<void> _openBrowserDownload() async {
    final url = widget.info.apkUrl;
    if (url == null || url.isEmpty) return;
    final ok = await UpdateService.openApkInBrowser(url);
    if (!mounted) return;
    if (!ok) {
      setState(() {
        _error = 'تعذر فتح رابط التنزيل في المتصفح';
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final title = _waitingPermission
        ? 'يلزم إذن التثبيت'
        : (_installerLaunched
            ? 'شاشة التثبيت'
            : (_error != null ? 'فشل التحديث' : 'جاري تنزيل التحديث'));

    return PopScope(
      canPop: !widget.mandatory || _installerLaunched || _waitingPermission,
      child: Directionality(
        textDirection: TextDirection.rtl,
        child: AlertDialog(
          title: Text(
            title,
            style: const TextStyle(fontFamily: 'Cairo'),
          ),
          content: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              if (!_installerLaunched &&
                  !_waitingPermission &&
                  _error == null)
                LinearProgressIndicator(
                  value: _progress <= 0 ? null : _progress.clamp(0.0, 1.0),
                  color: AppTheme.primaryColor,
                ),
              if (_waitingPermission || _installerLaunched)
                Icon(
                  _waitingPermission
                      ? Icons.security
                      : Icons.system_update_alt,
                  size: 42,
                  color: _waitingPermission
                      ? Colors.orange.shade800
                      : const Color(0xFF2E7D32),
                ),
              const SizedBox(height: 12),
              Text(
                _error ?? _status,
                style: TextStyle(
                  fontFamily: 'Cairo',
                  color: _error == null ? Colors.black87 : Colors.red,
                  height: 1.5,
                ),
                textAlign: TextAlign.center,
              ),
            ],
          ),
          actions: [
            TextButton(
              onPressed: _openBrowserDownload,
              child: const Text(
                'تنزيل من المتصفح',
                style: TextStyle(fontFamily: 'Cairo'),
              ),
            ),
            if (_error != null)
              TextButton(
                onPressed: () {
                  setState(() {
                    _error = null;
                    _progress = 0;
                    _started = false;
                    _waitingPermission = false;
                    _installerLaunched = false;
                  });
                  _start();
                },
                child: const Text(
                  'إعادة المحاولة',
                  style: TextStyle(fontFamily: 'Cairo'),
                ),
              ),
            if (!widget.mandatory && !_installerLaunched && _error == null)
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
