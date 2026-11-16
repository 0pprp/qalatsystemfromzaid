import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../data/models.dart';
import '../features/account/account_screen.dart';
import '../features/account/team_screen.dart';
import '../features/auth/auth_controller.dart';
import '../features/auth/login_screen.dart';
import '../features/home/home_screen.dart';
import '../features/materials/materials_screen.dart';
import '../features/notifications/notifications_screen.dart';
import '../features/orders/new_order_screen.dart';
import '../features/orders/order_detail_screen.dart';
import '../features/orders/orders_screen.dart';
import '../features/shell/shell_screens.dart';
import '../features/wallet/payout_chat_screen.dart';
import '../features/wallet/wallet_screen.dart';
import 'app_state.dart';

final rootNavigatorKey = GlobalKey<NavigatorState>();

/// A screen asked for by a notification tapped before the app finished starting.
String? pendingLaunchRoute;

/// Opens the screen of a tapped notification (kept for after start-up / sign-in when needed).
void openFromNotification(GoRouter router, String route) {
  final current = router.routerDelegate.currentConfiguration.uri.path;
  if (current.isEmpty || current == '/splash' || current == '/login') {
    pendingLaunchRoute = route;
    return;
  }
  router.push(route);
}

class _Refresh extends ChangeNotifier {
  void ping() => notifyListeners();
}

final routerProvider = Provider<GoRouter>((ref) {
  final refresh = _Refresh();
  ref.listen(authControllerProvider, (_, _) => refresh.ping());
  ref.listen(mustUpdateProvider, (_, _) => refresh.ping());
  ref.onDispose(refresh.dispose);

  final router = GoRouter(
    navigatorKey: rootNavigatorKey,
    initialLocation: '/splash',
    refreshListenable: refresh,
    redirect: (context, state) {
      final auth = ref.read(authControllerProvider);
      final path = state.uri.path;
      if (ref.read(mustUpdateProvider)) return path == '/force-update' ? null : '/force-update';
      if (path == '/force-update') return '/home';
      if (!auth.isKnown) return path == '/splash' ? null : '/splash';
      if (!auth.isLoggedIn) return path == '/login' ? null : '/login';
      if (path == '/login' || path == '/splash') {
        final pending = pendingLaunchRoute;
        if (pending != null) {
          pendingLaunchRoute = null;
          // Home first, then the notification's screen on top (so back returns to the app).
          WidgetsBinding.instance.addPostFrameCallback((_) {
            final ctx = rootNavigatorKey.currentContext;
            if (ctx != null) GoRouter.of(ctx).push(pending);
          });
        }
        return '/home';
      }
      return null;
    },
    routes: [
      GoRoute(path: '/', redirect: (_, _) => '/home'),
      GoRoute(path: '/splash', builder: (_, _) => const SplashScreen()),
      GoRoute(path: '/force-update', builder: (_, _) => const ForceUpdateScreen()),
      GoRoute(path: '/login', builder: (_, _) => const LoginScreen()),
      StatefulShellRoute.indexedStack(
        builder: (context, state, shell) => MainShell(shell: shell),
        branches: [
          StatefulShellBranch(routes: [GoRoute(path: '/home', builder: (_, _) => const HomeScreen())]),
          StatefulShellBranch(routes: [GoRoute(path: '/orders', builder: (_, _) => const OrdersScreen())]),
          StatefulShellBranch(routes: [GoRoute(path: '/wallet', builder: (_, _) => const WalletScreen())]),
          StatefulShellBranch(routes: [GoRoute(path: '/materials', builder: (_, _) => const MaterialsScreen())]),
          StatefulShellBranch(routes: [GoRoute(path: '/account', builder: (_, _) => const AccountScreen())]),
        ],
      ),
      GoRoute(
        path: '/new-order',
        parentNavigatorKey: rootNavigatorKey,
        builder: (_, s) => NewOrderScreen(initialProduct: s.extra is Product ? s.extra as Product : null),
      ),
      GoRoute(
        path: '/orders/:id',
        parentNavigatorKey: rootNavigatorKey,
        builder: (_, s) => OrderDetailScreen(orderId: int.tryParse(s.pathParameters['id'] ?? '') ?? 0),
      ),
      GoRoute(
        path: '/payouts/:id',
        parentNavigatorKey: rootNavigatorKey,
        builder: (_, s) => PayoutChatScreen(payoutId: int.tryParse(s.pathParameters['id'] ?? '') ?? 0),
      ),
      GoRoute(path: '/wallet/entries', parentNavigatorKey: rootNavigatorKey, builder: (_, _) => const EntriesScreen()),
      GoRoute(path: '/team', parentNavigatorKey: rootNavigatorKey, builder: (_, _) => const TeamScreen()),
      GoRoute(path: '/notifications', parentNavigatorKey: rootNavigatorKey, builder: (_, _) => const NotificationsScreen()),
    ],
    errorBuilder: (context, state) => Scaffold(
      appBar: AppBar(),
      body: Center(child: TextButton(onPressed: () => context.go('/home'), child: const Text('العودة للرئيسية'))),
    ),
  );
  ref.onDispose(router.dispose);
  return router;
});
