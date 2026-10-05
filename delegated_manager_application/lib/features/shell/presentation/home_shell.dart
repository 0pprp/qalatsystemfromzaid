import 'package:delegated_manager_application/core/auth/session.dart';
import 'package:delegated_manager_application/core/config/app_env.dart';
import 'package:delegated_manager_application/core/theme/app_theme.dart';
import 'package:delegated_manager_application/features/authentication/data/auth_repository.dart';
import 'package:delegated_manager_application/features/authentication/presentation/login_screen.dart';
import 'package:delegated_manager_application/features/committees/presentation/committees_screen.dart';
import 'package:delegated_manager_application/features/complaints/presentation/complaints_screen.dart';
import 'package:delegated_manager_application/features/dashboard/presentation/dashboard_screen.dart';
import 'package:delegated_manager_application/features/exceptions/presentation/exceptions_screen.dart';
import 'package:delegated_manager_application/features/settings/presentation/settings_screen.dart';
import 'package:delegated_manager_application/features/shell/presentation/home_tabs.dart';
import 'package:delegated_manager_application/features/stats/presentation/stats_page.dart';
import 'package:delegated_manager_application/features/waivers/presentation/waivers_screen.dart';
import 'package:flutter/material.dart';

/// Drawer: الرئيسية | التقييمات | الرسائل | الاستثناء | المبيعات الخاطئة | التنازل | اللجان | الإعدادات
class HomeShell extends StatefulWidget {
  const HomeShell({super.key});

  @override
  State<HomeShell> createState() => _HomeShellState();
}

