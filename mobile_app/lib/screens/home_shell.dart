import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../config/theme.dart';
import '../providers/auth_provider.dart';
import 'dashboard/dashboard_screen.dart';
import 'customers/customers_list_screen.dart';
import 'payments/payments_total_screen.dart';
import 'inventory/warehouses_list_screen.dart';
import 'finance/cash_registers_screen.dart';

class HomeShell extends StatefulWidget {
  const HomeShell({super.key});

  @override
  State<HomeShell> createState() => _HomeShellState();
}

class _HomeShellState extends State<HomeShell> {
  int _currentIndex = 0;

  static const _tabTitles = ['الرئيسية', 'العملاء', 'التسديدات', 'المخزون', 'المالية'];
  static const _tabIcons = [Icons.dashboard_rounded, Icons.people_alt_rounded, Icons.payments_rounded, Icons.inventory_2_rounded, Icons.account_balance_rounded];

  late final _screens = const [
    DashboardScreen(),
    CustomersListScreen(),
    PaymentsTotalScreen(),
    WarehousesListScreen(),
    CashRegistersScreen(),
  ];

  final GlobalKey<ScaffoldState> _scaffoldKey = GlobalKey<ScaffoldState>();

  @override
  Widget build(BuildContext context) {
    final auth = context.watch<AuthProvider>();
    final isDark = Theme.of(context).brightness == Brightness.dark;

    return Scaffold(
      key: _scaffoldKey,
      appBar: AppBar(
        title: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            ClipRRect(
              borderRadius: BorderRadius.circular(8),
              child: Image.asset('assets/icons/logo.png', width: 36, height: 36, fit: BoxFit.contain),
            ),
            const SizedBox(width: 10),
            Text(_tabTitles[_currentIndex], style: const TextStyle(fontWeight: FontWeight.bold)),
          ],
        ),
        leading: IconButton(icon: const Icon(Icons.menu), onPressed: () => _scaffoldKey.currentState?.openDrawer()),
        actions: [
          IconButton(
            icon: Icon(auth.themeMode == ThemeMode.dark ? Icons.light_mode : Icons.dark_mode),
            tooltip: 'تغيير المظهر',
            onPressed: auth.toggleTheme,
          ),
          IconButton(
            icon: const Icon(Icons.logout_rounded),
            tooltip: 'تسجيل الخروج',
            onPressed: () async => _handleLogout(auth),
          ),
        ],
      ),
      drawer: _buildDrawer(context, auth),
      body: IndexedStack(index: _currentIndex, children: _screens),
      bottomNavigationBar: _buildBottomNav(isDark),
    );
  }

  Future<void> _handleLogout(AuthProvider auth) async {
    final confirm = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('تسجيل الخروج'),
        content: const Text('هل تريد تسجيل الخروج؟'),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx, false), child: const Text('إلغاء')),
          TextButton(onPressed: () => Navigator.pop(ctx, true), child: const Text('خروج', style: TextStyle(color: Colors.red))),
        ],
      ),
    );
    if (confirm == true && mounted) {
      auth.logout();
      Navigator.pushReplacementNamed(context, '/login');
    }
  }

  Widget _buildBottomNav(bool isDark) {
    return Container(
      decoration: BoxDecoration(
        boxShadow: [BoxShadow(color: Colors.black.withAlpha(26), blurRadius: 10, offset: const Offset(0, -2))],
      ),
      child: BottomNavigationBar(
        currentIndex: _currentIndex,
        onTap: (index) => setState(() => _currentIndex = index),
        type: BottomNavigationBarType.fixed,
        selectedItemColor: AppTheme.primaryColor,
        unselectedItemColor: Colors.grey,
        items: List.generate(_tabTitles.length, (i) => BottomNavigationBarItem(
          icon: AnimatedContainer(
            duration: const Duration(milliseconds: 250),
            curve: Curves.easeOutCubic,
            transform: _currentIndex == i
                ? (Matrix4.identity()..scale(1.15))
                : Matrix4.identity(),
            child: Icon(_tabIcons[i]),
          ),
          activeIcon: ShaderMask(
            shaderCallback: (bounds) => AppTheme.primaryGradient.createShader(bounds),
            child: Icon(_tabIcons[i]),
          ),
          label: _tabTitles[i],
        )),
      ),
    );
  }

  Widget _buildDrawer(BuildContext context, AuthProvider auth) {
    final scheme = Theme.of(context).colorScheme;

    return Drawer(
      child: SafeArea(
        child: Column(
          children: [
            // Header
            Container(
              width: double.infinity,
              padding: const EdgeInsetsDirectional.only(start: 20, top: 30, end: 20, bottom: 20),
              decoration: const BoxDecoration(gradient: AppTheme.primaryGradient),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const SizedBox(height: 8),
                  ClipRRect(
                    borderRadius: BorderRadius.circular(16),
                    child: Image.asset('assets/icons/logo.png', width: 64, height: 64, fit: BoxFit.contain),
                  ),
                  const SizedBox(height: 12),
                  Text(auth.fullName ?? auth.userName ?? 'المستخدم', style: const TextStyle(color: Colors.white, fontSize: 18, fontWeight: FontWeight.bold)),
                  const SizedBox(height: 4),
                  Text(auth.userName ?? '', style: TextStyle(color: Colors.white.withAlpha(204), fontSize: 13)),
                ],
              ),
            ),
            // Menu items
            Expanded(
              child: ListView(
                padding: const EdgeInsets.symmetric(vertical: 8),
                children: [
                  _drawerItem(Icons.bar_chart_rounded, 'الإحصائيات', '/agents-statistics', scheme),
                  _drawerItem(Icons.person_search_rounded, 'المندوبين', '/delegates-list', scheme),
                  _drawerItem(Icons.sell_rounded, 'المبيعات', '/sales-list', scheme),
                  _drawerItem(Icons.archive_rounded, 'أرشيف المصفرين', '/customers-archived', scheme),
                  _drawerItem(Icons.pause_circle_rounded, 'المتوقفين', '/customers-stopped-date', scheme),
                  _drawerItem(Icons.calendar_month_rounded, 'تسديدات أسبوع', '/payments-week', scheme),
                  _drawerItem(Icons.calendar_view_month_rounded, 'تسديدات شهر', '/payments-month', scheme),
                  _drawerItem(Icons.track_changes_rounded, 'متابعة التاريخ', '/payments-date-tracking', scheme),
                  _drawerItem(Icons.request_page_rounded, 'طلبات التسديد', '/payment-requests', scheme),
                  _drawerItem(Icons.inventory_rounded, 'المواد', '/items-list', scheme),
                  _drawerItem(Icons.local_shipping_rounded, 'الموردين', '/suppliers-list', scheme),
                  _drawerItem(Icons.shopping_bag_rounded, 'المشتريات', '/purchases-list', scheme),
                  _drawerItem(Icons.add_circle_outline_rounded, 'الإضافات', '/cash-deposits', scheme),
                  _drawerItem(Icons.remove_circle_outline_rounded, 'السحوبات', '/cash-withdrawals', scheme),
                  _drawerItem(Icons.swap_horiz_rounded, 'التحويلات', '/cash-transfers', scheme),
                  _drawerItem(Icons.today_rounded, 'المتابعة اليومية', '/daily-followup', scheme),
                  const Divider(),
                  _drawerItem(Icons.people_rounded, 'الموظفين', '/employees-list', scheme),
                  _drawerItem(Icons.admin_panel_settings_rounded, 'المستخدمين', '/users-list', scheme),
                  _drawerItem(Icons.security_rounded, 'الصلاحيات', '/permission-list', scheme),
                  _drawerItem(Icons.currency_exchange_rounded, 'مواد الصرف', '/expense-items', scheme),
                  _drawerItem(Icons.cloud_upload_rounded, 'نسخ احتياطي', '/backup-database', scheme),
                ],
              ),
            ),
            // Theme toggle
            Padding(
              padding: const EdgeInsets.all(16),
              child: Row(
                children: [
                  IconButton(
                    icon: Icon(auth.themeMode == ThemeMode.dark ? Icons.light_mode : Icons.dark_mode),
                    onPressed: auth.toggleTheme,
                  ),
                  const Text('تغيير المظهر', style: TextStyle(fontSize: 14)),
                  const Spacer(),
                  const Text('v1.0.0', style: TextStyle(color: Colors.grey, fontSize: 12)),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _drawerItem(IconData icon, String title, String route, ColorScheme scheme) {
    return ListTile(
      leading: Icon(icon, color: scheme.primary),
      title: Text(title, style: const TextStyle(fontSize: 14)),
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
      onTap: () {
        Navigator.pop(context);
        Navigator.pushNamed(context, route);
      },
    );
  }
}
