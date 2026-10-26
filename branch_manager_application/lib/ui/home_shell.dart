import 'package:branch_manager_application/core/api.dart';
import 'package:branch_manager_application/core/session.dart';
import 'package:branch_manager_application/core/update/app_update_gate.dart';
import 'package:branch_manager_application/theme/bm_theme.dart';
import 'package:branch_manager_application/ui/assigned_tasks_page.dart';
import 'package:branch_manager_application/ui/branch_visit_depart_sheet.dart';
import 'package:branch_manager_application/ui/customers_page.dart';
import 'package:branch_manager_application/ui/decisions_page.dart';
import 'package:branch_manager_application/ui/declining_customers_page.dart';
import 'package:branch_manager_application/ui/history_page.dart';
import 'package:branch_manager_application/ui/new_sales_page.dart';
import 'package:branch_manager_application/ui/payments_page.dart';
import 'package:branch_manager_application/ui/stats_page.dart';
import 'package:branch_manager_application/ui/waivers_history_page.dart';
import 'package:branch_manager_application/ui/wrong_sales_page.dart';
import 'package:branch_manager_application/ui/request_sheets.dart';
import 'package:branch_manager_application/ui/tutorial_questions_page.dart';
import 'package:flutter/material.dart';

class HomeShell extends StatefulWidget {
  const HomeShell({
    super.key,
    required this.onLogout,
    required this.onDepartBranch,
    /// Returns false when entry form must be shown again (ratings incomplete).
    required this.onEnsureVisitReadyForDepart,
  });

  final VoidCallback onLogout;
  final VoidCallback onDepartBranch;
  final Future<bool> Function() onEnsureVisitReadyForDepart;

  @override
  State<HomeShell> createState() => _HomeShellState();
}

class _HomeShellState extends State<HomeShell> {
  int _index = 0;
  bool _leaving = false;

  /// Leaving the province is NEVER free: ratings + long note required.
  Future<void> _onProvinceOrDepart(BuildContext context) async {
    if (_leaving) return;
    setState(() => _leaving = true);
    try {
      final ready = await widget.onEnsureVisitReadyForDepart();
      if (!ready) return; // parent switched to entry form

      var visitId = (BmSession.instance.activeVisitId ?? '').trim();
      if (visitId.isEmpty) {
        if (!mounted) return;
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text(
              'لا توجد زيارة مفتوحة — أعد فتح المحافظة من القائمة',
              style: TextStyle(fontFamily: 'Cairo'),
            ),
          ),
        );
        return;
      }

