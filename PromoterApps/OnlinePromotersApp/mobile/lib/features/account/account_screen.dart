import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/app_state.dart';
import '../../core/providers.dart';
import '../../core/theme/app_theme.dart';
import '../../core/utils/launcher.dart';
import '../../core/widgets/common.dart';
import '../auth/auth_controller.dart';

class AccountScreen extends ConsumerWidget {
  const AccountScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final me = ref.watch(currentUserProvider);
    final cfg = ref.watch(configProvider);
    final version = ref.watch(packageInfoProvider).version;
    if (me == null) return const SizedBox();

    return Scaffold(
      appBar: AppBar(title: const Text('حسابي')),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          AppCard(
            child: Row(
              children: [
                CircleAvatar(
                  radius: 28,
                  backgroundColor: AppColors.primaryLight,
                  child: Text(me.username.characters.first.toUpperCase(),
                      style: const TextStyle(color: AppColors.primary, fontSize: 22, fontWeight: FontWeight.w800)),
                ),
                const SizedBox(width: 14),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(me.username, style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 18)),
                      Text(me.phone, textDirection: TextDirection.ltr, style: const TextStyle(color: AppColors.textMuted)),
                    ],
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(height: 16),
          Card(
            child: Column(
              children: [
                if (me.canCreateAccounts)
                  ListTile(
                    leading: const Icon(Icons.groups_rounded),
                    title: const Text('فريقي'),
                    subtitle: const Text('الحسابات التي أنشأتها وأرباحي منها'),
                    trailing: const Icon(Icons.chevron_left_rounded),
                    onTap: () => context.push('/team'),
                  ),
                ListTile(
                  leading: const Icon(Icons.receipt_rounded),
                  title: const Text('سجل الحركات'),
                  trailing: const Icon(Icons.chevron_left_rounded),
                  onTap: () => context.push('/wallet/entries'),
                ),
                ListTile(
                  leading: const Icon(Icons.notifications_none_rounded),
                  title: const Text('الإشعارات'),
                  trailing: const Icon(Icons.chevron_left_rounded),
                  onTap: () => context.push('/notifications'),
                ),
                if (cfg.supportPhone != null)
                  ListTile(
                    leading: const Icon(Icons.call_rounded),
                    title: const Text('اتصل بالدعم'),
                    subtitle: Text(cfg.supportPhone!, textDirection: TextDirection.ltr, textAlign: TextAlign.right),
                    onTap: () => Launch.call(context, cfg.supportPhone),
                  ),
                if (cfg.supportWhatsapp != null)
                  ListTile(
                    leading: const Icon(Icons.chat_rounded),
                    title: const Text('واتساب الدعم'),
                    onTap: () => Launch.whatsapp(context, cfg.supportWhatsapp!.startsWith('0') ? '964${cfg.supportWhatsapp!.substring(1)}' : cfg.supportWhatsapp),
                  ),
              ],
            ),
          ),
          const SizedBox(height: 16),
          OutlinedButton.icon(
            style: OutlinedButton.styleFrom(foregroundColor: AppColors.danger, side: const BorderSide(color: AppColors.danger)),
            onPressed: () async {
              if (await confirmDialog(context, title: 'تسجيل الخروج', message: 'هل تريد تسجيل الخروج من هذا الهاتف؟', confirm: 'خروج', destructive: true)) {
                await ref.read(authControllerProvider.notifier).logout();
              }
            },
            icon: const Icon(Icons.logout_rounded),
            label: const Text('تسجيل الخروج'),
          ),
          const SizedBox(height: 16),
          Center(child: Text('الإصدار $version', style: const TextStyle(color: AppColors.textMuted, fontSize: 12))),
        ],
      ),
    );
  }
}
