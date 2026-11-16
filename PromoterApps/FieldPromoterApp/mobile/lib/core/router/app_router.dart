import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../features/account/presentation/about_screen.dart';
import '../../features/account/presentation/account_screen.dart';
import '../../features/app_config/data/app_config_repository.dart';
import '../../features/auth/data/auth_controller.dart';
import '../../features/auth/presentation/login_screen.dart';
import '../../features/notifications/presentation/notifications_screen.dart';
import '../../features/orders/data/order_models.dart';
import '../../features/orders/presentation/new_order_screen.dart';
import '../../features/orders/presentation/order_detail_screen.dart';
import '../../features/orders/presentation/order_success_screen.dart';
import '../../features/orders/presentation/orders_screen.dart';
import '../../features/shell/presentation/force_update_screen.dart';
import '../../features/shell/presentation/main_shell.dart';
import '../../features/shell/presentation/splash_screen.dart';
import '../../features/work/data/prospects_repository.dart';
import '../../features/work/presentation/my_pins_screen.dart';
import '../../features/work/presentation/work_screen.dart';

final rootNavigatorKey = GlobalKey<NavigatorState>();

/// The app is for field agents only: every route except these needs a signed-in user.
bool _isPublic(String path) => path == '/login' || path == '/splash' || path == '/force-update';

/// Bridges Riverpod state to go_router's refreshListenable.
class _RouterRefresh extends ChangeNotifier {
  void ping() => notifyListeners();
}

/// Route requested before the app finished booting (deep link / push tap on cold start).
String? pendingLaunchRoute;

final routerProvider = Provider<GoRouter>((ref) {
  final refresh = _RouterRefresh();
  ref.listen(authControllerProvider, (_, _) => refresh.ping());
  ref.listen(mustUpdateProvider, (_, _) => refresh.ping());
  ref.onDispose(refresh.dispose);

  final router = GoRouter(
    navigatorKey: rootNavigatorKey,
    initialLocation: '/splash',
    refreshListenable: refresh,
    debugLogDiagnostics: false,
    redirect: (context, state) {
      final auth = ref.read(authControllerProvider);
      final path = state.uri.path;

      if (ref.read(mustUpdateProvider)) return path == '/force-update' ? null : '/force-update';
      if (path == '/force-update') return '/new';

      if (!auth.isKnown) return path == '/splash' ? null : '/splash';
      if (path == '/splash') {
        final pending = pendingLaunchRoute;
        pendingLaunchRoute = null;
        return pending ?? '/new';
      }

      if (!auth.isLoggedIn && !_isPublic(path)) {
        return path == '/new' ? '/login' : Uri(path: '/login', queryParameters: {'from': state.uri.toString()}).toString();
      }
      if (auth.isLoggedIn && path == '/login') return '/new';
      return null;
    },
    routes: [
      // The app opens on the sale order form.
      GoRoute(path: '/', redirect: (_, _) => '/new'),
      GoRoute(path: '/splash', builder: (_, _) => const SplashScreen()),
      GoRoute(path: '/force-update', builder: (_, _) => const ForceUpdateScreen()),
      StatefulShellRoute.indexedStack(
        builder: (context, state, shell) => MainShell(shell: shell),
        branches: [
          StatefulShellBranch(routes: [GoRoute(path: '/new', builder: (_, _) => const NewOrderScreen())]),
          StatefulShellBranch(routes: [GoRoute(path: '/stores', builder: (_, _) => const StoresScreen())]),
          StatefulShellBranch(routes: [GoRoute(path: '/orders', builder: (_, _) => const OrdersScreen())]),
          StatefulShellBranch(routes: [GoRoute(path: '/work', builder: (_, _) => const WorkScreen())]),
          StatefulShellBranch(routes: [GoRoute(path: '/account', builder: (_, _) => const AccountScreen())]),
        ],
      ),
      GoRoute(
        path: '/login',
        parentNavigatorKey: rootNavigatorKey,
        builder: (_, s) => LoginScreen(from: s.uri.queryParameters['from']),
      ),
      // Orders
      GoRoute(path: '/orders/new', redirect: (_, _) => '/new'),
      GoRoute(
        path: '/order-success',
        parentNavigatorKey: rootNavigatorKey,
        builder: (_, s) => OrderSuccessScreen(order: s.extra as OrderDetail?),
      ),
      GoRoute(
        path: '/orders/:id',
        parentNavigatorKey: rootNavigatorKey,
        builder: (_, s) => OrderDetailScreen(orderId: int.tryParse(s.pathParameters['id'] ?? '') ?? 0),
      ),
      // «محلاتي»: the agent's pinned shops
      // «ملخصي» and the old pushed shop list became the «المحلات» tab.
      GoRoute(path: '/home', redirect: (_, _) => '/stores'),
      GoRoute(path: '/pins', redirect: (_, _) => '/stores'),
      GoRoute(
        path: '/pins/:id',
        parentNavigatorKey: rootNavigatorKey,
        builder: (_, s) => PinDetailScreen(
          id: int.tryParse(s.pathParameters['id'] ?? '') ?? 0,
          initial: s.extra is Prospect ? s.extra as Prospect : null,
        ),
      ),
      GoRoute(path: '/notifications', parentNavigatorKey: rootNavigatorKey, builder: (_, _) => const NotificationsScreen()),
      GoRoute(path: '/account/about', parentNavigatorKey: rootNavigatorKey, builder: (_, _) => const AboutScreen()),
    ],
    errorBuilder: (context, state) => Scaffold(
      appBar: AppBar(),
      body: Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(Icons.link_off_rounded, size: 56),
            const SizedBox(height: 12),
            const Text('الصفحة غير موجودة'),
            TextButton(onPressed: () => context.go('/new'), child: const Text('العودة للتطبيق')),
          ],
        ),
      ),
    ),
  );
  ref.onDispose(router.dispose);
  return router;
});

/// Opens a route coming from outside (deep link / notification tap).
void openExternalRoute(GoRouter router, String route) {
  final current = router.routerDelegate.currentConfiguration.uri.path;
  if (current == '/splash' || current.isEmpty) {
    pendingLaunchRoute = route;
    return;
  }
  router.push(route);
}
