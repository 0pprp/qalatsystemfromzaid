import 'package:follower_application/collection_tab_page.dart';
import 'package:follower_application/declining_customers_page.dart';
import 'package:follower_application/follower_lists_home.dart';
import 'package:follower_application/payments_tab_page.dart';
import 'package:follower_application/ui/app_safe_scaffold.dart';
import 'package:follower_application/ui/follower_complaint_sheet.dart';
import 'package:follower_application/utils/AppTheme.dart';
import 'package:follower_application/AsyncIdChecker.dart';
import 'package:flutter/material.dart';

/// Main entry: drawer only (no bottom tabs).
/// Sections: الرئيسية / الجباية / التسديدات / المتدنية.
class FollowerHomeShell extends StatefulWidget {
  const FollowerHomeShell({super.key});

  @override
  State<FollowerHomeShell> createState() => _FollowerHomeShellState();
}

class _FollowerHomeShellState extends State<FollowerHomeShell> {
  int _index = 0;
  final GlobalKey<ScaffoldState> _scaffoldKey = GlobalKey<ScaffoldState>();

  static const _titles = [
    'الرئيسية',
    'الجباية',
    'التسديدات',
    'الزبائن المتدنية',
  ];

  void _goTab(int i) {
    setState(() => _index = i);
  }

  @override
  Widget build(BuildContext context) {
    return Directionality(
      textDirection: TextDirection.rtl,
      child: AppSafeScaffold(
        scaffoldKey: _scaffoldKey,
        backgroundColor: AppTheme.backgroundColor,
        appBar: AppBar(
          backgroundColor: AppTheme.primaryColor,
          foregroundColor: Colors.white,
          title: Text(
            _titles[_index],
            style: const TextStyle(
              fontFamily: 'Cairo',
              fontWeight: FontWeight.bold,
              color: Colors.white,
            ),
          ),
          leading: IconButton(
            tooltip: 'القائمة',
            icon: const Icon(Icons.menu, color: Colors.white),
            onPressed: () => _scaffoldKey.currentState?.openDrawer(),
          ),
        ),
        drawer: Drawer(
          child: SafeArea(
            child: ListView(
              children: [
                DrawerHeader(
                  decoration: BoxDecoration(color: AppTheme.primaryColor),
                  child: const Align(
                    alignment: Alignment.bottomRight,
                    child: Text(
                      'القائمة',
                      style: TextStyle(
                        fontFamily: 'Cairo',
                        color: Colors.white,
                        fontSize: 22,
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                  ),
                ),
                ListTile(
                  selected: _index == 0,
                  leading: const Icon(Icons.home_outlined),
                  title: const Text('الرئيسية', style: TextStyle(fontFamily: 'Cairo')),
                  onTap: () {
                    Navigator.pop(context);
                    _goTab(0);
                  },
                ),
                ListTile(
                  selected: _index == 1,
                  leading: const Icon(Icons.account_balance_wallet_outlined),
                  title: const Text('الجباية', style: TextStyle(fontFamily: 'Cairo')),
                  onTap: () {
                    Navigator.pop(context);
                    _goTab(1);
                  },
                ),
                ListTile(
                  selected: _index == 2,
                  leading: const Icon(Icons.payments_outlined),
                  title:
                      const Text('التسديدات', style: TextStyle(fontFamily: 'Cairo')),
                  onTap: () {
                    Navigator.pop(context);
                    _goTab(2);
                  },
                ),
                ListTile(
                  selected: _index == 3,
                  leading: const Icon(Icons.trending_down_outlined),
                  title: const Text('الزبائن المتدنية',
                      style: TextStyle(fontFamily: 'Cairo')),
                  onTap: () {
                    Navigator.pop(context);
                    _goTab(3);
                  },
                ),
                const Divider(),
                ListTile(
                  leading: const Icon(Icons.point_of_sale),
                  title: const Text('المبيعات', style: TextStyle(fontFamily: 'Cairo')),
                  onTap: () {
                    Navigator.pop(context);
                    Navigator.pushNamed(context, '/PendingSales');
                  },
                ),
                ListTile(
                  leading: const Icon(Icons.request_quote_outlined),
                  title: const Text('طلبات البيع',
                      style: TextStyle(fontFamily: 'Cairo')),
                  onTap: () {
                    Navigator.pop(context);
                    Navigator.pushNamed(context, '/FollowerSalesRequests');
                  },
                ),
                ListTile(
                  leading: const Icon(Icons.bar_chart),
                  title: const Text('الإحصائيات', style: TextStyle(fontFamily: 'Cairo')),
                  onTap: () {
                    Navigator.pop(context);
                    Navigator.pushNamed(context, '/ListStats');
                  },
                ),
                ListTile(
                  leading: const Icon(Icons.gavel),
                  title: const Text('الزبائن القديمة',
                      style: TextStyle(fontFamily: 'Cairo')),
                  onTap: () {
                    Navigator.pop(context);
                    Navigator.pushNamed(context, '/LegalFollowups');
                  },
                ),
                ListTile(
                  leading: const Icon(Icons.report_gmailerrorred_outlined),
                  title: const Text('إرسال شكوى',
                      style: TextStyle(fontFamily: 'Cairo')),
                  onTap: () {
                    Navigator.pop(context);
                    FollowerComplaintSheet.show(context);
                  },
                ),
                const Divider(),
                ListTile(
                  leading: const Icon(Icons.logout, color: Colors.red),
                  title: const Text('تسجيل الخروج',
                      style: TextStyle(fontFamily: 'Cairo', color: Colors.red)),
                  onTap: () async {
                    Navigator.pop(context);
                    await AsyncIdChecker.logout();
                    if (!context.mounted) return;
                    Navigator.pushNamedAndRemoveUntil(
                        context, '/Login', (r) => false);
                  },
                ),
              ],
            ),
          ),
        ),
        body: IndexedStack(
          index: _index,
          children: const [
            FollowerListsHome(),
            CollectionTabPage(),
            PaymentsTabPage(),
            DecliningCustomersPage(),
          ],
        ),
      ),
    );
  }
}
