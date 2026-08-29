import 'dart:convert';
import 'package:delegate_application/AsyncIdChecker.dart';
import 'package:delegate_application/utils/AppTheme.dart';
import 'package:delegate_application/main.dart';
import 'package:flutter/material.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:http/http.dart' as http;
import 'package:delegate_application/utils/Formatters.dart';
import 'package:delegate_application/services/DatabaseHelper.dart';

class HomePage extends StatefulWidget {
  const HomePage({super.key});

  @override
  _HomePageState createState() => _HomePageState();
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
  double amountDay = 0;
  bool isLoading = true;

  int _selectedIndex = 2; // Default to Home

  @override
  void initState() {
    super.initState();
    _initializeData();
  }

  Future<void> _initializeData() async {
    await _checkAndNavigate();
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
              phone = data['phone'] ?? ''; // Guessing key
              city = data['city'] ?? ''; // Guessing key
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
                        // حذف البيانات وقاعدة البيانات
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
                        // فقط تسجيل الخروج (مسح SharedPreferences)
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

    // Navigation logic matching the bottom bar
    switch (index) {
      case 0: // Sales
        Navigator.pushNamed(context, '/AllSale');
        break;
      case 1: // Customers
        Navigator.pushNamed(context, '/Customer');
        break;
      case 2: // Home (Refresh)
        fetchData();
        break;
      case 3: // Payments
        Navigator.pushNamed(context, '/AllReceipt');
        break;
      case 4: // Sync
        Navigator.pushNamed(context, '/Sync');
        break;
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppTheme.backgroundColor,
      body: Stack(
        children: [
          // Background Elements (Wavy lines placeholder)
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

          SafeArea(
            child: RefreshIndicator(
              onRefresh: fetchData,
              color: AppTheme.primaryColor,
              child: SingleChildScrollView(
                padding: const EdgeInsets.all(20),
                child: Column(
                  children: [
                    _buildHeader(),
                    const SizedBox(height: 30),
                    _buildSummaryCard(),
                    const SizedBox(height: 20),
                    _buildReportSection(),
                    const SizedBox(height: 20),
                    // _buildTrustReceiptsButton(),
                    // const SizedBox(height: 20),
                    TextButton.icon(
                      onPressed: _logout,
                      icon: const Icon(Icons.logout, color: Colors.red),
                      label: const Text('تسجيل الخروج',
                          style: TextStyle(
                              fontFamily: 'Cairo', color: Colors.red)),
                    ),
                    const SizedBox(height: 10),
                    const Text(
                      "الإصدار 30",
                      style: TextStyle(
                          fontFamily: 'Cairo',
                          color: Colors.grey,
                          fontSize: 12),
                    ),
                    const SizedBox(height: 80), // Space for FAB
                  ],
                ),
              ),
            ),
          ),
        ],
      ),
      floatingActionButton: FloatingActionButton(
        onPressed: () => _onItemTapped(2),
        backgroundColor: AppTheme.primaryColor,
        shape: const CircleBorder(),
        elevation: 4,
        child: const Icon(Icons.home, color: Colors.white, size: 30),
      ),
      floatingActionButtonLocation: FloatingActionButtonLocation.centerDocked,
      bottomNavigationBar: BottomAppBar(
        shape: const CircularNotchedRectangle(),
        notchMargin: 10,
        color: Theme.of(context).cardColor,
        child: SizedBox(
          height: 60,
          child: Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              _buildNavItem(Icons.sync, "المزامنة", 4),
              _buildNavItem(
                  Icons.account_balance_wallet_outlined, "التسديدات", 3),
              const SizedBox(width: 40), // Space for FAB
              _buildNavItem(Icons.people_outline, "العملاء", 1),
              _buildNavItem(Icons.monetization_on_outlined, "المبيعات", 0),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildNavItem(IconData icon, String label, int index) {
    bool isSelected = _selectedIndex == index;
    return Expanded(
      child: InkWell(
        onTap: () => _onItemTapped(index),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Icon(
              icon,
              color: isSelected ? AppTheme.primaryColor : Colors.grey,
              size: 24,
            ),
            const SizedBox(height: 2),
            Text(
              label,
              style: TextStyle(
                fontFamily: 'Cairo',
                fontSize: 12,
                color: isSelected ? AppTheme.primaryColor : Colors.grey,
                fontWeight: isSelected ? FontWeight.bold : FontWeight.normal,
              ),
            )
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

        // Logo/Title in Center
// Logo removed

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

  Widget _buildReportSection() {
    return Container(
      decoration: BoxDecoration(
        color: AppTheme.primaryColor
            .withValues(alpha: 0.05), // Light Green background
        borderRadius: BorderRadius.circular(25),
      ),
      padding: const EdgeInsets.all(20),
      child: Column(
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.end,
            children: [
              const Text(
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
          _buildStatCard("سعر البيع", "مجموع سعر القائمة", amountTotal,
              delay: 0),
          _buildStatCard("الأقساط", "مجموع سعر الأقساط", amountDay, delay: 100),
          _buildStatCard("الواصل", "مجموع الأموال المستلمة", amountRecever,
              delay: 300),
          _buildStatCard("الباقي", "مجموع الأموال المتبقية", amountRemaining,
              isTotal: true, delay: 400),
        ],
      ),
    );
  }

  Widget _buildStatCard(String title, String subtitle, double value,
      {bool isTotal = false, int delay = 0}) {
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

  // Widget _buildTrustReceiptsButton() {
  //   return SizedBox(
  //     width: double.infinity,
  //     child: ElevatedButton.icon(
  //       onPressed: () {
  //         Navigator.pushNamed(context, '/TrustReceiptsList');
  //       },
  //       icon: const Icon(Icons.receipt_long, color: Colors.white),
  //       label: const Text(
  //         "وصولات الأمانة",
  //         style: TextStyle(
  //           fontFamily: 'Cairo',
  //           color: Colors.white,
  //           fontWeight: FontWeight.bold,
  //           fontSize: 16,
  //         ),
  //       ),
  //       style: ElevatedButton.styleFrom(
  //         backgroundColor: AppTheme.primaryColor,
  //         padding: const EdgeInsets.symmetric(vertical: 15),
  //         shape: RoundedRectangleBorder(
  //           borderRadius: BorderRadius.circular(15),
  //         ),
  //       ),
  //     ),
  //   );
  // }
}
