import 'dart:async';
import 'dart:convert';

import 'package:delegate_application/AsyncIdChecker.dart';
import 'package:delegate_application/utils/AppTheme.dart';
import 'package:delegate_application/main.dart';
import 'package:delegate_application/customer.dart';
import 'package:delegate_application/core/update/app_update_gate.dart';
import 'package:delegate_application/core/update/app_version.dart';
import 'package:delegate_application/rating_tab_page.dart';
import 'package:delegate_application/services/delegate_data_refresh_service.dart';
import 'package:delegate_application/tutorial_questions_page.dart';
import 'package:delegate_application/today_payments_page.dart';
import 'package:delegate_application/ui/announcements_banner.dart';
import 'package:delegate_application/ui/app_safe_scaffold.dart';
import 'package:delegate_application/ui/delegate_complaint_sheet.dart';
import 'package:delegate_application/ui/main_bottom_nav.dart';
import 'package:delegate_application/services/delegate_business_date_service.dart';
import 'package:flutter/material.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:http/http.dart' as http;
import 'package:delegate_application/utils/Formatters.dart';
import 'package:delegate_application/services/DatabaseHelper.dart';
import 'package:delegate_application/services/phantom_customer_rating.dart';
import 'package:delegate_application/utils/iraq_date.dart';
class HomePage extends StatefulWidget {
  const HomePage({super.key});

  @override
  State<HomePage> createState() => _HomePageState();
}

class _HomePageState extends State<HomePage> with WidgetsBindingObserver {
  String delegateName = 'جار التحميل...';
  String receiptName = 'المندوب';
  String phone = '';
  String city = '';
  bool isLoading = true;
  double _overallPhantomScore = 0;
  double _weekAgoPhantomScore = 0;
  double _installmentSum = 0;
  String _ratingTrend = 'flat';
  bool _kickingSession = false;
  Timer? _sessionWatchdog;

