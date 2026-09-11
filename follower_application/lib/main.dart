import 'package:follower_application/HomePage.dart';
import 'package:follower_application/Login.dart';
import 'package:follower_application/WelcomePage.dart';
import 'package:follower_application/config/app_env.dart';
import 'package:follower_application/customer_profile_page.dart';
import 'package:follower_application/follower_sales_request_page.dart';
import 'package:follower_application/services/follower_tracking_repository.dart';
import 'package:follower_application/start_shift_page.dart';
import 'package:follower_application/tracking/shift_gate_observer.dart';
import 'package:follower_application/tracking/shift_tracking_controller.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:intl/date_symbol_data_local.dart';

final GlobalKey<NavigatorState> followerNavigatorKey = GlobalKey<NavigatorState>();

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  try {
    await initializeDateFormatting('ar');
    await initializeDateFormatting('en');
  } catch (_) {}
  AppEnv.logIfDebug();
  TrackingRuntime.instance ??=
      ShiftTrackingController(repository: ApiFollowerTrackingRepository());
  runApp(const MyApp());
}

class MyApp extends StatelessWidget {
  const MyApp({super.key});

  static final ValueNotifier<ThemeMode> themeNotifier =
      ValueNotifier(ThemeMode.light);

  @override
  Widget build(BuildContext context) {
    SystemChrome.setPreferredOrientations([
      DeviceOrientation.portraitUp,
    ]);
    return ValueListenableBuilder<ThemeMode>(
      valueListenable: themeNotifier,
      builder: (_, ThemeMode currentMode, __) {
        return MaterialApp(
          debugShowCheckedModeBanner: false,
          navigatorKey: followerNavigatorKey,
          navigatorObservers: [ShiftGateObserver()],
          initialRoute: '/',
          routes: {
            '/': (context) => const WelcomePage(),
            '/Login': (context) => const Login(),
            '/StartShift': (context) => const StartShiftPage(),
            '/HomePage': (context) => const HomePage(),
            '/CustomerProfile': (context) {
              final args = ModalRoute.of(context)!.settings.arguments as Map;
              return CustomerProfilePage(
                customer: Map<String, dynamic>.from(args['customer'] as Map),
                listId: int.tryParse('${args['listId'] ?? 0}') ?? 0,
              );
            },
            '/FollowerSalesRequest': (context) =>
                const FollowerSalesRequestPage(),
          },
          theme: ThemeData(fontFamily: 'Cairo', brightness: Brightness.light),
          darkTheme:
              ThemeData(fontFamily: 'Cairo', brightness: Brightness.dark),
          themeMode: currentMode,
        );
      },
    );
  }
}
