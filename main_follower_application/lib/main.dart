import 'package:flutter/material.dart';
import 'package:main_follower_application/core/auth/session.dart';
import 'package:main_follower_application/core/config/app_env.dart';
import 'package:main_follower_application/core/network/api_client.dart';
import 'package:main_follower_application/presentation/login_screen.dart';
import 'package:main_follower_application/presentation/shift_gate_screen.dart';

final GlobalKey<NavigatorState> appNavigatorKey = GlobalKey<NavigatorState>();

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  await Session.load();
  ApiClient.onUnauthorized = _handleUnauthorized;
  runApp(const MainFollowerApp());
}

Future<void> _handleUnauthorized() async {
  await Session.clear();
  final nav = appNavigatorKey.currentState;
  if (nav == null) return;
  nav.pushAndRemoveUntil(
    MaterialPageRoute(builder: (_) => const LoginScreen()),
    (_) => false,
  );
}

class MainFollowerApp extends StatelessWidget {
  const MainFollowerApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'المتابع الرئيسي',
      navigatorKey: appNavigatorKey,
      debugShowCheckedModeBanner: false,
      locale: const Locale('ar'),
      theme: ThemeData(
        useMaterial3: true,
        fontFamily: 'Cairo',
        colorScheme: ColorScheme.fromSeed(
          seedColor: const Color(0xFF0F3D3E),
          brightness: Brightness.light,
        ),
      ),
      builder: (context, child) => Directionality(
        textDirection: TextDirection.rtl,
        child: child ?? const SizedBox.shrink(),
      ),
      home: Session.isLoggedIn ? const ShiftGateScreen() : const LoginScreen(),
    );
  }
}

String appEnvLabel() => AppEnv.isDemo ? 'تجريبي' : 'إنتاج';