      final left = await showBranchVisitDepartSheet(context, visitId: visitId);
      if (left && mounted) {
        widget.onDepartBranch();
      }
    } finally {
      if (mounted) setState(() => _leaving = false);
    }
  }

  static const _titles = [
    'مهامي',
    'أسماء قانونية',
    'الزبائن المتدنية',
    'الزبائن',
    'المبيعات',
    'المبيعات الخاطئة',
    'التقييم',
    'التسديدات',
    'طلبات التنازل',
    'سجل القرارات',
  ];

  final _menu = const [
    (Icons.assignment_outlined, Icons.assignment, 'مهامي'),
    (Icons.gavel_outlined, Icons.gavel, 'أسماء قانونية'),
    (Icons.trending_down_outlined, Icons.trending_down, 'الزبائن المتدنية'),
    (Icons.people_outline, Icons.people, 'الزبائن'),
    (Icons.point_of_sale_outlined, Icons.point_of_sale, 'المبيعات'),
    (Icons.report_outlined, Icons.report, 'المبيعات الخاطئة'),
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
    if (!mounted) return;
    await Navigator.of(context).push(
      MaterialPageRoute(builder: (_) => const TutorialQuestionsPage()),
    );
  }

  Future<void> _confirmLogout() async {
    // Cannot logout from inside a province without departing (note + ratings).
    final visitId = (BmSession.instance.activeVisitId ?? '').trim();
    if (visitId.isNotEmpty || BmSession.instance.isLoggedIn) {
      await _onProvinceOrDepart(context);
      return;
    }
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
      AssignedTasksPage(),
      DecisionsPage(),
      DecliningCustomersPage(),
      CustomersPage(),
      NewSalesPage(),
      WrongSalesPage(),
      StatsPage(),
      PaymentsPage(),
      WaiversHistoryPage(),
      DecisionsHistoryPage(),
    ];

    final inVisit = (BmSession.instance.activeVisitId ?? '').isNotEmpty ||
        BmSession.instance.isLoggedIn;

    return Directionality(
      textDirection: TextDirection.rtl,
      child: Scaffold(
        appBar: AppBar(
          title: Text(_titles[_index]),
          actions: [
            IconButton(
              tooltip: 'مغادرة الفرع',
              onPressed: _leaving ? null : () => _onProvinceOrDepart(context),
              icon: _leaving
                  ? const SizedBox(
                      width: 22,
                      height: 22,
                      child: CircularProgressIndicator(
                          strokeWidth: 2, color: Colors.white),
                    )
                  : const Icon(Icons.logout),
            ),
            Padding(
              padding: const EdgeInsets.only(left: 8, right: 4),
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
                        'تطبيق إدارة الفرع',
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
                      const SizedBox(height: 4),
                      const Text(
                        'v1.0.41',
                        style: TextStyle(
                          fontFamily: 'Cairo',
                          color: Colors.white54,
                          fontSize: 11,
                        ),
                      ),
                    ],
                  ),
                ),
                Expanded(
                  child: ListView(
                    padding: EdgeInsets.zero,
                    children: [
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
                      const Divider(height: 1),
                      ListTile(
                        leading: const Icon(Icons.play_circle_filled_rounded,
                            color: Color(0xFFE6A800)),
                        title: const Text('مشاهدة الفيديو التعليمي',
                            style: TextStyle(fontFamily: 'Cairo')),
                        subtitle: const Text(
                          'شرح استخدام تطبيق إدارة الفرع',
                          style: TextStyle(fontFamily: 'Cairo', fontSize: 11),
                        ),
                        onTap: () {
                          Navigator.of(context).maybePop();
                          _openTutorialVideo();
                        },
                      ),
                      ListTile(
                        leading: Icon(Icons.note_alt_outlined,
                            color: BmTheme.primary),
                        title: const Text('إرسال ملاحظة إلى المدير',
                            style: TextStyle(fontFamily: 'Cairo')),
                        subtitle: const Text(
                          'اكتب ملاحظة عن البرنامج لغرض التطوير أو الإصلاح',
                          style: TextStyle(fontFamily: 'Cairo', fontSize: 11),
                        ),
                        onTap: () {
                          Navigator.of(context).maybePop();
                          DelegatedManagerRequestSheet.show(context,
                              asNote: true);
                        },
                      ),
                      ListTile(
                        leading: const Icon(Icons.logout),
                        title: Text(
                          inVisit ? 'مغادرة الفرع' : 'اختيار المحافظة',
                          style: const TextStyle(fontFamily: 'Cairo'),
                        ),
                        subtitle: inVisit
                            ? const Text(
                                'يتطلب تقييم المندوبين + ملاحظة طويلة',
                                style: TextStyle(
                                    fontFamily: 'Cairo', fontSize: 11),
                              )
                            : null,
                        onTap: _leaving
                            ? null
                            : () async {
                                Navigator.of(context).maybePop();
                                await _onProvinceOrDepart(context);
                              },
                      ),
                      ListTile(
                        leading: const Icon(Icons.power_settings_new),
                        title: const Text('خروج من الحساب',
                            style: TextStyle(fontFamily: 'Cairo')),
                        onTap: () {
                          Navigator.of(context).maybePop();
                          _confirmLogout();
                        },
                      ),
                    ],
                  ),
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
