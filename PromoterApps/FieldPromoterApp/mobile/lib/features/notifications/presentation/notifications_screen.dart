import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/network/json.dart';
import '../../../core/push/push_service.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/utils/formatters.dart';
import '../../../core/widgets/paged_list.dart';
import '../../../core/widgets/state_views.dart';
import '../data/notifications_repository.dart';

class NotificationsScreen extends ConsumerStatefulWidget {
  const NotificationsScreen({super.key});

  @override
  ConsumerState<NotificationsScreen> createState() => _NotificationsScreenState();
}

class _NotificationsScreenState extends ConsumerState<NotificationsScreen> {
  final Set<int> _readLocally = {};
  bool _allRead = false;
  final int _version = 0;

  Future<void> _open(AppNotification n) async {
    if (!n.isRead && !_allRead && !_readLocally.contains(n.id)) {
      setState(() => _readLocally.add(n.id));
      final unread = ref.read(unreadCountProvider);
      ref.read(unreadCountProvider.notifier).set(unread - 1);
      unawaited(ref.read(notificationsRepositoryProvider).markRead(n.id).catchError((_) {}));
    }
    final hasTarget = n.data['orderId'] != null;
    if (hasTarget && mounted) unawaited(context.push(routeForPushData(n.data)));
  }

  Future<void> _markAll() async {
    try {
      await ref.read(notificationsRepositoryProvider).markAllRead();
      ref.read(unreadCountProvider.notifier).set(0);
      setState(() => _allRead = true);
    } catch (e) {
      if (mounted) context.showError(e);
    }
  }

  @override
  Widget build(BuildContext context) {
    final unread = ref.watch(unreadCountProvider);
    return Scaffold(
      appBar: AppBar(
        title: const Text('الإشعارات'),
        actions: [
          if (unread > 0 && !_allRead) TextButton(onPressed: _markAll, child: const Text('قراءة الكل')),
        ],
      ),
      body: PagedListView<AppNotification>(
        key: ValueKey('notifications-$_version'),
        fetch: (page) => ref.read(notificationsRepositoryProvider).list(page: page),
        onLoaded: (p) {
          if (p.page == 1) ref.read(unreadCountProvider.notifier).set(asInt(p.extra['unreadCount'], unread));
        },
        onRefresh: () => setState(() {
          _readLocally.clear();
          _allRead = false;
        }),
        separator: 8,
        itemBuilder: (_, n, _) {
          final read = n.isRead || _allRead || _readLocally.contains(n.id);
          return Material(
            color: read ? Colors.white : AppColors.primaryLight.withValues(alpha: 0.6),
            borderRadius: BorderRadius.circular(16),
            child: InkWell(
              borderRadius: BorderRadius.circular(16),
              onTap: () => _open(n),
              child: Padding(
                padding: const EdgeInsets.all(14),
                child: Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    CircleAvatar(
                      backgroundColor: n.type.color.withValues(alpha: 0.14),
                      child: Icon(n.type.icon, color: n.type.color, size: 21),
                    ),
                    const SizedBox(width: 12),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Row(
                            children: [
                              Expanded(
                                child: Text(n.title, style: TextStyle(fontWeight: read ? FontWeight.w600 : FontWeight.w800)),
                              ),
                              if (!read)
                                Container(
                                  width: 9,
                                  height: 9,
                                  decoration: const BoxDecoration(color: AppColors.primary, shape: BoxShape.circle),
                                ),
                            ],
                          ),
                          const SizedBox(height: 3),
                          Text(n.body, style: const TextStyle(color: AppColors.textMuted, height: 1.6)),
                          const SizedBox(height: 4),
                          Text(Fmt.relative(n.createdAt), style: const TextStyle(color: AppColors.textMuted, fontSize: 11.5)),
                        ],
                      ),
                    ),
                  ],
                ),
              ),
            ),
          );
        },
        empty: const EmptyState(
          icon: Icons.notifications_none_rounded,
          title: 'لا توجد إشعارات',
          message: 'ستصلك هنا تحديثات طلباتك وأرباحك والعروض.',
        ),
      ),
    );
  }
}
