import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_client.dart';
import '../../../core/network/json.dart';
import '../../../core/providers.dart';
import '../../../core/theme/app_theme.dart';
import '../../auth/data/auth_controller.dart';

enum NotificationType {
  order('Order', Icons.receipt_long_rounded, AppColors.info),
  commission('Commission', Icons.account_balance_wallet_rounded, AppColors.success),
  promotion('Promotion', Icons.local_offer_rounded, AppColors.accent),
  network('Network', Icons.hub_rounded, AppColors.purple),
  system('System', Icons.notifications_rounded, AppColors.primary);

  const NotificationType(this.api, this.icon, this.color);

  final String api;
  final IconData icon;
  final Color color;

  static NotificationType fromApi(String? v) =>
      values.firstWhere((e) => e.api.toLowerCase() == v?.toLowerCase(), orElse: () => system);
}

class AppNotification {
  const AppNotification({
    required this.id,
    required this.type,
    required this.title,
    required this.body,
    this.data = const {},
    this.readAt,
    this.createdAt,
  });

  final int id;
  final NotificationType type;
  final String title;
  final String body;
  final Json data;
  final DateTime? readAt;
  final DateTime? createdAt;

  bool get isRead => readAt != null;

  AppNotification markRead() => AppNotification(
        id: id,
        type: type,
        title: title,
        body: body,
        data: data,
        readAt: DateTime.now(),
        createdAt: createdAt,
      );

  factory AppNotification.fromJson(Json j) => AppNotification(
        id: asInt(j['id']),
        type: NotificationType.fromApi(asStringOrNull(j['type'])),
        title: asString(j['title']),
        body: asString(j['body']),
        data: asJson(j['data']),
        readAt: asDate(j['readAt']),
        createdAt: asDate(j['createdAt']),
      );
}

class NotificationsRepository {
  NotificationsRepository(this._api);

  final ApiClient _api;

  Future<Paged<AppNotification>> list({int page = 1}) async =>
      Paged.fromJson(await _api.get('notifications', query: {'page': page}), AppNotification.fromJson);

  Future<void> markRead(int id) => _api.post('notifications/$id/read');

  Future<void> markAllRead() => _api.post('notifications/read-all');
}

final notificationsRepositoryProvider = Provider((ref) => NotificationsRepository(ref.watch(apiClientProvider)));

/// Unread badge for the bell icon. Reads `unreadCount` from the first page.
class UnreadCount extends Notifier<int> {
  @override
  int build() {
    final loggedIn = ref.watch(isLoggedInProvider);
    if (loggedIn) Future.microtask(refresh);
    return 0;
  }

  Future<void> refresh() async {
    if (!ref.read(isLoggedInProvider)) return;
    try {
      final page = await ref.read(notificationsRepositoryProvider).list(page: 1);
      state = asInt(page.extra['unreadCount'], page.items.where((e) => !e.isRead).length);
    } catch (_) {}
  }

  void set(int value) => state = value < 0 ? 0 : value;
}

final unreadCountProvider = NotifierProvider<UnreadCount, int>(UnreadCount.new);