class _HomeShellState extends State<HomeShell>
    with SingleTickerProviderStateMixin {
  final _dashboardKey = GlobalKey<DashboardScreenState>();
  final _statsKey = GlobalKey<StatsPageState>();
  final _complaintsKey = GlobalKey<ComplaintsScreenState>();
  final _exceptionsKey = GlobalKey<ExceptionsScreenState>();
  final _wrongSalesKey = GlobalKey<ExceptionsScreenState>();
  final _waiversKey = GlobalKey<WaiversScreenState>();
  final _committeesKey = GlobalKey<CommitteesScreenState>();

  late final TabController _committeeTabs;
  int _index = 0;

  static const _titles = [
    'الرئيسية',
    'التقييمات',
    'الرسائل والشكاوى',
    'طلبات الاستثناء',
    'المبيعات الخاطئة',
    'طلبات التنازل',
    'اللجان',
    'الإعدادات',
  ];

  static const _menu = [
    (Icons.dashboard_outlined, Icons.dashboard, 'الرئيسية'),
    (Icons.bar_chart_outlined, Icons.bar_chart, 'التقييمات'),
    (Icons.mail_outline, Icons.mail, 'الرسائل والشكاوى'),
    (Icons.rule_outlined, Icons.rule, 'طلبات الاستثناء'),
    (Icons.report_gmailerrorred_outlined, Icons.report_gmailerrorred,
        'المبيعات الخاطئة'),
    (Icons.handshake_outlined, Icons.handshake, 'طلبات التنازل'),
    (Icons.groups_outlined, Icons.groups, 'اللجان'),
    (Icons.settings_outlined, Icons.settings, 'الإعدادات'),
  ];

  @override
  void initState() {
    super.initState();
    _committeeTabs = TabController(length: 2, vsync: this);
    _committeeTabs.addListener(() {
      if (mounted) setState(() {});
    });
  }

  @override
  void dispose() {
    _committeeTabs.dispose();
    super.dispose();
  }

  void _select(int index, {bool wrongSalesUnread = false}) {
    if (_index == index && !wrongSalesUnread) {
      Navigator.of(context).maybePop();
      return;
    }
    setState(() => _index = index);
    Navigator.of(context).maybePop();
    switch (index) {
      case HomeShellTabs.dashboard:
        _dashboardKey.currentState?.reload();
        break;
      case HomeShellTabs.stats:
        _statsKey.currentState?.reload();
        break;
      case HomeShellTabs.complaints:
        _complaintsKey.currentState?.reload();
        break;
      case HomeShellTabs.exceptions:
        _exceptionsKey.currentState?.reload();
        break;
      case HomeShellTabs.wrongSales:
        if (wrongSalesUnread) {
          _wrongSalesKey.currentState?.openUnreadOnly();
        } else {
          _wrongSalesKey.currentState?.reload();
        }
        break;
      case HomeShellTabs.waivers:
        _waiversKey.currentState?.reload();
        break;
      case HomeShellTabs.committees:
        _committeesKey.currentState?.reload();
        break;
    }
  }

  void _openTab(int index) => _select(index);

  void _openWrongSalesUnread() =>
      _select(HomeShellTabs.wrongSales, wrongSalesUnread: true);

  void _refreshDashboard() => _dashboardKey.currentState?.reload();

  Future<void> _confirmLogout() async {
    final ok = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('تسجيل الخروج'),
        content: const Text('هل تريد الخروج من التطبيق؟'),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx, false),
            child: const Text('إلغاء'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(ctx, true),
            child: const Text('خروج'),
          ),
        ],
      ),
    );
    if (ok != true || !mounted) return;
    await AuthRepository.logout();
    if (!mounted) return;
    Navigator.of(context).pushAndRemoveUntil(
      MaterialPageRoute(builder: (_) => const LoginScreen()),
      (_) => false,
    );
  }

  List<Widget> _actions() {
    switch (_index) {
      case HomeShellTabs.dashboard:
        return [
          IconButton(
            tooltip: 'تحديث',
            onPressed: () => _dashboardKey.currentState?.reload(),
            icon: const Icon(Icons.refresh),
          ),
        ];
      case HomeShellTabs.complaints:
        final unreadOnly = _complaintsKey.currentState?.unreadOnly ?? false;
        return [
          IconButton(
            tooltip: unreadOnly ? 'عرض الكل' : 'غير المقروءة فقط',
            onPressed: () {
              _complaintsKey.currentState?.toggleUnreadOnly();
              setState(() {});
            },
            icon: Icon(
                unreadOnly ? Icons.filter_alt : Icons.filter_alt_outlined),
          ),
          IconButton(
            tooltip: 'تحديد الكل كمقروء',
            onPressed: () => _complaintsKey.currentState?.markAllRead(),
            icon: const Icon(Icons.done_all),
          ),
        ];
      case HomeShellTabs.exceptions:
        return [
          IconButton(
            tooltip: 'تحديث',
            onPressed: () => _exceptionsKey.currentState?.reload(),
            icon: const Icon(Icons.refresh),
          ),
        ];
      case HomeShellTabs.wrongSales:
        return [
          IconButton(
            tooltip: 'تحديث',
            onPressed: () => _wrongSalesKey.currentState?.reload(),
            icon: const Icon(Icons.refresh),
          ),
        ];
      case HomeShellTabs.waivers:
        return [
          IconButton(
            tooltip: 'تحديث',
            onPressed: () => _waiversKey.currentState?.reload(),
            icon: const Icon(Icons.refresh),
          ),
        ];
      case HomeShellTabs.committees:
        return [
          IconButton(
            tooltip: 'تحديث',
            onPressed: () => _committeesKey.currentState?.reload(),
            icon: const Icon(Icons.refresh),
          ),
        ];
      default:
        return const [];
    }
  }

  @override
  Widget build(BuildContext context) {
    final onCommittees = _index == HomeShellTabs.committees;
    return Scaffold(
      appBar: AppBar(
        title: Text(onCommittees
            ? (_committeeTabs.index == 1 ? 'خريطة المتابعين' : 'التكليف')
            : _titles[_index]),
        actions: _actions(),
        bottom: onCommittees
            ? TabBar(
                controller: _committeeTabs,
                indicatorColor: Colors.white,
                indicatorWeight: 3,
                labelColor: Colors.white,
                unselectedLabelColor: Colors.white70,
                labelStyle: const TextStyle(
                  fontFamily: 'Cairo',
                  fontWeight: FontWeight.bold,
                  fontSize: 14,
                ),
                unselectedLabelStyle: const TextStyle(
                  fontFamily: 'Cairo',
                  fontSize: 13,
                ),
                tabs: const [
                  Tab(
                    icon: Icon(Icons.assignment_outlined),
                    text: 'التكليف',
                  ),
                  Tab(
                    icon: Icon(Icons.map_outlined),
                    text: 'خريطة المتابعين',
                  ),
                ],
              )
            : null,
      ),
      drawer: Drawer(
        child: SafeArea(
          child: ListView(
            children: [
              DrawerHeader(
                decoration: const BoxDecoration(color: AppColors.primary),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Text(
                      'تطبيق المدير المفوض',
                      style: TextStyle(
                        color: Colors.white,
                        fontSize: 20,
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                    const SizedBox(height: 6),
                    Text(
                      Session.userName ?? 'مدير مفوض',
                      style: const TextStyle(
                        color: Colors.white70,
                        fontSize: 13,
                      ),
                    ),
                    if (AppEnv.isDemo) ...[
                      const SizedBox(height: 4),
                      const Text(
                        'تجريبي • النجف DEMO',
                        style: TextStyle(
                          color: Colors.white70,
                          fontSize: 12,
                        ),
                      ),
                    ],
                  ],
                ),
              ),
              for (var i = 0; i < _menu.length; i++)
                ListTile(
                  leading: Icon(
                    _index == i ? _menu[i].$2 : _menu[i].$1,
                    color: _index == i ? AppColors.primary : null,
                  ),
                  title: Text(
                    _menu[i].$3,
                    style: TextStyle(
                      fontWeight:
                          _index == i ? FontWeight.w700 : FontWeight.w500,
                      color: _index == i ? AppColors.primary : null,
                    ),
                  ),
                  selected: _index == i,
                  onTap: () => _select(i),
                ),
              const Divider(height: 1),
              ListTile(
                leading: const Icon(Icons.logout),
                title: const Text('خروج'),
                onTap: () {
                  Navigator.of(context).maybePop();
                  _confirmLogout();
                },
              ),
            ],
          ),
        ),
      ),
      body: IndexedStack(
        index: _index,
        children: [
          DashboardScreen(
            key: _dashboardKey,
            onOpenTab: _openTab,
            onOpenWrongSalesUnread: _openWrongSalesUnread,
          ),
          StatsPage(key: _statsKey),
          ComplaintsScreen(key: _complaintsKey, onChanged: _refreshDashboard),
          ExceptionsScreen(
            key: _exceptionsKey,
            mode: ExceptionInboxMode.hold,
            onChanged: _refreshDashboard,
          ),
          ExceptionsScreen(
            key: _wrongSalesKey,
            mode: ExceptionInboxMode.wrongSale,
            onChanged: _refreshDashboard,
          ),
          WaiversScreen(key: _waiversKey, onChanged: _refreshDashboard),
          CommitteesScreen(
            key: _committeesKey,
            tabController: _committeeTabs,
          ),
          const SettingsScreen(),
        ],
      ),
    );
  }
}
