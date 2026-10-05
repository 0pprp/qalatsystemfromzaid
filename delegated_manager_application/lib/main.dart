import 'package:delegated_manager_application/core/auth/session.dart';
import 'package:delegated_manager_application/core/config/app_env.dart';
import 'package:delegated_manager_application/core/network/api_client.dart';
import 'package:delegated_manager_application/core/routing/app_routes.dart';
import 'package:delegated_manager_application/core/theme/app_theme.dart';
import 'package:delegated_manager_application/features/authentication/data/auth_repository.dart';
import 'package:delegated_manager_application/features/authentication/presentation/login_screen.dart';
import 'package:delegated_manager_application/features/shell/presentation/home_shell.dart';
import 'package:flutter/material.dart';

final GlobalKey<NavigatorState> appNavigatorKey = GlobalKey<NavigatorState>();

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  AppEnv.logIfDebug();
  await Session.load();
  // Demo and production gateways use different JWT signing keys. A stored
  // demo token against the production host yields HTTP 401 («انتهت الجلسة»).
  if (AppEnv.isSessionBaseMismatched(
      env: AppEnv.name, sessionBase: Session.apiBase)) {
    await Session.clear();
  }
  ApiClient.onUnauthorized = _handleUnauthorized;
  runApp(const DelegatedManagerApp());
}

Future<void> _handleUnauthorized() async {
  await AuthRepository.logout();
  final nav = appNavigatorKey.currentState;
  if (nav == null) return;
  nav.pushAndRemoveUntil(
    MaterialPageRoute(builder: (_) => const LoginScreen()),
    (_) => false,
  );
}

class DelegatedManagerApp extends StatelessWidget {
  const DelegatedManagerApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'تطبيق المدير المفوض',
      navigatorKey: appNavigatorKey,
      debugShowCheckedModeBanner: false,
      theme: AppTheme.light,
      locale: const Locale('ar'),
      onGenerateRoute: AppRoutes.generate,
      builder: (context, child) => Directionality(
        textDirection: TextDirection.rtl,
        child: child ?? const SizedBox.shrink(),
      ),
      home: Session.isLoggedIn ? const HomeShell() : const LoginScreen(),
    );
  }
}
