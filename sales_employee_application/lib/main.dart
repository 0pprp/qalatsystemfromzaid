import 'package:flutter/material.dart';
import 'package:sales_employee_application/config/app_env.dart';
import 'package:sales_employee_application/screens/home_screen.dart';
import 'package:sales_employee_application/screens/login_screen.dart';
import 'package:sales_employee_application/screens/pending_sales_screen.dart';
import 'package:sales_employee_application/screens/sale_details_screen.dart';
import 'package:sales_employee_application/screens/sale_screen.dart';
import 'package:sales_employee_application/screens/search_screen.dart';
import 'package:sales_employee_application/screens/shift_screen.dart';
import 'package:sales_employee_application/data/sales_repository_factory.dart';
import 'package:sales_employee_application/screens/warehouse_screen.dart';
import 'package:sales_employee_application/services/session.dart';
import 'package:sales_employee_application/tracking/shift_tracking_controller.dart';
import 'package:sales_employee_application/tracking/tracking_channel.dart';
import 'package:sales_employee_application/utils/app_theme.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  AppEnv.logIfDebug();
  await Session.init();
  runApp(const SalesEmployeeApp());
}

class SalesEmployeeApp extends StatelessWidget {
  const SalesEmployeeApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      debugShowCheckedModeBanner: false,
      title: 'موظف المبيعات',
      theme: AppTheme.themeData,
      builder: (context, child) => Directionality(
        textDirection: TextDirection.rtl,
        child: child ?? const SizedBox.shrink(),
      ),
      initialRoute: '/',
      routes: {
        '/': (_) => const _SplashScreen(),
        '/login': (_) => const LoginScreen(),
        '/shift': (_) => const ShiftScreen(),
        '/home': (_) => const HomeScreen(),
        '/search': (_) => const SearchScreen(),
        '/sale': (_) => const SaleScreen(),
        '/warehouse': (_) => const WarehouseScreen(),
        '/sales': (_) => const PendingSalesScreen(),
        '/sale-details': (context) {
          final id = ModalRoute.of(context)?.settings.arguments;
          return SaleDetailsScreen(saleId: id is int ? id : null);
        },
      },
    );
  }
}

class _SplashScreen extends StatefulWidget {
  const _SplashScreen();

  @override
  State<_SplashScreen> createState() => _SplashScreenState();
}

class _SplashScreenState extends State<_SplashScreen> {
  @override
  void initState() {
    super.initState();
    _go();
  }

  Future<void> _go() async {
    await Future.delayed(const Duration(milliseconds: 500));
    if (!mounted) return;
    if (!Session.isLoggedIn) {
      Navigator.pushReplacementNamed(context, '/login');
      return;
    }
    if (Session.gpsStoppedByUser) {
      try {
        await TrackingChannel.stop();
      } catch (_) {}
      if (!mounted) return;
      Navigator.pushReplacementNamed(context, '/home');
      return;
    }
    try {
      final current = await SalesRepositoryFactory.instance.currentShift();
      if (!mounted) return;
      if (current != null && current.isActive) {
        final tracking = TrackingRuntime.instance ??=
            ShiftTrackingController(repository: SalesRepositoryFactory.instance);
        await tracking.attach(current);
        await Session.saveShift(current.toJson(), current.cutoffAtUtc.toIso8601String());
        if (!mounted) return;
        Navigator.pushReplacementNamed(context, '/home');
        return;
      }
      try {
        await TrackingChannel.stop();
      } catch (_) {}
    } catch (_) {
      try {
        await TrackingChannel.stop();
      } catch (_) {}
    }
    if (!mounted) return;
    Navigator.pushReplacementNamed(context, '/shift');
  }

  @override
  Widget build(BuildContext context) {
    return const Scaffold(
      body: Center(
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Image(image: AssetImage('assets/icons/LogoCompany.png'), width: 160),
            SizedBox(height: 16),
            Text(
              'شركة قلعة الضمان',
              style: TextStyle(
                fontSize: 22,
                fontWeight: FontWeight.bold,
                color: AppColors.darkGreen,
              ),
            ),
          ],
        ),
      ),
    );
  }
}
