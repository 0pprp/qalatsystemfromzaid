import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/config/app_config.dart';
import '../../../core/providers.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/utils/launcher.dart';
import '../../app_config/data/app_config_repository.dart';

/// Blocking screen when the installed version is below `minSupportedVersion`.
class ForceUpdateScreen extends ConsumerWidget {
  const ForceUpdateScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final cfg = ref.watch(configProvider);
    final current = ref.watch(packageInfoProvider).version;
    return PopScope(
      canPop: false,
      child: Scaffold(
        backgroundColor: Colors.white,
        body: SafeArea(
          child: Padding(
            padding: const EdgeInsets.all(28),
            child: Column(
              children: [
                const Spacer(),
                Image.asset('assets/images/logo.png', cacheWidth: 400, width: 120),
                const SizedBox(height: 24),
                Text('يلزم تحديث التطبيق', style: Theme.of(context).textTheme.headlineSmall),
                const SizedBox(height: 12),
                Text(
                  'هذا الإصدار ($current) لم يعد مدعوماً. يرجى تحديث التطبيق إلى الإصدار ${cfg.latestVersion} للاستمرار.',
                  textAlign: TextAlign.center,
                  style: const TextStyle(color: AppColors.textMuted, height: 1.7),
                ),
                const Spacer(),
                FilledButton.icon(
                  style: FilledButton.styleFrom(minimumSize: const Size.fromHeight(54)),
                  onPressed: () => Launch.url(context, cfg.apkUrl ?? AppConfig.apkDownloadUrl),
                  icon: const Icon(Icons.system_update_rounded),
                  label: const Text('تحديث الآن'),
                ),
                const SizedBox(height: 8),
                TextButton(
                  onPressed: () => ref.invalidate(remoteConfigProvider),
                  child: const Text('حدّثت التطبيق؟ تحقق مجدداً'),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
