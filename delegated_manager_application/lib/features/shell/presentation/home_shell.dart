import 'package:delegated_manager_application/features/complaints/presentation/complaints_screen.dart';
import 'package:delegated_manager_application/features/dashboard/presentation/dashboard_screen.dart';
import 'package:delegated_manager_application/features/exceptions/presentation/exceptions_screen.dart';
import 'package:delegated_manager_application/features/settings/presentation/settings_screen.dart';
import 'package:flutter/material.dart';

/// Bottom navigation host: الرئيسية | الرسائل والشكاوى | طلبات الاستثناء | الإعدادات
class HomeShell extends StatefulWidget {
  const HomeShell({super.key});

  @override
  State<HomeShell> createState() => _HomeShellState();
}

class _HomeShellState extends State<HomeShell> {
  final _dashboardKey = GlobalKey<DashboardScreenState>();
  final _complaintsKey = GlobalKey<ComplaintsScreenState>();
  final _exceptionsKey = GlobalKey<ExceptionsScreenState>();

  int _index = 0;

  void _select(int index) {
    if (_index == index) return;
    setState(() => _index = index);
    switch (index) {
      case 0:
        _dashboardKey.currentState?.reload();
        break;
      case 1:
        _complaintsKey.currentState?.reload();
        break;
      case 2:
        _exceptionsKey.currentState?.reload();
        break;
    }
  }

  void _refreshDashboard() => _dashboardKey.currentState?.reload();

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: IndexedStack(
        index: _index,
        children: [
          DashboardScreen(key: _dashboardKey, onOpenTab: _select),
          ComplaintsScreen(key: _complaintsKey, onChanged: _refreshDashboard),
          ExceptionsScreen(key: _exceptionsKey, onChanged: _refreshDashboard),
          const SettingsScreen(),
        ],
      ),
      bottomNavigationBar: NavigationBar(
        selectedIndex: _index,
        onDestinationSelected: _select,
        destinations: const [
          NavigationDestination(
            icon: Icon(Icons.dashboard_outlined),
            selectedIcon: Icon(Icons.dashboard),
            label: 'الرئيسية',
          ),
          NavigationDestination(
            icon: Icon(Icons.mail_outline),
            selectedIcon: Icon(Icons.mail),
            label: 'الرسائل والشكاوى',
          ),
          NavigationDestination(
            icon: Icon(Icons.rule_outlined),
            selectedIcon: Icon(Icons.rule),
            label: 'طلبات الاستثناء',
          ),
          NavigationDestination(
            icon: Icon(Icons.settings_outlined),
            selectedIcon: Icon(Icons.settings),
            label: 'الإعدادات',
          ),
        ],
      ),
    );
  }
}
