import 'package:delegated_manager_application/core/auth/session.dart';
import 'package:delegated_manager_application/core/config/app_env.dart';
import 'package:delegated_manager_application/core/routing/app_routes.dart';
import 'package:delegated_manager_application/core/theme/app_theme.dart';
import 'package:delegated_manager_application/features/authentication/presentation/login_screen.dart';
import 'package:delegated_manager_application/features/shell/presentation/home_shell.dart';
import 'package:flutter/material.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  AppEnv.logIfDebug();
  await Session.load();
  runApp(const DelegatedManagerApp());
}

class DelegatedManagerApp extends StatelessWidget {
  const DelegatedManagerApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'تطبيق المدير المفوض',
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
