import 'package:delegate_application/all_receipt.dart';
import 'package:delegate_application/AllSale.dart';
import 'package:delegate_application/Customer.dart';
import 'package:delegate_application/HomePage.dart';
import 'package:delegate_application/Login.dart';
import 'package:delegate_application/Sync.dart';
import 'package:delegate_application/WelcomePage.dart';
import 'package:delegate_application/TrustReceiptsListPage.dart';
import 'package:delegate_application/TrustReceiptFormPage.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

Future<void> main() async {
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
            '/Sync': (context) => const Sync(),
            '/AllSale': (context) => const AllSale(),
            '/AllReceipt': (context) => const AllReceipt(),
            '/Customer': (context) => const Customer(),
            '/TrustReceiptsList': (context) => const TrustReceiptsListPage(),
            '/TrustReceiptForm': (context) => const TrustReceiptFormPage(),
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
