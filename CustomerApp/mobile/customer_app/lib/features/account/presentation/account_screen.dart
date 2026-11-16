import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/providers.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/utils/launcher.dart';
import '../../../core/widgets/common.dart';
import '../../../core/widgets/state_views.dart';
import '../../app_config/data/app_config_repository.dart';
import '../../auth/data/auth_controller.dart';
import '../../notifications/data/notifications_repository.dart';

/// حسابي — agent profile (managed by the administration), shortcuts, support, logout.
class AccountScreen extends ConsumerStatefulWidget {
  const AccountScreen({super.key});

  @override
  ConsumerState<AccountScreen> createState() => _AccountScreenState();
}

class _AccountScreenState extends ConsumerState<AccountScreen> {

  Future<void> _logout() async {
    final ok = await confirmDialog(context, title: 'تسجيل الخروج', message: 'هل تريد تسجيل الخروج من حسابك؟', confirm: 'خروج');
    if (!ok) return;
    await ref.read(authControllerProvider.notifier).logout();
    if (mounted) {
      context.go('/new');
      context.showSnack('تم تسجيل الخروج');
    }
  }

  @override
  Widget build(BuildContext context) {
    final user = ref.watch(currentUserProvider);
    final cfg = ref.watch(configProvider);
    final unread = ref.watch(unreadCountProvider);
    final version = ref.watch(packageInfoProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('حسابي')),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(16, 8, 16, 32),
        children: [
          if (user != null)
            AppCard(
              child: Row(
                children: [
                  CircleAvatar(
                    radius: 30,
                    backgroundColor: AppColors.primary,
                    child: Text(user.firstName.isEmpty ? '؟' : user.firstName.characters.first,
                        style: const TextStyle(color: Colors.white, fontSize: 24, fontWeight: FontWeight.w800)),
                  ),
                  const SizedBox(width: 14),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(user.displayName, style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 16)),
                        Text(user.phone, textDirection: TextDirection.ltr, style: const TextStyle(color: AppColors.textMuted)),
                        if (user.username != null)
                          Text('اسم المستخدم: ${user.username}', style: const TextStyle(color: AppColors.textMuted, fontSize: 12.5)),
                      ],
                    ),
                  ),
                ],
              ),
            ),
          const SizedBox(height: 16),
          _Group(children: [
            _Tile(
              icon: Icons.receipt_long_rounded,
              title: 'الطلبات',
              subtitle: 'طلبات البيع ومتابعة تسديدها',
              onTap: () => context.go('/orders'),
            ),
            _Tile(
              icon: Icons.map_rounded,
              title: 'الدوام',
              subtitle: 'بدء وإنهاء الدوام وخريطة اليوم',
              onTap: () => context.go('/work'),
            ),
            _Tile(
              icon: Icons.notifications_none_rounded,
              title: 'الإشعارات',
              trailing: user != null && unread > 0 ? Badge(label: Text('$unread')) : null,
              onTap: () => context.push('/notifications'),
            ),
          ]),
          const SizedBox(height: 16),
          _Group(children: [
            if (cfg.supportPhone != null)
              _Tile(
                icon: Icons.call_outlined,
                title: 'اتصل بالإدارة',
                subtitle: cfg.supportPhone,
                onTap: () => Launch.call(context, cfg.supportPhone),
              ),
            if (cfg.supportWhatsapp != null)
              _Tile(
                icon: Icons.chat_outlined,
                title: 'تواصل عبر واتساب',
                onTap: () => Launch.whatsapp(context, cfg.supportWhatsapp, text: 'مرحباً، أحتاج مساعدة بخصوص تطبيق المسوّقين'),
              ),
            _Tile(
              icon: Icons.info_outline_rounded,
              title: 'عن التطبيق',
              subtitle: 'الإصدار ${version.version}',
              onTap: () => context.push('/account/about'),
            ),
          ]),
          if (user != null) ...[
            const SizedBox(height: 16),
            _Group(children: [
              _Tile(icon: Icons.logout_rounded, title: 'تسجيل الخروج', color: AppColors.danger, onTap: _logout),
            ]),
          ],
        ],
      ),
    );
  }
}

class _Group extends StatelessWidget {
  const _Group({required this.children});

  final List<Widget> children;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Column(
        children: [
          for (var i = 0; i < children.length; i++) ...[
            children[i],
            if (i < children.length - 1) const Divider(indent: 56),
          ],
        ],
      ),
    );
  }
}

class _Tile extends StatelessWidget {
  const _Tile({required this.icon, required this.title, this.subtitle, this.onTap, this.trailing, this.color});

  final IconData icon;
  final String title;
  final String? subtitle;
  final VoidCallback? onTap;
  final Widget? trailing;
  final Color? color;

  @override
  Widget build(BuildContext context) {
    return ListTile(
      leading: Icon(icon, color: color ?? AppColors.primary),
      title: Text(title, style: TextStyle(fontWeight: FontWeight.w600, color: color)),
      subtitle: subtitle == null ? null : Text(subtitle!),
      trailing: trailing ?? const Icon(Icons.chevron_left_rounded, color: AppColors.textMuted),
      onTap: onTap,
    );
  }
}
