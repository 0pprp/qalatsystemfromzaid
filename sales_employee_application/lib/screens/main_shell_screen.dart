import 'package:flutter/material.dart';
import 'package:sales_employee_application/screens/archive_screen.dart';
import 'package:sales_employee_application/screens/pending_sales_screen.dart';
import 'package:sales_employee_application/screens/submitted_requests_screen.dart';
import 'package:sales_employee_application/tracking/shift_tracking_controller.dart';
import 'package:sales_employee_application/utils/app_theme.dart';

/// Bottom tabs: home, submitted sales-request history, and the paper archive.
class MainShellScreen extends StatefulWidget {
  const MainShellScreen({super.key});

  @override
  State<MainShellScreen> createState() => _MainShellScreenState();
}

class _MainShellScreenState extends State<MainShellScreen> with WidgetsBindingObserver {
  int _index = 0;
  final _archiveKey = GlobalKey<ArchiveScreenState>();

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
    TrackingRuntime.instance?.restoreIfNeeded();
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    super.dispose();
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed) {
      TrackingRuntime.instance?.restoreIfNeeded();
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: IndexedStack(
        index: _index,
        children: [
          const PendingSalesScreen(),
          const _SubmittedTabHost(),
          ArchiveScreen(key: _archiveKey),
        ],
      ),
      bottomNavigationBar: NavigationBar(
        selectedIndex: _index,
        onDestinationSelected: (i) {
          setState(() => _index = i);
          if (i == 2) {
            _archiveKey.currentState?.reload();
          }
        },
        indicatorColor: AppColors.darkGreen.withValues(alpha: 0.15),
        destinations: const [
          NavigationDestination(
            icon: Icon(Icons.home_outlined),
            selectedIcon: Icon(Icons.home, color: AppColors.darkGreen),
            label: 'الرئيسية',
          ),
          NavigationDestination(
            icon: Icon(Icons.history_outlined),
            selectedIcon: Icon(Icons.history, color: AppColors.darkGreen),
            label: 'سجل طلبات المبيع',
          ),
          NavigationDestination(
            icon: Icon(Icons.inventory_2_outlined),
            selectedIcon: Icon(Icons.inventory_2, color: AppColors.darkGreen),
            label: 'الارشيف',
          ),
        ],
      ),
    );
  }
}

class _SubmittedTabHost extends StatelessWidget {
  const _SubmittedTabHost();

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('سجل طلبات المبيع')),
      body: const SubmittedRequestsScreen(),
    );
  }
}