  /// 0 الرئيسية | 1 التسديدات | 2 العملاء | 3 التقييم
  int _selectedIndex = 0;
  final GlobalKey<ScaffoldState> _scaffoldKey = GlobalKey<ScaffoldState>();
  final GlobalKey<TodayPaymentsPageState> _paymentsKey =
      GlobalKey<TodayPaymentsPageState>();
  final GlobalKey<CustomerPageState> _customerKey =
      GlobalKey<CustomerPageState>();
  final GlobalKey<RatingTabPageState> _ratingKey =
      GlobalKey<RatingTabPageState>();

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
    _initializeData();
    _sessionWatchdog = Timer.periodic(const Duration(seconds: 20), (_) {
      unawaited(_enforceCollectionSession());
    });
  }

  @override
  void dispose() {
    _sessionWatchdog?.cancel();
    WidgetsBinding.instance.removeObserver(this);
    super.dispose();
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed) {
      unawaited(_enforceCollectionSession());
    }
  }

  Future<void> _enforceCollectionSession() async {
    if (!mounted || _kickingSession) return;
    final msg = await AsyncIdChecker.enforceSessionKickMessage();
    if (msg == null || !mounted) return;
    _kickingSession = true;
    _sessionWatchdog?.cancel();
    await showDialog<void>(
      context: context,
      barrierDismissible: false,
      builder: (ctx) => Directionality(
        textDirection: TextDirection.rtl,
        child: AlertDialog(
          title: const Text('انتهت الجلسة',
              style: TextStyle(fontFamily: 'Cairo', fontWeight: FontWeight.bold)),
          content: Text(msg, style: const TextStyle(fontFamily: 'Cairo')),
          actions: [
            ElevatedButton(
              onPressed: () => Navigator.pop(ctx),
              child: const Text('حسناً', style: TextStyle(fontFamily: 'Cairo')),
            ),
          ],
        ),
      ),
    );
    if (!mounted) return;
    Navigator.pushNamedAndRemoveUntil(context, '/Login', (r) => false);
  }

  Future<void> _initializeData() async {
    await _checkAndNavigate();
    if (!mounted) return;
    await AppUpdateGate.checkAndPrompt(context);
    if (!mounted) return;
    // Finish master refresh before tabs read SelectDelegate / Customer.
    await DelegateDataRefreshService.instance.refreshIfPossible(force: true);
    if (!mounted) return;
    await _customerKey.currentState
        ?.reloadLists(attemptNetworkRefreshIfEmpty: false);
    await _ratingKey.currentState?.reload();
    await fetchData();
  }

  Future<void> _checkAndNavigate() async {
    bool loggedIn = await AsyncIdChecker.isLoggedIn();
    if (loggedIn) {
      bool isValidSession = await AsyncIdChecker.checkAsyncId();
      if (!isValidSession) {
        final msg = AsyncIdChecker.lastInvalidMessage;
        await AsyncIdChecker.logout();
        if (!mounted) return;
        if (msg != null && msg.isNotEmpty) {
          await showDialog<void>(
            context: context,
            builder: (ctx) => Directionality(
              textDirection: TextDirection.rtl,
              child: AlertDialog(
                title: const Text('تنبيه', style: TextStyle(fontFamily: 'Cairo')),
                content: Text(msg, style: const TextStyle(fontFamily: 'Cairo')),
                actions: [
                  TextButton(
                    onPressed: () => Navigator.pop(ctx),
                    child:
                        const Text('حسناً', style: TextStyle(fontFamily: 'Cairo')),
                  ),
                ],
              ),
            ),
          );
        }
        if (mounted) Navigator.pushReplacementNamed(context, '/Login');
        return;
      }
    } else {
      if (mounted) Navigator.pushReplacementNamed(context, '/Login');
      return;
    }
  }

  Future<void> fetchData() async {
    if (!await AsyncIdChecker.checkAsyncId()) return;

    SharedPreferences prefs = await SharedPreferences.getInstance();
    String linkDelegate = prefs.getString('LinkDelegate') ?? '0';
    int delegateId = int.parse(prefs.getString('DelegateID') ?? '0');

    if (delegateId > 0) {
      try {
        var uri =
            Uri.parse('${linkDelegate}Delegates/GetDelegateTitle/$delegateId/');
        var response =
            await http.get(uri, headers: {"Content-Type": "application/json"});

        if (response.statusCode == 200) {
          var data = json.decode(response.body);
          if (mounted) {
            setState(() {
              delegateName = data['delegateName'] ?? 'No Name';
              receiptName = data['receiptName'] ?? 'Delegate';
              phone = data['phone'] ?? '';
              city = data['city'] ?? '';
              isLoading = false;
            });
          }
        }
      } catch (_) {}
    }
    await _refreshOverallPhantomScore();
  }

  Future<void> _refreshOverallPhantomScore() async {
    try {
      final db = await DatabaseHelper().database;
      final rows = await db.query('Customer');
      final today = IraqDate.calendarDate();
      final weekAgo = today.subtract(const Duration(days: 7));
      final current = PhantomCustomerRating.summarizeCohort(rows, today);
      final prior = PhantomCustomerRating.summarizeCohort(rows, weekAgo);
      if (!mounted) return;
      setState(() {
        _overallPhantomScore = current.averageScore;
        _weekAgoPhantomScore = prior.averageScore;
        _installmentSum = current.installmentSum;
        _ratingTrend = PhantomCustomerRating.trendArrow(
            current.averageScore, prior.averageScore);
      });
    } catch (_) {}
  }

  Future<void> _openTutorialVideo() async {
    if (!mounted) return;
    await Navigator.of(context).push(
      MaterialPageRoute(builder: (_) => const TutorialQuestionsPage()),
    );
  }

  Future<void> _logout() async {
    showDialog(
      context: context,
      builder: (BuildContext context) {
        return Directionality(
            textDirection: TextDirection.rtl,
            child: AlertDialog(
              shape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(20)),
              title: const Row(
                children: [
                  Icon(Icons.logout, color: Colors.red),
                  SizedBox(width: 10),
                  Text("تسجيل الخروج",
                      style: TextStyle(
                          fontFamily: 'Cairo', fontWeight: FontWeight.bold)),
                ],
              ),
              content: const Text("اختر طريقة تسجيل الخروج التي تفضلها.",
                  style: TextStyle(fontFamily: 'Cairo')),
              actions: [
                Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    ElevatedButton.icon(
                      icon:
                          const Icon(Icons.delete_forever, color: Colors.white),
                      label: const Text("حذف البيانات وتسجيل الخروج",
                          style: TextStyle(
                              fontFamily: 'Cairo', color: Colors.white)),
                      style: ElevatedButton.styleFrom(
                        backgroundColor: Colors.red,
                        shape: RoundedRectangleBorder(
                            borderRadius: BorderRadius.circular(10)),
                      ),
                      onPressed: () async {
                        final unresolved =
                            await DatabaseHelper().countUnresolvedPayments();
                        if (unresolved > 0) {
                          if (context.mounted) {
                            ScaffoldMessenger.of(context).showSnackBar(
                              SnackBar(
                                content: Text(
                                  'لا يمكن حذف البيانات: يوجد $unresolved تسديد غير مُرسَل/مرفوض. احفظ البيانات أو أرسل التسديدات أولاً.',
                                  style: const TextStyle(fontFamily: 'Cairo'),
                                ),
                                backgroundColor: Colors.red.shade700,
                              ),
                            );
                          }
                          return;
                        }
                        await DatabaseHelper().clearAllTables();
                        await AsyncIdChecker.logout();
                        if (context.mounted) {
                          Navigator.of(context).pop();
                          Navigator.pushReplacementNamed(context, '/Login');
                        }
                      },
                    ),
                    const SizedBox(height: 10),
                    OutlinedButton.icon(
                      icon:
                          const Icon(Icons.save, color: AppTheme.primaryColor),
                      label: const Text("حفظ البيانات وتسجيل الخروج",
                          style: TextStyle(
                              fontFamily: 'Cairo',
                              color: AppTheme.primaryColor)),
                      style: OutlinedButton.styleFrom(
                        side: const BorderSide(color: AppTheme.primaryColor),
                        shape: RoundedRectangleBorder(
                            borderRadius: BorderRadius.circular(10)),
                      ),
                      onPressed: () async {
                        await AsyncIdChecker.logout();
                        if (context.mounted) {
                          Navigator.of(context).pop();
                          Navigator.pushReplacementNamed(context, '/Login');
                        }
                      },
                    ),
                    const SizedBox(height: 10),
                    TextButton(
                      child: const Text("إلغاء",
                          style: TextStyle(
                              fontFamily: 'Cairo', color: Colors.grey)),
                      onPressed: () {
                        Navigator.of(context).pop();
                      },
                    ),
                  ],
                )
              ],
            ));
      },
    );
  }

  void _onItemTapped(int index) {
    unawaited(_enforceCollectionSession());
    setState(() {
      _selectedIndex = index;
    });
    if (index == 0) {
      fetchData();
    }
    if (index == 1) {
      unawaited(_paymentsKey.currentState?.reload() ?? Future.value());
      unawaited(DelegateBusinessDateService.instance.ensureCurrentBusinessDay());
    }
    if (index == 2) {
      _customerKey.currentState
          ?.reloadLists(attemptNetworkRefreshIfEmpty: true);
      unawaited(DelegateBusinessDateService.instance.ensureCurrentBusinessDay());
    }
    if (index == 3) {
      _ratingKey.currentState?.reload();
    }
  }

  @override
  Widget build(BuildContext context) {
    return Directionality(
      textDirection: TextDirection.rtl,
      child: AppSafeScaffold(
        scaffoldKey: _scaffoldKey,
        backgroundColor: AppTheme.backgroundColor,
        extendBody: false,
        safeBottom: false,
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
                  leading: const Icon(Icons.receipt_long_outlined),
                  title: const Text(
                    'طلبات البيع',
                    style: TextStyle(fontFamily: 'Cairo'),
                  ),
                  onTap: () {
                    Navigator.pop(context);
                    Navigator.pushNamed(context, '/DelegateSalesRequests');
                  },
                ),
              ],
            ),
          ),
        ),
        floatingActionButtonLocation: FloatingActionButtonLocation.startFloat,
        floatingActionButton: SafeArea(
          child: FloatingActionButton(
            heroTag: 'delegate_complaint_fab',
            tooltip: 'شكوى إلى المدير المفوض',
            backgroundColor: AppTheme.primaryColor,
            onPressed: () => DelegateComplaintSheet.show(context),
            child: const Icon(Icons.chat_bubble_outline, color: Colors.white),
          ),
        ),
        bottomNavigationBar: MainBottomNavBar(
          selectedIndex: _selectedIndex,
          onTap: _onItemTapped,
        ),
        body: IndexedStack(
          index: _selectedIndex,
          children: [
            _buildHomeDashboard(),
            TodayPaymentsPage(key: _paymentsKey, embedded: true),
            Customer(key: _customerKey, embedded: true),
            RatingTabPage(key: _ratingKey),
          ],
        ),
      ),
    );
  }

  Widget _buildHomeDashboard() {
    return Stack(
      children: [
        Positioned(
          top: -50,
          left: -50,
          child: Container(
            width: 200,
            height: 200,
            decoration: BoxDecoration(
              shape: BoxShape.circle,
              border: Border.all(
                  color: AppTheme.primaryColor.withValues(alpha: 0.05),
                  width: 30),
            ),
          ),
        ),
        RefreshIndicator(
          onRefresh: () async {
            await DelegateDataRefreshService.instance.refreshIfPossible();
            await fetchData();
          },
          color: AppTheme.primaryColor,
          child: SingleChildScrollView(
            padding: AppInsets.scrollPadding(context),
            child: Column(
              children: [
                ValueListenableBuilder<String?>(
                  valueListenable:
                      DelegateDataRefreshService.instance.statusMessage,
                  builder: (context, msg, _) {
                    if (msg == null || msg.isEmpty) {
                      return const SizedBox.shrink();
                    }
                    return Padding(
                      padding: const EdgeInsets.only(bottom: 10),
                      child: Text(
                        msg,
                        style: TextStyle(
                          fontFamily: 'Cairo',
                          color: Colors.green[700],
                          fontSize: 13,
                        ),
                      ),
                    );
                  },
                ),
                _buildHeader(),
                const SizedBox(height: 16),
                const AnnouncementsBanner(),
                const SizedBox(height: 8),
                _buildWeekRatingCard(),
                const SizedBox(height: 20),
                SizedBox(
                  width: double.infinity,
                  child: ElevatedButton.icon(
                    onPressed: _openTutorialVideo,
                    icon: const Icon(Icons.play_circle_filled_rounded,
                        color: Colors.white, size: 28),
                    label: const Text(
                      'مشاهدة الفيديو التعليمي',
                      style: TextStyle(
                        fontFamily: 'Cairo',
                        color: Colors.white,
                        fontWeight: FontWeight.bold,
                        fontSize: 16,
                      ),
                    ),
                    style: ElevatedButton.styleFrom(
                      backgroundColor: const Color(0xFFFFC107),
                      padding: const EdgeInsets.symmetric(vertical: 14),
                      shape: RoundedRectangleBorder(
                          borderRadius: BorderRadius.circular(12)),
                    ),
                  ),
                ),
                const SizedBox(height: 10),
                SizedBox(
                  width: double.infinity,
                  child: OutlinedButton.icon(
                    onPressed: () =>
                        DelegateComplaintSheet.show(context, asNote: true),
                    icon: const Icon(Icons.note_alt_outlined, size: 22),
                    label: const Text(
                      'إرسال ملاحظة إلى المدير',
                      style: TextStyle(
                        fontFamily: 'Cairo',
                        fontWeight: FontWeight.bold,
                        fontSize: 15,
                      ),
                    ),
                    style: OutlinedButton.styleFrom(
                      foregroundColor: AppTheme.primaryColor,
                      side: BorderSide(color: AppTheme.primaryColor),
                      padding: const EdgeInsets.symmetric(vertical: 12),
                      shape: RoundedRectangleBorder(
                          borderRadius: BorderRadius.circular(12)),
                    ),
                  ),
                ),
                const SizedBox(height: 12),
                SizedBox(
                  width: double.infinity,
                  child: ElevatedButton.icon(
                    onPressed: () {
                      Navigator.pushNamed(
                        context,
                        '/DelegateSalesRequest',
                        arguments: {'saleRequestType': 'New'},
                      );
                    },
                    icon: const Icon(Icons.add_shopping_cart,
                        color: Colors.white),
                    label: const Text(
                      'طلب مبيع جديد',
                      style:
                          TextStyle(fontFamily: 'Cairo', color: Colors.white),
                    ),
                    style: ElevatedButton.styleFrom(
                      backgroundColor: AppTheme.primaryColor,
                      padding: const EdgeInsets.symmetric(vertical: 14),
                      shape: RoundedRectangleBorder(
                          borderRadius: BorderRadius.circular(12)),
                    ),
                  ),
                ),
                const SizedBox(height: 20),
                TextButton.icon(
                  onPressed: _logout,
                  icon: const Icon(Icons.logout, color: Colors.red),
                  label: const Text('تسجيل الخروج',
                      style: TextStyle(fontFamily: 'Cairo', color: Colors.red)),
                ),
                const SizedBox(height: 10),
                Text(
                  'الإصدار ${AppVersion.display}',
                  style: const TextStyle(
                      fontFamily: 'Cairo', color: Colors.grey, fontSize: 12),
                ),
              ],
            ),
          ),
        ),
      ],
    );
  }

  Widget _phantomScoreChip(String value) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: Colors.black.withValues(alpha: 0.08)),
      ),
      child: Text(
        value,
        style: const TextStyle(
          fontFamily: 'Cairo',
          fontWeight: FontWeight.w900,
          fontSize: 22,
          color: Colors.black,
          height: 1.1,
        ),
      ),
    );
  }

  Widget _buildHeader() {
    return Row(
      children: [
        Container(
          decoration: BoxDecoration(
            shape: BoxShape.circle,
            color: Theme.of(context).cardColor,
            boxShadow: [
              BoxShadow(
                  color: Colors.grey.withValues(alpha: 0.1),
                  blurRadius: 10,
                  offset: const Offset(0, 4)),
            ],
          ),
          child: IconButton(
            tooltip: 'القائمة',
            icon: Icon(Icons.menu, color: AppTheme.textColor),
            onPressed: () => _scaffoldKey.currentState?.openDrawer(),
          ),
        ),
        const SizedBox(width: 4),
        Container(
          decoration: BoxDecoration(
            shape: BoxShape.circle,
            color: Theme.of(context).cardColor,
            boxShadow: [
              BoxShadow(
                  color: Colors.grey.withValues(alpha: 0.1),
                  blurRadius: 10,
                  offset: const Offset(0, 4)),
            ],
          ),
          child: IconButton(
            icon: Icon(
              MyApp.themeNotifier.value == ThemeMode.light
                  ? Icons.dark_mode
                  : Icons.light_mode,
              color: AppTheme.textColor,
            ),
            onPressed: () {
              MyApp.themeNotifier.value =
                  MyApp.themeNotifier.value == ThemeMode.light
                      ? ThemeMode.dark
                      : ThemeMode.light;
              setState(() {});
            },
          ),
        ),
        const SizedBox(width: 8),
        _phantomScoreChip(
            PhantomCustomerRating.formatAverageScore(_overallPhantomScore)),
        const Spacer(),
        Column(
          crossAxisAlignment: CrossAxisAlignment.end,
          children: [
            Text(
              receiptName,
              style: TextStyle(
                fontFamily: 'Cairo',
                fontWeight: FontWeight.bold,
                fontSize: 16,
                color: Theme.of(context).textTheme.bodyLarge?.color,
              ),
            ),
            Text(
              delegateName.length > 20
                  ? "${delegateName.substring(0, 15)}..."
                  : delegateName,
              style: const TextStyle(
                fontFamily: 'Cairo',
                fontSize: 12,
                color: Colors.grey,
              ),
            ),
          ],
        ),
        const SizedBox(width: 10),
        InkWell(
          onTap: _logout,
          child: const CircleAvatar(
            radius: 22,
            backgroundColor: AppTheme.primaryColor,
            child: Icon(Icons.person, color: Colors.white),
          ),
        ),
      ],
    );
  }

  IconData _trendIcon(String trend) {
    switch (trend) {
      case 'up':
        return Icons.arrow_upward_rounded;
      case 'down':
        return Icons.arrow_downward_rounded;
      default:
        return Icons.drag_handle_rounded;
    }
  }

  Color _trendColor(String trend) {
    switch (trend) {
      case 'up':
        return Colors.green.shade700;
      case 'down':
        return Colors.red.shade700;
      default:
        return Colors.grey.shade600;
    }
  }

  Widget _buildWeekRatingCard() {
    return Container(
      width: double.infinity,
      decoration: BoxDecoration(
        color: Theme.of(context).cardColor,
        borderRadius: BorderRadius.circular(25),
        boxShadow: [
          BoxShadow(
            color: Colors.black.withValues(alpha: 0.05),
            blurRadius: 20,
            offset: const Offset(0, 10),
          )
        ],
      ),
      padding: const EdgeInsets.all(20),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            children: [
              Expanded(
                child: _weekMetric(
                  'الأسبوع السابق',
                  PhantomCustomerRating.formatAverageScore(_weekAgoPhantomScore),
                ),
              ),
              Padding(
                padding: const EdgeInsets.symmetric(horizontal: 8),
                child: Icon(
                  _trendIcon(_ratingTrend),
                  color: _trendColor(_ratingTrend),
                  size: 32,
                ),
              ),
              Expanded(
                child: _weekMetric(
                  'الحالي',
                  PhantomCustomerRating.formatAverageScore(_overallPhantomScore),
                ),
              ),
            ],
          ),
          const Divider(height: 28),
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text(
                '${Formatters.formatNumber(_installmentSum)} دع',
                style: const TextStyle(
                  fontFamily: 'Cairo',
                  fontWeight: FontWeight.bold,
                  fontSize: 15,
                ),
              ),
              const Text(
                'مجموع القسط',
                style: TextStyle(fontFamily: 'Cairo', color: Colors.grey),
              ),
            ],
          ),
        ],
      ),
    );
  }

  Widget _weekMetric(String label, String value) {
    return Column(
      children: [
        Text(label,
            style: const TextStyle(
                fontFamily: 'Cairo', color: Colors.grey, fontSize: 12)),
        const SizedBox(height: 6),
        Text(
          value,
          style: const TextStyle(
            fontFamily: 'Cairo',
            fontWeight: FontWeight.w900,
            fontSize: 22,
            color: Colors.black,
          ),
        ),
      ],
    );
  }
}
