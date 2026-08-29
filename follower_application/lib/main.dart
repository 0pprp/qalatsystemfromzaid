import 'package:follower_application/config/app_env.dart';
import 'package:follower_application/HomePage.dart';
import 'package:follower_application/Login.dart';
import 'package:follower_application/WelcomePage.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  AppEnv.logIfDebug();
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
          initialRoute: '/',
          routes: {
            '/': (context) => const WelcomePage(),
            '/Login': (context) => const Login(),
            '/HomePage': (context) => const HomePage(),
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
