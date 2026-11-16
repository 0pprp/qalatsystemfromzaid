import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/app_state.dart';
import '../../core/providers.dart';
import '../../core/theme/app_theme.dart';
import '../../core/utils/launcher.dart';

class SplashScreen extends ConsumerWidget {
  const SplashScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    ref.watch(remoteConfigProvider);
    return Scaffold(
      backgroundColor: Colors.white,
      body: Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Image.asset('assets/images/logo.png', cacheWidth: 400, width: 140, height: 140),
            const SizedBox(height: 16),
            Text('مروّجو قلعة الضمان', style: Theme.of(context).textTheme.headlineSmall?.copyWith(color: AppColors.primary)),
            const SizedBox(height: 28),
            const SizedBox(width: 26, height: 26, child: CircularProgressIndicator(strokeWidth: 2.6)),
          ],
        ),
      ),
    );
  }
}

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
                Image.asset('assets/images/logo.png', cacheWidth: 400, width: 110),
                const SizedBox(height: 24),
                Text('يرجى تحديث التطبيق', style: Theme.of(context).textTheme.headlineSmall),
                const SizedBox(height: 12),
                Text(
                  'الإصدار $current لم يعد مدعوماً. حدّث التطبيق إلى ${cfg.latestVersion} للاستمرار.',
                  textAlign: TextAlign.center,
                  style: const TextStyle(color: AppColors.textMuted, height: 1.7),
                ),
                const Spacer(),
                FilledButton.icon(
                  style: FilledButton.styleFrom(minimumSize: const Size.fromHeight(54)),
                  onPressed: cfg.apkUrl == null ? null : () => Launch.url(context, cfg.apkUrl),
                  icon: const Icon(Icons.system_update_rounded),
                  label: const Text('تحديث الآن'),
                ),
                TextButton(onPressed: () => ref.invalidate(remoteConfigProvider), child: const Text('حدّثت التطبيق؟ تحقق مجدداً')),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

/// Bottom navigation: home, orders, wallet, marketing material, account.
class MainShell extends ConsumerWidget {
  const MainShell({super.key, required this.shell});

  final StatefulNavigationShell shell;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return Scaffold(
      body: shell,
      bottomNavigationBar: NavigationBar(
        selectedIndex: shell.currentIndex,
        onDestinationSelected: (i) {
          // Tabs stay alive in the shell: reload balances and orders whenever the promoter switches tab.
          if (i != shell.currentIndex) ref.read(dataVersionProvider.notifier).bump();
          shell.goBranch(i, initialLocation: i == shell.currentIndex);
        },
        destinations: const [
          NavigationDestination(icon: Icon(Icons.home_outlined), selectedIcon: Icon(Icons.home_rounded), label: 'الرئيسية'),
          NavigationDestination(icon: Icon(Icons.receipt_long_outlined), selectedIcon: Icon(Icons.receipt_long_rounded), label: 'طلباتي'),
          NavigationDestination(
              icon: Icon(Icons.account_balance_wallet_outlined), selectedIcon: Icon(Icons.account_balance_wallet_rounded), label: 'المحفظة'),
          NavigationDestination(icon: Icon(Icons.perm_media_outlined), selectedIcon: Icon(Icons.perm_media_rounded), label: 'المواد'),
          NavigationDestination(icon: Icon(Icons.person_outline_rounded), selectedIcon: Icon(Icons.person_rounded), label: 'حسابي'),
        ],
      ),
    );
  }
}

/// The bell with the unread count, for app bars.
class NotificationsBell extends ConsumerWidget {
  const NotificationsBell({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final unread = ref.watch(unreadCountProvider);
    return IconButton(
      tooltip: 'الإشعارات',
      onPressed: () => context.push('/notifications'),
      icon: Badge(
        isLabelVisible: unread > 0,
        label: Text(unread > 99 ? '99+' : '$unread'),
        child: const Icon(Icons.notifications_none_rounded),
      ),
    );
  }
}
