import 'package:delegated_manager_application/core/auth/session.dart';
import 'package:delegated_manager_application/core/config/app_env.dart';
import 'package:delegated_manager_application/core/network/api_client.dart';
import 'package:delegated_manager_application/core/theme/app_theme.dart';
import 'package:delegated_manager_application/core/update/app_version.dart';
import 'package:delegated_manager_application/core/update/update_models.dart';
import 'package:delegated_manager_application/core/update/update_rules.dart';
import 'package:delegated_manager_application/core/update/update_service.dart';
import 'package:delegated_manager_application/core/widgets/state_views.dart';
import 'package:delegated_manager_application/features/authentication/data/auth_repository.dart';
import 'package:delegated_manager_application/features/authentication/presentation/login_screen.dart';
import 'package:flutter/material.dart';

class SettingsScreen extends StatefulWidget {
  const SettingsScreen({super.key});

  @override
  State<SettingsScreen> createState() => _SettingsScreenState();
}

class _SettingsScreenState extends State<SettingsScreen> {
  MobileUpdateInfo? _update;
  bool _checking = false;
  bool _downloading = false;
  double _progress = 0;
  String? _message;

  Future<void> _checkUpdate() async {
    if (_checking) return;
    setState(() {
      _checking = true;
      _message = null;
    });
    try {
      final info = await UpdateService.check();
      if (!mounted) return;
      setState(() {
        _update = info;
        _message = MobileUpdateRules.arabic(info.kind);
      });
    } on ApiException catch (e) {
      if (!mounted) return;
      setState(() => _message = e.message);
    } finally {
      if (mounted) setState(() => _checking = false);
    }
  }

  Future<void> _install() async {
    final info = _update;
    if (info == null || _downloading) return;
    setState(() {
      _downloading = true;
      _progress = 0;
    });
    final result = await UpdateService.downloadAndInstall(
      info,
      onProgress: (value) {
        if (mounted) setState(() => _progress = value);
      },
    );
    if (!mounted) return;
    setState(() {
      _downloading = false;
      _message = result.message;
    });
  }

  Future<void> _logout() async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('تسجيل الخروج'),
        content: const Text('هل تريد تسجيل الخروج من التطبيق؟'),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(context).pop(false),
            child: const Text('إلغاء'),
          ),
          FilledButton(
            style: FilledButton.styleFrom(backgroundColor: AppColors.rejected),
            onPressed: () => Navigator.of(context).pop(true),
            child: const Text('خروج'),
          ),
        ],
      ),
    );
    if (confirmed != true) return;
    await AuthRepository.logout();
    if (!mounted) return;
    Navigator.of(context).pushAndRemoveUntil(
      MaterialPageRoute(builder: (_) => const LoginScreen()),
      (route) => false,
    );
  }

  @override
  Widget build(BuildContext context) {
    final update = _update;
    return Scaffold(
      appBar: AppBar(title: const Text('الإعدادات')),
      body: SafeArea(
        child: ListView(
          padding: const EdgeInsets.all(AppSpacing.md),
          children: [
            SectionCard(
              title: 'الحساب',
              children: [
                InfoRow(
                  label: 'المستخدم',
                  value: Session.userName ?? '-',
                ),
                InfoRow(
                  label: 'الصفة',
                  value: Session.userType ?? 'مدير مفوض',
                ),
                const InfoRow(label: 'النطاق', value: 'كل المحافظات'),
              ],
            ),
            const SizedBox(height: AppSpacing.md),
            SectionCard(
              title: 'عن التطبيق',
              children: [
                const InfoRow(
                    label: 'التطبيق', value: 'تطبيق المدير المفوض'),
                InfoRow(label: 'الإصدار', value: AppVersion.display),
                InfoRow(label: 'البيئة', value: AppEnv.displayName),
                InfoRow(label: 'بوابة المبيعات', value: ApiClient.resolveBase()),
              ],
            ),
            const SizedBox(height: AppSpacing.md),
            SectionCard(
              title: 'تحديث التطبيق',
              children: [
                if (update != null) ...[
                  InfoRow(
                    label: 'أحدث إصدار',
                    value:
                        '${update.latestVersionName} (${update.latestVersionCode})',
                  ),
                  InfoRow(
                    label: 'الحالة',
                    value: MobileUpdateRules.arabic(update.kind),
                  ),
                  if ((update.releaseNotes ?? '').isNotEmpty)
                    InfoRow(
                        label: 'ملاحظات الإصدار',
                        value: update.releaseNotes!),
                  if (update.mandatory)
                    const Padding(
                      padding: EdgeInsets.symmetric(vertical: AppSpacing.xs),
                      child: StatusChip(
                          label: 'تحديث إلزامي', color: AppColors.rejected),
                    ),
                ],
                if (_message != null)
                  Padding(
                    padding: const EdgeInsets.symmetric(vertical: AppSpacing.xs),
                    child: Text(
                      _message!,
                      style: const TextStyle(
                          fontSize: 13, color: AppColors.muted),
                    ),
                  ),
                if (_downloading) ...[
                  const SizedBox(height: AppSpacing.xs),
                  LinearProgressIndicator(
                      value: _progress == 0 ? null : _progress),
                  const SizedBox(height: AppSpacing.xs),
                  Text(
                    'جارٍ التنزيل ${(_progress * 100).toStringAsFixed(0)}%',
                    style: const TextStyle(
                        fontSize: 12, color: AppColors.muted),
                  ),
                ],
                const SizedBox(height: AppSpacing.sm),
                OutlinedButton.icon(
                  onPressed: _checking ? null : _checkUpdate,
                  icon: const Icon(Icons.system_update_outlined),
                  label: Text(
                      _checking ? 'جارٍ التحقق...' : 'التحقق من التحديثات'),
                ),
                if (update != null && update.updateAvailable) ...[
                  const SizedBox(height: AppSpacing.xs),
                  FilledButton.icon(
                    onPressed:
                        _downloading || !update.canDownload ? null : _install,
                    icon: const Icon(Icons.download_outlined),
                    label: const Text('تنزيل وتثبيت التحديث'),
                  ),
                ],
              ],
            ),
            const SizedBox(height: AppSpacing.md),
            OutlinedButton.icon(
              style: OutlinedButton.styleFrom(
                foregroundColor: AppColors.rejected,
                minimumSize: const Size.fromHeight(48),
              ),
              onPressed: _logout,
              icon: const Icon(Icons.logout),
              label: const Text('تسجيل الخروج'),
            ),
          ],
        ),
      ),
    );
  }
}
