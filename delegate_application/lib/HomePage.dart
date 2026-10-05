import 'dart:async';
import 'dart:convert';

import 'package:delegate_application/AsyncIdChecker.dart';
import 'package:delegate_application/AllSale.dart';
import 'package:delegate_application/utils/AppTheme.dart';
import 'package:delegate_application/main.dart';
import 'package:delegate_application/customer.dart';
import 'package:delegate_application/core/update/app_update_gate.dart';
import 'package:delegate_application/core/update/app_version.dart';
import 'package:delegate_application/services/delegate_data_refresh_service.dart';
import 'package:delegate_application/services/manual_payment_sync_result.dart';
import 'package:delegate_application/services/payment_sync_service.dart';
import 'package:delegate_application/services/tutorial_video_service.dart';
import 'package:delegate_application/today_payments_page.dart';
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
import 'package:url_launcher/url_launcher.dart';

class HomePage extends StatefulWidget {
  const HomePage({super.key});

  @override
  State<HomePage> createState() => _HomePageState();
}

class _HomePageState extends State<HomePage> {
  String delegateName = 'جار التحميل...';
  String receiptName = 'المندوب';
  String phone = '';
  String city = '';
  int numberOfCustomer = 0;
  int numberOfCustomerIsLegal = 0;
  int numberOfCustomerIsNotZero = 0;
  int numberOfCustomerIsZero = 0;
  double amountRecever = 0;
  double amountRemaining = 0;
  double amountTotal = 0;
  bool _sendingPayments = false;
  double amountDay = 0;
  bool isLoading = true;
  double _overallPhantomScore = 0;

  int _selectedIndex = 2; // Default to Home
  final GlobalKey<TodayPaymentsPageState> _todayKey =
      GlobalKey<TodayPaymentsPageState>();
  final GlobalKey<CustomerPageState> _customerKey =
      GlobalKey<CustomerPageState>();

  @override
  void initState() {
    super.initState();
    _initializeData();
  }

  Future<void> _initializeData() async {
    await _checkAndNavigate();
    if (!mounted) return;
    await AppUpdateGate.checkAndPrompt(context);
    if (!mounted) return;
    await DelegateDataRefreshService.instance.refreshIfPossible();
    // Customer tab may have loaded empty SelectDelegate during IndexedStack build.
    await _customerKey.currentState
        ?.reloadLists(attemptNetworkRefreshIfEmpty: false);
    await fetchData();
  }

