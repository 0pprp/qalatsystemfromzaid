import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/app_state.dart';
import '../../core/network/json.dart';
import '../../core/theme/app_theme.dart';
import '../../core/utils/formatters.dart';
import '../../core/widgets/paged_list.dart';
import '../../core/widgets/state_views.dart';
import '../../data/models.dart';
import '../../data/repository.dart';

class NotificationsScreen extends ConsumerStatefulWidget {
  const NotificationsScreen({super.key});

  @override
  ConsumerState<NotificationsScreen> createState() => _NotificationsScreenState();
}

class _NotificationsScreenState extends ConsumerState<NotificationsScreen> {
  int _version = 0;

  Future<void> _readAll() async {
    try {
      await ref.read(repositoryProvider).markAllRead();
      ref.read(unreadCountProvider.notifier).set(0);
      setState(() => _version++);
    } catch (e) {
      if (mounted) context.showError(e);
    }
  }

  void _open(AppNotification n) {
    if (!n.isRead) {
      ref.read(repositoryProvider).markRead(n.id).then((_) => ref.read(unreadCountProvider.notifier).refresh()).ignore();
    }
    final orderId = asIntOrNull(n.data['orderId']);
    final payoutId = asIntOrNull(n.data['payoutId']);
    if (orderId != null) {
      context.push('/orders/$orderId');
    } else if (payoutId != null) {
      context.push('/payouts/$payoutId');
    } else if (n.type == 'Wallet') {
      context.go('/wallet');
    }
  }

  IconData _icon(String type) => switch (type) {
        'Order' => Icons.receipt_long_rounded,
        'Wallet' => Icons.account_balance_wallet_rounded,
        'Payout' => Icons.outbox_rounded,
        'Account' => Icons.manage_accounts_rounded,
        _ => Icons.campaign_rounded,
      };

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('الإشعارات'),
        actions: [TextButton(onPressed: _readAll, child: const Text('قراءة الكل'))],
      ),
      body: PagedListView<AppNotification>(
        key: ValueKey(_version),
        fetch: (page) => ref.read(repositoryProvider).notifications(page: page),
        separator: 8,
        itemBuilder: (_, n, _) => Card(
          color: n.isRead ? null : AppColors.primaryLight.withValues(alpha: 0.5),
          child: ListTile(
            onTap: () => _open(n),
            leading: CircleAvatar(backgroundColor: Colors.white, child: Icon(_icon(n.type))),
            title: Text(n.title, style: TextStyle(fontWeight: n.isRead ? FontWeight.w600 : FontWeight.w800)),
            subtitle: Text('${n.body}\n${Fmt.relative(n.createdAt)}', style: const TextStyle(height: 1.5)),
            isThreeLine: true,
          ),
        ),
        empty: const EmptyState(icon: Icons.notifications_none_rounded, title: 'لا توجد إشعارات'),
      ),
    );
  }
}
