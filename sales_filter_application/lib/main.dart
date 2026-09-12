import 'package:flutter/material.dart';
import 'package:sales_filter_application/screens/home_screen.dart';
import 'package:sales_filter_application/screens/login_screen.dart';
import 'package:sales_filter_application/services/session.dart';
import 'package:sales_filter_application/theme/app_theme.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  await Session.load();
  runApp(const SalesFilterApp());
}

class SalesFilterApp extends StatelessWidget {
  const SalesFilterApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'فلترة المبيعات',
      debugShowCheckedModeBanner: false,
      theme: AppTheme.light,
      locale: const Locale('ar'),
      builder: (context, child) => Directionality(
        textDirection: TextDirection.rtl,
        child: child ?? const SizedBox.shrink(),
      ),
      home: Session.isLoggedIn ? const HomeScreen() : const LoginScreen(),
    );
  }
}