  Future<void> _checkAndNavigate() async {
    bool loggedIn = await AsyncIdChecker.isLoggedIn();
    if (loggedIn) {
      bool isValidSession = await AsyncIdChecker.checkAsyncId();
      if (!isValidSession) {
        await AsyncIdChecker.logout();
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
              numberOfCustomer = data['numberOfCustomer'] ?? 0;
              numberOfCustomerIsLegal = data['numberOfCustomerIsLegal'] ?? 0;
              numberOfCustomerIsNotZero =
                  data['numberOfCustomerIsNotZero'] ?? 0;
              numberOfCustomerIsZero = data['numberOfCustomerIsZero'] ?? 0;
              amountRecever = data['amountRecever']?.toDouble() ?? 0;
              amountRemaining = data['amountRemaining']?.toDouble() ?? 0;
              amountTotal = data['amountTotal']?.toDouble() ?? 0;
              amountDay = data['amountDay']?.toDouble() ?? 0;
              isLoading = false;
            });
          }
        }
      } catch (e) {
        // Log error
      }
    }
    await _refreshOverallPhantomScore();
  }

  Future<void> _refreshOverallPhantomScore() async {
    try {
      final db = await DatabaseHelper().database;
      final rows = await db.query('Customer');
      // تقييمك = متوسط من سدّد خلال 30 يوم فقط (الباقي > 0).
      final score = PhantomCustomerRating.averageCustomerScore(
        rows,
        IraqDate.calendarDate(),
      );
      if (!mounted) return;
      setState(() => _overallPhantomScore = score);
    } catch (_) {
      // Keep last known score if local DB is unavailable.
    }
  }

  Future<void> _sendPendingPayments() async {
    if (_sendingPayments) return;
    setState(() => _sendingPayments = true);
    try {
      final result =
          await PaymentSyncService.instance.sendPendingPaymentsNow();
      if (!mounted) return;
      final message = result.summaryMessage();
      final ok = result.isCompleteSuccess ||
          (result.outcome == ManualPaymentSyncOutcome.nothingPending &&
              result.permanentFailures == 0);
      ScaffoldMessenger.of(context).hideCurrentSnackBar();
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(message, style: const TextStyle(fontFamily: 'Cairo')),
          backgroundColor:
              ok ? Colors.teal.shade700 : Colors.orange.shade800,
          behavior: SnackBarBehavior.floating,
          duration: const Duration(seconds: 6),
        ),
      );
      if (result.outcome == ManualPaymentSyncOutcome.uploaded ||
          result.outcome == ManualPaymentSyncOutcome.partial) {
        TodayPaymentsRefresh.notify();
        await _customerKey.currentState
            ?.reloadLists(attemptNetworkRefreshIfEmpty: false);
        await fetchData();
      }
    } finally {
      if (mounted) setState(() => _sendingPayments = false);
    }
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
    setState(() {
      _selectedIndex = index;
    });
    if (index == 1) {
      // IndexedStack may have created Customer before SelectDelegate was filled.
      _customerKey.currentState
          ?.reloadLists(attemptNetworkRefreshIfEmpty: true);
      unawaited(DelegateBusinessDateService.instance.ensureCurrentBusinessDay());
    }
    if (index == 2) {
      fetchData();
    }
    if (index == 3) {
      unawaited(DelegateBusinessDateService.instance.ensureCurrentBusinessDay());
      _todayKey.currentState?.reload();
    }
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
        Positioned(
          top: 50,
          right: -80,
          child: Container(
            width: 300,
            height: 300,
            decoration: BoxDecoration(
              shape: BoxShape.circle,
              border: Border.all(
                  color: AppTheme.primaryColor.withValues(alpha: 0.05),
                  width: 50),
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
                const SizedBox(height: 16),
                _buildHeader(),
                const SizedBox(height: 30),
                _buildSummaryCard(),
                const SizedBox(height: 20),
                _buildOverallRatingSection(),
                const SizedBox(height: 12),
                _buildReportSection(),
                const SizedBox(height: 20),
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
                const SizedBox(height: 12),
                SizedBox(
                  width: double.infinity,
                  child: ElevatedButton.icon(
                    onPressed: _sendingPayments ? null : _sendPendingPayments,
                    icon: _sendingPayments
                        ? const SizedBox(
                            width: 22,
                            height: 22,
                            child: CircularProgressIndicator(
                              strokeWidth: 2.4,
                              color: Colors.white,
                            ),
                          )
                        : const Icon(Icons.cloud_upload_rounded,
                            color: Colors.white),
                    label: Text(
                      _sendingPayments
                          ? 'جاري إرسال التسديدات...'
                          : 'إرسال التسديدات',
                      style: const TextStyle(
                        fontFamily: 'Cairo',
                        color: Colors.white,
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                    style: ElevatedButton.styleFrom(
                      backgroundColor: Colors.teal.shade700,
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

  @override
  Widget build(BuildContext context) {
    return Directionality(
      textDirection: TextDirection.rtl,
      child: AppSafeScaffold(
        backgroundColor: AppTheme.backgroundColor,
        extendBody: false,
        safeBottom: false,
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
            const AllSale(embedded: true),
            Customer(key: _customerKey, embedded: true),
            _buildHomeDashboard(),
            TodayPaymentsPage(key: _todayKey, embedded: true),
          ],
        ),
      ),
    );
  }

  Widget _buildHeader() {
    return Row(
      mainAxisAlignment: MainAxisAlignment.spaceBetween,
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
        Row(
          children: [
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
        ),
      ],
    );
  }

  Widget _buildSummaryCard() {
    return Container(
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
      padding: const EdgeInsets.all(25),
      child: Column(
        children: [
          _buildInfoRow("اسم المندوب", delegateName),
          if (phone.isNotEmpty) _buildInfoRow("رقم الهاتف", phone),
          if (city.isNotEmpty) _buildInfoRow("المدينة", city),
          const Divider(),
          _buildInfoRow("عدد العملاء الكلي", "$numberOfCustomer", isBold: true),
          _buildInfoRow("العملاء المصفرين", "$numberOfCustomerIsZero",
              color: Colors.green),
          _buildInfoRow("العملاء الغير مصفرين", "$numberOfCustomerIsNotZero",
              color: Colors.orange),
          _buildInfoRow("العملاء القانونية", "$numberOfCustomerIsLegal",
              color: Colors.red),
        ],
      ),
    );
  }

  Widget _buildInfoRow(String label, String value,
      {bool isBold = false, Color? color}) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 8.0),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Text(
            value,
            style: TextStyle(
              fontFamily: 'Cairo',
              fontWeight: isBold ? FontWeight.bold : FontWeight.normal,
              fontSize: 14,
              color: color ?? Theme.of(context).textTheme.bodyLarge?.color,
            ),
          ),
          Text(
            label,
            style: const TextStyle(
              fontFamily: 'Cairo',
              color: Colors.grey,
              fontSize: 14,
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildOverallRatingSection() {
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
      padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 18),
      child: Row(
        textDirection: TextDirection.rtl,
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          const Text(
            'تقييمك :',
            style: TextStyle(
              fontFamily: 'Cairo',
              fontWeight: FontWeight.bold,
              fontSize: 18,
              color: AppTheme.textColor,
            ),
          ),
          const SizedBox(width: 8),
          Text(
            PhantomCustomerRating.formatAverageScore(_overallPhantomScore),
            style: const TextStyle(
              fontFamily: 'Cairo',
              fontWeight: FontWeight.w800,
              fontSize: 22,
              color: Color(0xFFE6A800),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildReportSection() {
    return Container(
      decoration: BoxDecoration(
        color: AppTheme.primaryColor.withValues(alpha: 0.05),
        borderRadius: BorderRadius.circular(25),
      ),
      padding: const EdgeInsets.all(20),
      child: Column(
        children: [
          const Row(
            mainAxisAlignment: MainAxisAlignment.end,
            children: [
              Text(
                "التقرير المالي",
                style: TextStyle(
                    fontFamily: 'Cairo',
                    fontWeight: FontWeight.bold,
                    fontSize: 18,
                    color: AppTheme.primaryColor),
              ),
            ],
          ),
          const SizedBox(height: 20),
          _buildStatCard("سعر البيع", "مجموع سعر القائمة", amountTotal),
          _buildStatCard("الأقساط", "مجموع سعر الأقساط", amountDay),
          _buildStatCard("الواصل", "مجموع الأموال المستلمة", amountRecever),
          _buildStatCard("الباقي", "مجموع الأموال المتبقية", amountRemaining,
              isTotal: true),
        ],
      ),
    );
  }

  Widget _buildStatCard(String title, String subtitle, double value,
      {bool isTotal = false}) {
    return Container(
      margin: const EdgeInsets.only(bottom: 12),
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: Theme.of(context).cardColor,
        borderRadius: BorderRadius.circular(16),
        border:
            isTotal ? Border.all(color: AppTheme.primaryColor, width: 1) : null,
        boxShadow: [
          BoxShadow(
            color: Colors.grey.withValues(alpha: 0.05),
            blurRadius: 10,
            offset: const Offset(0, 2),
          ),
        ],
      ),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Text(
                "دع",
                style: TextStyle(
                  fontFamily: 'Cairo',
                  fontWeight: FontWeight.bold,
                  fontSize: 16,
                  color: AppTheme.textColor,
                ),
              ),
              const SizedBox(width: 4),
              Text(
                Formatters.formatNumber(value),
                textDirection: TextDirection.rtl,
                style: const TextStyle(
                  fontFamily: 'Cairo',
                  fontWeight: FontWeight.bold,
                  fontSize: 16,
                  color: AppTheme.textColor,
                ),
              ),
            ],
          ),
          Column(
            crossAxisAlignment: CrossAxisAlignment.end,
            children: [
              Text(
                title,
                style: TextStyle(
                  fontFamily: 'Cairo',
                  fontWeight: FontWeight.bold,
                  fontSize: 15,
                  color: Theme.of(context).textTheme.bodyLarge?.color,
                ),
              ),
              Text(
                subtitle,
                style: TextStyle(
                  fontFamily: 'Cairo',
                  fontSize: 11,
                  color: Colors.grey[500],
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}
