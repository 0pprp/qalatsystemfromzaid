import 'package:branch_manager_application/core/session.dart';
import 'package:branch_manager_application/core/update/app_update_gate.dart';
import 'package:branch_manager_application/theme/bm_theme.dart';
import 'package:branch_manager_application/ui/customers_page.dart';
import 'package:branch_manager_application/ui/decisions_page.dart';
import 'package:branch_manager_application/ui/history_page.dart';
import 'package:branch_manager_application/ui/new_sales_page.dart';
import 'package:branch_manager_application/ui/payments_page.dart';
import 'package:branch_manager_application/ui/stats_page.dart';
import 'package:branch_manager_application/ui/waivers_history_page.dart';
import 'package:branch_manager_application/services/tutorial_video_service.dart';
import 'package:flutter/material.dart';
import 'package:url_launcher/url_launcher.dart';

class HomeShell extends StatefulWidget {
  const HomeShell({super.key, required this.onLogout});

  final VoidCallback onLogout;

  @override
  State<HomeShell> createState() => _HomeShellState();
}

class _HomeShellState extends State<HomeShell> {
  int _index = 0;

  static const _titles = [
    'القرارات',
    'الزبائن',
    'المبيعات',
    'التقييم',
    'التسديدات',
    'طلبات التنازل',
    'سجل القرارات',
  ];

  static const _menu = [
    (Icons.gavel_outlined, Icons.gavel, 'القرارات'),
    (Icons.people_outline, Icons.people, 'الزبائن'),
    (Icons.storefront_outlined, Icons.storefront, 'المبيعات'),
    (Icons.bar_chart_outlined, Icons.bar_chart, 'التقييم'),
    (Icons.payments_outlined, Icons.payments, 'التسديدات'),
    (Icons.handshake_outlined, Icons.handshake, 'طلبات التنازل'),
    (Icons.history_outlined, Icons.history, 'سجل القرارات'),
  ];

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) AppUpdateGate.checkAndPrompt(context);
    });
  }

  void _openPage(int i) {
    setState(() => _index = i);
    Navigator.of(context).maybePop();
  }

  Future<void> _openTutorialVideo() async {
    showDialog(
      context: context,
      barrierDismissible: false,
      builder: (_) => const Center(child: CircularProgressIndicator()),
    );
    Uri? uri;
    try {
      uri = await TutorialVideoService.resolveLaunchUri();
    } finally {
      if (mounted) Navigator.of(context, rootNavigator: true).pop();
    }
    if (!mounted) return;
    if (uri == null) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text(
            'لم يُحدَّد رابط الفيديو بعد. عدّله من لوحة مدير المبيعات.',
            style: TextStyle(fontFamily: 'Cairo'),
          ),
        ),
      );
      return;
    }
    try {
      final ok = await launchUrl(uri, mode: LaunchMode.externalApplication);
      if (!ok && mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('تعذر فتح الفيديو التعليمي',
                style: TextStyle(fontFamily: 'Cairo')),
          ),
        );
      }
    } catch (_) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('تعذر فتح الفيديو التعليمي',
              style: TextStyle(fontFamily: 'Cairo')),
        ),
      );
    }
  }

  Future<void> _confirmLogout() async {
    final ok = await showDialog<bool>(
      context: context,
      builder: (ctx) => Directionality(
        textDirection: TextDirection.rtl,
        child: AlertDialog(
          title: const Text('تسجيل الخروج',
              style: TextStyle(fontFamily: 'Cairo')),
          content: const Text(
            'هل تريد الخروج من التطبيق؟',
            style: TextStyle(fontFamily: 'Cairo'),
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(ctx, false),
              child:
                  const Text('إلغاء', style: TextStyle(fontFamily: 'Cairo')),
            ),
            FilledButton(
              onPressed: () => Navigator.pop(ctx, true),
              child:
                  const Text('خروج', style: TextStyle(fontFamily: 'Cairo')),
            ),
          ],
        ),
      ),
    );
    if (ok == true) {
      await BmSession.instance.clear();
      widget.onLogout();
    }
  }

  @override
  Widget build(BuildContext context) {
    final pages = const [
      DecisionsPage(),
      CustomersPage(),
      NewSalesPage(),
      StatsPage(),
      PaymentsPage(),
      WaiversHistoryPage(),
      DecisionsHistoryPage(),
    ];

    return Directionality(
      textDirection: TextDirection.rtl,
      child: Scaffold(
        appBar: AppBar(
          title: Text(_titles[_index]),
          actions: [
            Padding(
              padding: const EdgeInsets.only(left: 8),
              child: Center(
                child: Text(
                  BmSession.instance.cityName ?? '',
                  style: const TextStyle(
                    fontFamily: 'Cairo',
                    fontSize: 12,
                    color: Colors.white70,
                  ),
                ),
              ),
            ),
          ],
        ),
        drawer: Drawer(
          child: SafeArea(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                DrawerHeader(
                  decoration: const BoxDecoration(color: BmTheme.primary),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      const Text(
                        'مدير الفرع',
                        style: TextStyle(
                          fontFamily: 'Cairo',
                          color: Colors.white,
                          fontSize: 20,
                          fontWeight: FontWeight.w700,
                        ),
                      ),
                      const SizedBox(height: 6),
                      Text(
                        BmSession.instance.cityName ?? '',
                        style: const TextStyle(
                          fontFamily: 'Cairo',
                          color: Colors.white70,
                          fontSize: 13,
                        ),
                      ),
                    ],
                  ),
                ),
                for (var i = 0; i < _menu.length; i++)
                  ListTile(
                    leading: Icon(
                      _index == i ? _menu[i].$2 : _menu[i].$1,
                      color: _index == i ? BmTheme.primary : null,
                    ),
                    title: Text(
                      _menu[i].$3,
                      style: TextStyle(
                        fontFamily: 'Cairo',
                        fontWeight:
                            _index == i ? FontWeight.w700 : FontWeight.w500,
                        color: _index == i ? BmTheme.primary : null,
                      ),
                    ),
                    selected: _index == i,
                    onTap: () => _openPage(i),
                  ),
                const Spacer(),
                const Divider(height: 1),
                ListTile(
                  leading: const Icon(Icons.play_circle_filled_rounded,
                      color: Color(0xFFE6A800)),
                  title: const Text('مشاهدة الفيديو التعليمي',
                      style: TextStyle(fontFamily: 'Cairo')),
                  subtitle: const Text(
                    'شرح استخدام تطبيق مدير الفرع',
                    style: TextStyle(fontFamily: 'Cairo', fontSize: 11),
                  ),
                  onTap: () {
                    Navigator.of(context).maybePop();
                    _openTutorialVideo();
                  },
                ),
                ListTile(
                  leading: const Icon(Icons.logout),
                  title: const Text('خروج',
                      style: TextStyle(fontFamily: 'Cairo')),
                  onTap: () {
                    Navigator.of(context).maybePop();
                    _confirmLogout();
                  },
                ),
              ],
            ),
          ),
        ),
        body: SafeArea(
          top: false,
          bottom: true,
          child: IndexedStack(index: _index, children: pages),
        ),
      ),
    );
  }
}
