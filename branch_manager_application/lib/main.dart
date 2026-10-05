import 'package:branch_manager_application/config/app_env.dart';
import 'package:branch_manager_application/core/session.dart';
import 'package:branch_manager_application/theme/bm_theme.dart';
import 'package:branch_manager_application/ui/home_shell.dart';
import 'package:branch_manager_application/ui/login_page.dart';
import 'package:flutter/material.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  AppEnv.logIfDebug();
  await BmSession.instance.load();
  runApp(const BranchManagerApp());
}

class BranchManagerApp extends StatefulWidget {
  const BranchManagerApp({super.key});

  @override
  State<BranchManagerApp> createState() => _BranchManagerAppState();
}

class _BranchManagerAppState extends State<BranchManagerApp> {
  late bool _loggedIn = BmSession.instance.isLoggedIn;

  void _refresh() => setState(() => _loggedIn = BmSession.instance.isLoggedIn);

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'مدير الفرع',
      debugShowCheckedModeBanner: false,
      theme: BmTheme.light(),
      home: _loggedIn
          ? HomeShell(onLogout: _refresh)
          : LoginPage(onLoggedIn: _refresh),
    );
  }
}
