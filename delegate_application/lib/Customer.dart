import 'dart:convert';
import 'package:delegate_application/utils/AppTheme.dart';

import 'package:connectivity_plus/connectivity_plus.dart';
import 'package:delegate_application/AllReceiptCustomer.dart';
import 'package:delegate_application/AllSaleCustomer.dart';
import 'package:delegate_application/AsyncIdChecker.dart';
import 'package:delegate_application/CustomerNoReceiptReport.dart';
import 'package:delegate_application/CustomerReceiptReport.dart';
import 'package:delegate_application/ReportReceipt.dart';
import 'package:flutter/material.dart';
import 'package:geolocator/geolocator.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:delegate_application/services/DatabaseHelper.dart';
import 'package:delegate_application/utils/Formatters.dart';
import 'package:http/http.dart' as http;
import 'package:url_launcher/url_launcher.dart';

class Customer extends StatefulWidget {
  const Customer({super.key});

  @override
  State<Customer> createState() => _CustomerState();
}

class _CustomerState extends State<Customer> {
  // late Database _db;
  List<Map<String, dynamic>> representatives = [];
  List<Client> clients = [];
  String? selectedRepresentative;
  String searchQuery = '';
  String searchQueryReceipt = '';
  double totalPayments = 0.0; // إجمالي التسديدات
  int paymentCount = 0; // عدد التسديدات
  List<Map<String, dynamic>> payments = [];

  bool isButtonDisabled = false;
  String customerType = 'العملاء المستمرين';

  bool isContinuous(String? lastPaymentDate, String? dateSaleDevice) {
    bool isLastPaymentRecent = false;
    bool isDateSaleRecent = false;

    if (lastPaymentDate != null && lastPaymentDate.isNotEmpty) {
      try {
        DateTime date = DateTime.parse(lastPaymentDate);
        isLastPaymentRecent = DateTime.now().difference(date).inDays < 365;
      } catch (e) {
        // Log error
      }
    }

    if (dateSaleDevice != null && dateSaleDevice.isNotEmpty) {
      try {
        DateTime date = DateTime.parse(dateSaleDevice);
        isDateSaleRecent = DateTime.now().difference(date).inDays < 365;
      } catch (e) {
        // Log error
      }
    }

    return isLastPaymentRecent || isDateSaleRecent;
  }

  @override
  void initState() {
    super.initState();
    // _initDatabase();
    _loadInitialData();
    _checkAndNavigate();
    _loadSettings();
  }

  Future<void> _loadSettings() async {
    SharedPreferences prefs = await SharedPreferences.getInstance();
    setState(() {
      customerType = prefs.getString('customerType') ?? 'العملاء المستمرين';
    });
  }

  // دالة التحقق والانتقال التلقائي
  Future<void> _checkAndNavigate() async {
    bool loggedIn = await AsyncIdChecker.isLoggedIn();

    if (loggedIn) {
      bool isValidSession = await AsyncIdChecker.checkAsyncId();
      if (!isValidSession) {
        await AsyncIdChecker.logout();
        if (mounted) {
          Navigator.pushReplacementNamed(context, '/Login');
        }
        return;
      }
    } else {
      if (mounted) {
        Navigator.pushReplacementNamed(context, '/Login');
      }
      return;
    }
  }

  Future<void> _loadInitialData() async {
    await _loadRepresentatives();
    await loadDateWeek();
  }

  // Load the data from the DateWeek table into the lastSevenDays list
  Future<void> loadDateWeek() async {
    final db = await DatabaseHelper().database;
    final List<Map<String, dynamic>> result = await db.query('DateWeek');

    if (result.isNotEmpty) {
      // Populate the lastSevenDays list with dates from the first row of DateWeek
      Map<String, dynamic> row = result.first;
      lastSevenDays = [
        row['Date1'],
        row['Date2'],
        row['Date3'],
        row['Date4'],
        row['Date5'],
        row['Date6'],
        row['Date7'],
      ];
    } else {
      debugPrint('No data found in DateWeek');
    }
  }

  Future<List<Client>> getCustomersWithoutPayments() async {
    final db = await DatabaseHelper().database;

    // Query to get customers who are not in the CustomerPayment table
    final List<Map<String, dynamic>> result = await db.rawQuery('''
    SELECT * FROM Customer 
    WHERE CustomerId NOT IN (SELECT CustomerId FROM CustomerPayment)
  ''');

    // Map the query results to a list of Client objects
    List<Client> customersWithoutPayments = result.map((client) {
      return Client(
        name: client['CustomerName'] ?? '', // تأكد من أن القيم النصية موجودة
        phone: client['PhoneNumber']?.toString() ??
            '', // تحويل القيم العددية إلى نصوص إذا كانت أرقامًا
        address: client['Address'] ?? '',
        shopName: client['ShopName'] ?? '',
        salePrice: (client['AmountTotalSales'] as num)
            .toDouble(), // تحويل القيمة إلى double
        receipt: (client['ReceiptsTotal'] as num)
            .toDouble(), // تحويل القيمة إلى double
        dailyInstallment: (client['AmountDaySales'] as num).toDouble(),
        remaining: (client['AmountRemaining'] as num).toDouble(),
        itemsNames: client['ItemsNames'] ?? '',
        amount1: (client['Amount1'] as num).toDouble(),
        amount2: (client['Amount2'] as num).toDouble(),
        amount3: (client['Amount3'] as num).toDouble(),
        amount4: (client['Amount4'] as num).toDouble(),
        amount5: (client['Amount5'] as num).toDouble(),
        amount6: (client['Amount6'] as num).toDouble(),
        amount7: (client['Amount7'] as num).toDouble(),
        phoneNumberCompany: client['PhoneNumberCompany']?.toString() ??
            '', // تحويل القيم العددية إلى نصوص
        countReceiptDevice: client['CountReceiptDevice']?.toString() ?? '',
        numberOfDayPayment: client['NumberOfDayPayment']?.toString() ?? '',
        isLegal: client['IsLegal'] ?? '',
        lastPaymentDate: client['LastPaymentDate'] ?? '',
        dateSaleDevice: client['DateSaleDevice'] ?? '',
        customerId: client['CustomerId'] as int,
      );
    }).toList();

    return customersWithoutPayments;
  }

  List<String> lastSevenDays = [];

  // تحميل المندوبين من جدول SelectDelegate
  Future<void> _loadRepresentatives() async {
    final db = await DatabaseHelper().database;
    final List<Map<String, dynamic>> reps = await db.query('SelectDelegate');
    setState(() {
      representatives = reps;
    });
  }

  // تحميل العملاء بناءً على المندوب المختار
  Future<void> _loadClients(int delegateId) async {
    final db = await DatabaseHelper().database;
    final List<Map<String, dynamic>> clientData = await db.query(
      'Customer',
      where: 'DelegateId = ?',
      whereArgs: [delegateId],
    );
    setState(() {
      clients = clientData
          .map((client) => Client(
                name: client['CustomerName'] ??
                    '', // تأكد من أن القيم النصية موجودة
                phone: client['PhoneNumber']?.toString() ??
                    '', // تحويل القيم العددية إلى نصوص إذا كانت أرقامًا
                address: client['Address'] ?? '',
                shopName: client['ShopName'] ?? '',
                salePrice: (client['AmountTotalSales'] as num)
                    .toDouble(), // تحويل القيمة إلى double
                receipt: (client['ReceiptsTotal'] as num)
                    .toDouble(), // تحويل القيمة إلى double
                dailyInstallment: (client['AmountDaySales'] as num).toDouble(),
                remaining: (client['AmountRemaining'] as num).toDouble(),
                itemsNames: client['ItemsNames'] ?? '',
                amount1: (client['Amount1'] as num).toDouble(),
                amount2: (client['Amount2'] as num).toDouble(),
                amount3: (client['Amount3'] as num).toDouble(),
                amount4: (client['Amount4'] as num).toDouble(),
                amount5: (client['Amount5'] as num).toDouble(),
                amount6: (client['Amount6'] as num).toDouble(),
                amount7: (client['Amount7'] as num).toDouble(),
                phoneNumberCompany: client['PhoneNumberCompany']?.toString() ??
                    '', // تحويل القيم العددية إلى نصوص
                countReceiptDevice:
                    client['CountReceiptDevice']?.toString() ?? '',
                numberOfDayPayment:
                    client['NumberOfDayPayment']?.toString() ?? '',
                isLegal: client['IsLegal'] ?? '',
                lastPaymentDate: client['LastPaymentDate'] ?? '',
                dateSaleDevice: client['DateSaleDevice'] ?? '',
                customerId: client['CustomerId'] as int,
              ))
          .toList();
    });

    // حساب مبلغ وعدد التسديدات
    await _calculatePayments(delegateId);
  }

  // حساب مبلغ وعدد التسديدات
  Future<void> _calculatePayments(int delegateId) async {
    final db = await DatabaseHelper().database;
    final List<Map<String, dynamic>> payments = await db.query(
      'CustomerPayment',
      where: 'DelegateId = ?',
      whereArgs: [delegateId],
    );

    double totalAmount = 0.0;
    for (var payment in payments) {
      totalAmount += payment['Amount'];
    }

    setState(() {
      totalPayments = totalAmount;
      paymentCount = payments.length;
    });
  }

  Future<String> _getCurrentLocation() async {
    try {
      // التحقق من تفعيل خدمة الموقع
      final serviceEnabled = await Geolocator.isLocationServiceEnabled();
      if (!serviceEnabled) {
        print('Location services are disabled.');
        return '1111,1111';
      }

      // التحقق من صلاحيات الموقع
      LocationPermission permission = await Geolocator.checkPermission();
      if (permission == LocationPermission.denied) {
        permission = await Geolocator.requestPermission();
        if (permission == LocationPermission.denied) {
          print('Location permission denied.');
          return '1111,1111';
        }
      }

      if (permission == LocationPermission.deniedForever) {
        print('Location permission permanently denied.');
        return '1111,1111';
      }

      // محاولة الحصول على آخر موقع معروف أولاً للسرعة
      Position? position = await Geolocator.getLastKnownPosition();

      if (position != null) {
        debugPrint(
            'Using last known location: ${position.latitude},${position.longitude}');
        return '${position.latitude},${position.longitude}';
      }

      // إذا لم يكن هناك موقع سابق، حاول الحصول على الموقع الحالي مع تقليل الدقة ووقت الانتظار
      position = await Geolocator.getCurrentPosition(
        desiredAccuracy: LocationAccuracy.medium,
        timeLimit: const Duration(seconds: 3),
      );

      debugPrint(
          'Current location fetched: ${position.latitude},${position.longitude}');
      return '${position.latitude},${position.longitude}';
    } catch (e, stackTrace) {
      print('Error getting location: $e');
      print('Stack trace: $stackTrace');
      return '1111,1111';
    }
  }

  Future<void> _deleteDuplicatePaymentsExceptLocation() async {
    final db = await DatabaseHelper().database;
    await db.rawQuery('''
    DELETE FROM CustomerPayment
    WHERE rowid NOT IN (
      SELECT MIN(rowid)
      FROM CustomerPayment
      GROUP BY
        CustomerId,
        CustomerName,
        DelegateId,
        DelegateName,
        Amount
    )
  ''');
  }

// دالة لإضافة التسديد
  Future<void> _addPayment(
      Client client, double amount, BuildContext context) async {
    // التحقق إذا كان المبلغ فارغًا أو يساوي صفرًا
    if (amount <= 0) {
      _showMessage('لا يمكن ترك المبلغ فارغًا أو يساوي صفر', context);
      return;
    }

    if (amount.toString() == "") {
      _showMessage('لا يمكن ترك المبلغ فارغًا أو يساوي صفر', context);
      return;
    }

    // التحقق مما إذا كان العميل قد سدد مسبقًا
    // التحقق مما إذا كان العميل قد سدد مسبقًا
    final db = await DatabaseHelper().database;
    final List<Map<String, dynamic>> existingPayment = await db.query(
      'CustomerPayment',
      where: 'CustomerId = ? AND DelegateId = ?',
      whereArgs: [client.customerId, selectedRepresentative],
    );

    if (!mounted) return;

    if (existingPayment.isNotEmpty) {
      // إذا وجدنا تسديدًا سابقًا لهذا العميل
      _showMessage('لا يمكن التسديد مرة أخرى لهذا العميل', context);
      return;
    }
    String? currentLocation = await _getCurrentLocation();
    // إضافة التسديد إلى جدول CustomerPayment
    await db.insert('CustomerPayment', {
      'CustomerId': client.customerId,
      'CustomerName': client.name,
      'DelegateId': selectedRepresentative,
      'DelegateName': representatives.firstWhere((rep) =>
          rep['DelegateId'].toString() ==
          selectedRepresentative)['DelegateName'],
      'Amount': amount,
      'Location': currentLocation
    });

    await _deleteDuplicatePaymentsExceptLocation();

    // تحديث التسديدات بعد الإضافة
    await _calculatePayments(int.parse(selectedRepresentative!));

    String customerName = client.name;
    String countReceiptDevice = client.countReceiptDevice;
    String receiptName = representatives.firstWhere(
      (rep) => rep['DelegateId'].toString() == selectedRepresentative,
      orElse: () => {'ReceiptName': 'Unknown'},
    )['ReceiptName'];
    String delegateName = representatives.firstWhere(
      (rep) => rep['DelegateId'].toString() == selectedRepresentative,
      orElse: () => {'DelegateName': 'Unknown'},
    )['DelegateName'];
    String itemsNames = client.itemsNames;
    double amountTotalSales = client.salePrice;
    double receiptsTotal = client.receipt;
    double amountRemaining = client.remaining;
    double amountPush = amount;
    String phoneNumberCompany = client.phoneNumberCompany;
    List<double> lastSevenAmounts = [
      client.amount1,
      client.amount2,
      client.amount3,
      client.amount4,
      client.amount5,
      client.amount6,
      client.amount7
    ];

    final report = ReportReceipt(
      customerName: customerName,
      receiptName: receiptName,
      delegateName: delegateName,
      itemsNames: itemsNames,
      amountTotalSales: amountTotalSales,
      receiptsTotal: receiptsTotal,
      amountRemaining: amountRemaining,
      amountPush: amountPush,
      phoneNumberCompany: phoneNumberCompany,
      lastSevenDays: lastSevenDays,
      lastSevenAmounts: lastSevenAmounts,
      countReceiptDevice: countReceiptDevice,
    );

    if (context.mounted) {
      await report.printReceipt(context);
    }
  }

// دالة لإظهار الرسائل
  void _showMessage(String message, BuildContext context) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text(message, style: const TextStyle(fontFamily: 'Cairo')),
        duration: const Duration(seconds: 3),
      ),
    );
  }

  // جلب بيانات التسديدات بناءً على المندوب المختار
  Future<void> _loadPayments(int delegateId) async {
    final db = await DatabaseHelper().database;
    final List<Map<String, dynamic>> paymentData = await db.query(
      'CustomerPayment',
      where: 'DelegateId = ?',
      whereArgs: [delegateId],
    );

    setState(() {
      payments = paymentData;
    });
  }

  void _showPaymentsDialog(BuildContext context) {
    TextEditingController searchController = TextEditingController();
    showGeneralDialog(
      context: context,
      barrierDismissible: true,
      barrierLabel: '',
      pageBuilder: (BuildContext context, Animation<double> animation,
          Animation<double> secondaryAnimation) {
        return Directionality(
          textDirection: TextDirection.rtl,
          child: Scaffold(
            backgroundColor: Theme.of(context).scaffoldBackgroundColor,
            appBar: AppBar(
              title: const Text('تسديدات اليوم',
                  style: TextStyle(fontFamily: 'Cairo', color: Colors.white)),
              backgroundColor: AppTheme.primaryColor,
              automaticallyImplyLeading: false,
              centerTitle: true,
              elevation: 0,
            ),
            body: StatefulBuilder(
              builder: (context, setState) {
                // StatefulBuilder للسماح بتحديث الواجهة
                return Padding(
                  padding: const EdgeInsets.all(10.0),
                  child: Column(
                    children: [
                      Container(
                        width: double.infinity,
                        margin: const EdgeInsets.symmetric(
                            horizontal: 10, vertical: 10),
                        decoration: BoxDecoration(
                          color: Theme.of(context).cardColor,
                          borderRadius: BorderRadius.circular(15),
                          border: Border.all(
                              color:
                                  AppTheme.primaryColor.withValues(alpha: 0.2)),
                          boxShadow: [
                            BoxShadow(
                              color: Colors.black.withValues(alpha: 0.05),
                              blurRadius: 10,
                              offset: const Offset(0, 4),
                            ),
                          ],
                        ),
                        child: TextField(
                          controller: searchController,
                          onChanged: (value) {
                            setState(() {
                              searchQueryReceipt = value;
                            });
                          },
                          decoration: InputDecoration(
                            hintText: 'البحث باسم العميل...',
                            hintStyle: TextStyle(
                              fontFamily: 'Cairo',
                              color: Colors.grey[400],
                              fontSize: 14,
                            ),
                            prefixIcon: const Icon(Icons.search,
                                color: AppTheme.primaryColor),
                            filled: true,
                            fillColor: Colors
                                .transparent, // Transparent to show Container color
                            border: OutlineInputBorder(
                              borderRadius: BorderRadius.circular(15),
                              borderSide: BorderSide.none,
                            ),
                            enabledBorder: OutlineInputBorder(
                              borderRadius: BorderRadius.circular(15),
                              borderSide: BorderSide.none,
                            ),
                            focusedBorder: OutlineInputBorder(
                              borderRadius: BorderRadius.circular(15),
                              borderSide: const BorderSide(
                                  color: AppTheme.primaryColor, width: 1.0),
                            ),
                            contentPadding:
                                const EdgeInsets.symmetric(vertical: 15),
                          ),
                          style:
                              const TextStyle(height: 1.5, fontFamily: 'Cairo'),
                        ),
                      ),
                      const SizedBox(height: 10),
                      Expanded(
                        child: SingleChildScrollView(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: payments
                                .where((payment) => payment['CustomerName']
                                    .toString()
                                    .contains(searchQueryReceipt))
                                .map((payment) => _buildPaymentRow(payment))
                                .toList(),
                          ),
                        ),
                      ),
                      const SizedBox(height: 10),
                      const SizedBox(height: 10),
                      Container(
                        padding: const EdgeInsets.all(15),
                        decoration: BoxDecoration(
                          color: Theme.of(context).cardColor,
                          borderRadius: const BorderRadius.only(
                            topLeft: Radius.circular(25),
                            topRight: Radius.circular(25),
                          ),
                          boxShadow: [
                            BoxShadow(
                                color: Colors.black.withValues(alpha: 0.05),
                                blurRadius: 10,
                                offset: const Offset(0, -5))
                          ],
                        ),
                        child: Column(
                          children: [
                            Row(
                              children: [
                                Expanded(
                                  child: ElevatedButton.icon(
                                    onPressed: () async {
                                      List<Map<String, dynamic>>
                                          filteredPayments = payments;

                                      final report = CustomerReceiptReport(
                                          delegateName:
                                              representatives.firstWhere(
                                            (rep) =>
                                                rep['DelegateId'].toString() ==
                                                selectedRepresentative,
                                            orElse: () =>
                                                {'DelegateName': 'Unknown'},
                                          )['DelegateName'],
                                          receiptName:
                                              representatives.firstWhere(
                                            (rep) =>
                                                rep['DelegateId'].toString() ==
                                                selectedRepresentative,
                                            orElse: () =>
                                                {'ReceiptName': 'Unknown'},
                                          )['ReceiptName'],
                                          numberOfReceipts:
                                              filteredPayments.length,
                                          amountReceiptTotal:
                                              filteredPayments.fold(
                                                  0.0,
                                                  (sum, item) =>
                                                      sum +
                                                      (item['Amount']
                                                              as num) // Log error
                                                          .toDouble()),
                                          receiptData: filteredPayments);
                                      if (context.mounted) {
                                        await report.printReceipt(context);
                                      }
                                    },
                                    icon: const Icon(Icons.print,
                                        color: Colors.white),
                                    label: const Text('المسددين',
                                        style: TextStyle(
                                            fontFamily: 'Cairo',
                                            color: Colors.white)),
                                    style: ElevatedButton.styleFrom(
                                        backgroundColor:
                                            AppTheme.secondaryColor,
                                        padding: const EdgeInsets.symmetric(
                                            vertical: 12),
                                        shape: RoundedRectangleBorder(
                                            borderRadius:
                                                BorderRadius.circular(10))),
                                  ),
                                ),
                                const SizedBox(width: 10),
                                Expanded(
                                  child: ElevatedButton.icon(
                                    onPressed: () async {
                                      final List<Client>
                                          customersWithoutPayments =
                                          await getCustomersWithoutPayments();

                                      final List<Client> filteredUnpaid =
                                          customersWithoutPayments
                                              .where((c) => isContinuous(
                                                  c.lastPaymentDate,
                                                  c.dateSaleDevice))
                                              .toList();

                                      final report = CustomerNoReceiptReport(
                                        delegateName:
                                            representatives.firstWhere(
                                          (rep) =>
                                              rep['DelegateId'].toString() ==
                                              selectedRepresentative,
                                          orElse: () =>
                                              {'DelegateName': 'Unknown'},
                                        )['DelegateName'],
                                        receiptName: representatives.firstWhere(
                                          (rep) =>
                                              rep['DelegateId'].toString() ==
                                              selectedRepresentative,
                                          orElse: () =>
                                              {'ReceiptName': 'Unknown'},
                                        )['ReceiptName'],
                                        customerData: filteredUnpaid,
                                      );

                                      if (context.mounted) {
                                        await report.printReport(context);
                                      }
                                    },
                                    icon: const Icon(Icons.print_disabled,
                                        color: Colors.white),
                                    label: const Text('الغير مسددين',
                                        style: TextStyle(
                                            fontFamily: 'Cairo',
                                            color: Colors.white)),
                                    style: ElevatedButton.styleFrom(
                                        backgroundColor: Colors.redAccent,
                                        padding: const EdgeInsets.symmetric(
                                            vertical: 12),
                                        shape: RoundedRectangleBorder(
                                            borderRadius:
                                                BorderRadius.circular(10))),
                                  ),
                                ),
                              ],
                            ),
                            const SizedBox(height: 10),
                            SizedBox(
                              width: double.infinity,
                              child: ElevatedButton.icon(
                                onPressed: isButtonDisabled
                                    ? null
                                    : () {
                                        _checkAndNavigate();
                                        _sendPayments(context);
                                      },
                                icon: isButtonDisabled
                                    ? const SizedBox(
                                        width: 20,
                                        height: 20,
                                        child: CircularProgressIndicator(
                                            color: Colors.white,
                                            strokeWidth: 2))
                                    : const Icon(Icons.send,
                                        color: Colors.white),
                                label: Text(
                                    isButtonDisabled
                                        ? 'جاري الارسال...'
                                        : 'إرسال التسديدات',
                                    style: const TextStyle(
                                        fontFamily: 'Cairo',
                                        color: Colors.white)),
                                style: ElevatedButton.styleFrom(
                                    backgroundColor: AppTheme.primaryColor,
                                    padding: const EdgeInsets.symmetric(
                                        vertical: 15),
                                    shape: RoundedRectangleBorder(
                                        borderRadius:
                                            BorderRadius.circular(12))),
                              ),
                            ),
                          ],
                        ),
                      ),
                      Container(
                        width: double.infinity,
                        color: Theme.of(context).cardColor,
                        padding: const EdgeInsets.only(bottom: 20, top: 10),
                        child: Center(
                            child: TextButton.icon(
                          onPressed: () => Navigator.pop(context),
                          icon: const Icon(Icons.close, color: Colors.grey),
                          label: const Text("إغلاق",
                              style: TextStyle(
                                  fontFamily: 'Cairo', color: Colors.grey)),
                        )),
                      )
                    ],
                  ),
                );
              },
            ),
          ),
        );
      },
      transitionBuilder: (context, animation1, animation2, child) {
        return FadeTransition(
          opacity: animation1,
          child: child,
        );
      },
    );
  }

  Widget _buildPaymentRow(Map<String, dynamic> payment) {
    return Container(
      margin: const EdgeInsets.only(bottom: 10),
      padding: const EdgeInsets.all(12),
      width: double.infinity,
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(15),
        color: Theme.of(context).cardColor,
        boxShadow: [
          BoxShadow(
              color: Colors.black.withValues(alpha: 0.05),
              blurRadius: 5,
              offset: const Offset(0, 2))
        ],
      ),
      child: Padding(
          padding: const EdgeInsets.symmetric(vertical: 5.0),
          child: Column(
            children: [
              Text('اسم الزبون: ${payment['CustomerName']}',
                  style: TextStyle(
                      fontFamily: 'Cairo',
                      fontSize: 16,
                      fontWeight: FontWeight.bold,
                      color: Theme.of(context).textTheme.bodyLarge?.color)),
              const SizedBox(height: 5),
              Divider(color: Colors.grey.withValues(alpha: 0.2)),
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  Text(
                    'مبلغ التسديد :',
                    style: TextStyle(
                        fontFamily: 'Cairo',
                        color: Theme.of(context).hintColor),
                  ),
                  Text(
                    '${Formatters.formatNumber(roundToNearestThousand(double.parse(payment['Amount'].toString())))} :دع ',
                    style: const TextStyle(
                        color: AppTheme.primaryColor,
                        fontFamily: 'Cairo',
                        fontWeight: FontWeight.bold,
                        fontSize: 16),
                  ),
                ],
              ),
              const SizedBox(height: 10),
              ElevatedButton.icon(
                onPressed: () async {
                  final rep = representatives.firstWhere(
                    (r) => r['DelegateId'].toString() == selectedRepresentative,
                    orElse: () => {},
                  );
                  String receiptName = rep['ReceiptName'] ?? '';
                  String delegateName = rep['DelegateName'] ?? '';
                  String phoneNumberCompany =
                      rep['PhoneNumberCompany']?.toString() ?? '';

                  // Find customer name by matching CustomerId
                  Client client = clients.firstWhere(
                    (rep) =>
                        rep.customerId.toString() ==
                        payment['CustomerId'].toString(),
                    orElse: () => Client(
                      customerId: 0,
                      name: 'Unknown',
                      phone: '',
                      address: '',
                      shopName: '',
                      salePrice: 0,
                      receipt: 0,
                      dailyInstallment: 0,
                      remaining: 0,
                      itemsNames: '',
                      amount1: 0,
                      amount2: 0,
                      amount3: 0,
                      amount4: 0,
                      amount5: 0,
                      amount6: 0,
                      amount7: 0,
                      phoneNumberCompany: '',
                      countReceiptDevice: '',
                      numberOfDayPayment: '',
                      isLegal: '',
                      lastPaymentDate: '',
                      dateSaleDevice: '',
                    ),
                  );

                  String itemsNames = client.itemsNames;
                  double amountTotalSales = client.salePrice;
                  double receiptsTotal = client.receipt;
                  double amountRemaining = client.remaining;

                  // Safe parsing for amountPush
                  double amountPush = 0.0;
                  try {
                    amountPush = double.parse(payment['Amount'].toString());
                  } catch (e) {
                    amountPush = 0.0;
                  }

                  List<String> lastSevenDays = [
                    "اليوم",
                    "أمس",
                    "قبل يومين",
                    "قبل 3 أيام",
                    "قبل 4 أيام",
                    "قبل 5 أيام",
                    "قبل 6 أيام"
                  ];
                  List<double> lastSevenAmounts = [
                    client.amount1,
                    client.amount2,
                    client.amount3,
                    client.amount4,
                    client.amount5,
                    client.amount6,
                    client.amount7
                  ];

                  final report = ReportReceipt(
                    customerName: client.name,
                    receiptName: receiptName,
                    delegateName: delegateName,
                    itemsNames: itemsNames,
                    amountTotalSales: amountTotalSales,
                    receiptsTotal: receiptsTotal,
                    amountRemaining: amountRemaining,
                    amountPush: amountPush,
                    phoneNumberCompany: phoneNumberCompany,
                    lastSevenDays: lastSevenDays,
                    lastSevenAmounts: lastSevenAmounts,
                    countReceiptDevice: client.countReceiptDevice,
                  );

                  await report.printReceipt(context);
                },
                icon: const Icon(Icons.print, color: Colors.white),
                label: const Text('طباعة الوصل',
                    style: TextStyle(fontFamily: 'Cairo', color: Colors.white)),
                style: ElevatedButton.styleFrom(
                    backgroundColor: AppTheme.secondaryColor,
                    shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(8))),
              ),
            ],
          )),
    );
  }

  Future<void> fetchPayments(int delegateId) async {
    final db = await DatabaseHelper().database;
    payments = await db.query(
      'CustomerPayment',
      where: 'DelegateId = ?',
      whereArgs: [delegateId],
    );
    setState(() {});
  }

  Future<void> sendPaymentsToAPI(int delegateId) async {
    SharedPreferences prefs = await SharedPreferences.getInstance();
    String linkDelegate = prefs.getString('LinkDelegate') ?? '0';
    bool hasError = false;

    // تجهيز قائمة الدفع كلها دفعة واحدة
    List<Map<String, dynamic>> paymentsData = payments.map((payment) {
      return {
        "CustomerId": payment['CustomerId'],
        "DelegateId": payment['DelegateId'],
        "Amount": payment['Amount'],
        "Location": payment['Location'],
      };
    }).toList();

    try {
      final response = await http.post(
        Uri.parse(
            '${linkDelegate}CustomersPaymentsRequests/PostSelectPaymentCustomerTemporaryMulti'),
        headers: {"Content-Type": "application/json"},
        body: jsonEncode(paymentsData),
      );

      if (response.statusCode == 200) {
        print("All payments sent successfully.");
      } else {
        print("Failed to send payments. Status code: ${response.statusCode}");
        hasError = true;
      }
    } catch (error) {
      print('Error sending payments: $error');
      hasError = true;
    }

    if (hasError) {
      await deletePayments(delegateId);
      print('Error occurred, but payments deleted anyway.');
    } else {
      await deletePayments(delegateId);
      print('All payments were successfully sent and deleted.');
    }

    _loadClients(int.parse(selectedRepresentative!));
  }

  // دالة لمسح التسديدات الخاصة بالمندوب المختار
  Future<void> deletePayments(int delegateId) async {
    final db = await DatabaseHelper().database;
    await db.delete(
      'CustomerPayment',
      where: 'DelegateId = ?',
      whereArgs: [delegateId],
    );
    print('Deleted all payments for DelegateId: $delegateId');
  }

  Future<void> _sendPayments(BuildContext context) async {
    await fetchPayments(
        int.parse(selectedRepresentative!)); // جلب التسديدات أولاً

    if (!mounted) return;

    if (payments.isNotEmpty) {
      setState(() {
        isButtonDisabled = true; // تعطيل الزر عند البدء
      });
      var connectivityResult = await (Connectivity().checkConnectivity());
      if (!connectivityResult.contains(ConnectivityResult.none)) {
        if (!mounted) return;
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('انتظر لحين ارسال التسديدات')),
        );
        await Future.delayed(const Duration(seconds: 5));
        if (!mounted) return;
        await sendPaymentsToAPI(
            int.parse(selectedRepresentative!)); // إرسال التسديدات إلى الـ API
        if (!mounted) return;
        setState(() {
          isButtonDisabled = false;
        });
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('تم إرسال جميع التسديدات بنجاح')),
        );
        Navigator.pop(context);
      } else {
        if (!mounted) return;
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
              content: Text('لا يمكن إرسال التسديدات بدون اتصال بالإنترنت')),
        );
      }
    } else {
      if (!mounted) return;
      print('No payments to send');
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('لا توجد تسديدات للإرسال')),
      );
    }
  }

  double roundToNearestThousand(double value) {
    return (value / 1000).round() * 1000;
  }

  // إظهار الـDialog
  void _showPaymentDialog(Client client, BuildContext context) {
    TextEditingController amountController =
        TextEditingController(text: client.dailyInstallment.toInt().toString());
    // isDarkMode removed as it was unused

    showDialog(
      context: context,
      builder: (context) {
        return Directionality(
          textDirection: TextDirection.rtl,
          child: Dialog(
            shape:
                RoundedRectangleBorder(borderRadius: BorderRadius.circular(20)),
            backgroundColor: Theme.of(context).dialogTheme.backgroundColor ??
                Theme.of(context).cardColor,
            child: Padding(
              padding: const EdgeInsets.all(20.0),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Text(
                    'إضافة تسديد',
                    textAlign: TextAlign.center,
                    style: TextStyle(
                      fontFamily: 'Cairo',
                      fontSize: 20,
                      fontWeight: FontWeight.bold,
                      color: Theme.of(context).textTheme.bodyLarge?.color,
                    ),
                  ),
                  const SizedBox(height: 20),
                  Text(
                    'أدخل مبلغ التسديد:',
                    style: TextStyle(
                      fontFamily: 'Cairo',
                      fontSize: 14,
                      color: Theme.of(context).hintColor,
                    ),
                  ),
                  const SizedBox(height: 10),
                  TextField(
                    controller: amountController,
                    keyboardType: TextInputType.number,
                    style: const TextStyle(fontFamily: 'Cairo'),
                    decoration: InputDecoration(
                      filled: true,
                      fillColor: Theme.of(context).cardColor,
                      hintText: 'المبلغ',
                      border: OutlineInputBorder(
                        borderRadius: BorderRadius.circular(12),
                        borderSide: BorderSide.none,
                      ),
                      prefixIcon: const Icon(Icons.attach_money,
                          color: AppTheme.primaryColor),
                    ),
                  ),
                  const SizedBox(height: 25),
                  Row(
                    children: [
                      Expanded(
                        child: ElevatedButton.icon(
                          onPressed: () async {
                            double amount =
                                double.tryParse(amountController.text) ?? 0.0;
                            if (amount <= 0) {
                              _showMessage('لا يمكن ترك المبلغ فارغًا أو يساوي صفر', context);
                              return;
                            }
                            await _addPayment(client, amount, context);
                            if (mounted) Navigator.pop(context);
                          },
                          icon: const Icon(Icons.check_circle_outline,
                              color: Colors.white),
                          label: const Text('تسديد',
                              style: TextStyle(
                                  fontFamily: 'Cairo', color: Colors.white)),
                          style: ElevatedButton.styleFrom(
                            backgroundColor: AppTheme.primaryColor,
                            padding: const EdgeInsets.symmetric(vertical: 12),
                            shape: RoundedRectangleBorder(
                                borderRadius: BorderRadius.circular(10)),
                          ),
                        ),
                      ),
                      const SizedBox(width: 10),
                      Expanded(
                        child: OutlinedButton.icon(
                          onPressed: () {
                            Navigator.pop(context);
                          },
                          icon: Icon(Icons.cancel_outlined,
                              color: Theme.of(context).colorScheme.error),
                          label: Text('إلغاء',
                              style: TextStyle(
                                  fontFamily: 'Cairo',
                                  color: Theme.of(context).colorScheme.error)),
                          style: OutlinedButton.styleFrom(
                            padding: const EdgeInsets.symmetric(vertical: 12),
                            side: BorderSide(
                                color: Theme.of(context).colorScheme.error),
                            shape: RoundedRectangleBorder(
                                borderRadius: BorderRadius.circular(10)),
                          ),
                        ),
                      ),
                    ],
                  ),
                ],
              ),
            ),
          ),
        );
      },
    );
  }

  @override
  Widget build(BuildContext context) {
    return Directionality(
      textDirection: TextDirection.rtl,
      child: Scaffold(
        backgroundColor: AppTheme.backgroundColor,
        appBar: AppBar(
          backgroundColor: AppTheme.primaryColor,
          elevation: 0,
          title: const Text('العملاء',
              style: TextStyle(fontFamily: 'Cairo', color: Colors.white)),
          centerTitle: true,
          iconTheme: const IconThemeData(color: Colors.white),
        ),
        body: Column(
          children: [
            Container(
              padding: const EdgeInsets.all(20),
              decoration: const BoxDecoration(
                color: AppTheme.primaryColor,
                borderRadius: BorderRadius.only(
                  bottomLeft: Radius.circular(30),
                  bottomRight: Radius.circular(30),
                ),
              ),
              child: Column(
                children: [
                  DropdownButtonFormField<String>(
                    decoration: InputDecoration(
                      filled: true,
                      fillColor: Theme.of(context).cardColor,
                      hintText: 'اختر القائمة',
                      hintStyle:
                          const TextStyle(fontFamily: 'Cairo', fontSize: 14),
                      prefixIcon:
                          const Icon(Icons.list, color: AppTheme.primaryColor),
                      contentPadding: const EdgeInsets.symmetric(
                          horizontal: 10, vertical: 0),
                      border: OutlineInputBorder(
                          borderRadius: BorderRadius.circular(10),
                          borderSide: BorderSide.none),
                    ),
                    alignment: Alignment.centerRight,
                    value: selectedRepresentative,
                    items: representatives.map((rep) {
                      return DropdownMenuItem<String>(
                        value: rep['DelegateId'].toString(),
                        child: Align(
                          alignment: Alignment.centerRight,
                          child: Text(
                            rep['DelegateName'],
                            textAlign: TextAlign.right,
                            style: const TextStyle(fontFamily: 'Cairo'),
                          ),
                        ),
                      );
                    }).toList(),
                    onChanged: (value) {
                      setState(() {
                        selectedRepresentative = value;
                        _loadClients(int.parse(value!));
                        _loadPayments(int.parse(value));
                      });
                    },
                  ),
                  const SizedBox(height: 10),
                  DropdownButtonFormField<String>(
                    decoration: InputDecoration(
                      filled: true,
                      fillColor: Theme.of(context).cardColor,
                      hintText: 'نوع العميل',
                      hintStyle:
                          const TextStyle(fontFamily: 'Cairo', fontSize: 14),
                      prefixIcon: const Icon(Icons.category,
                          color: AppTheme.primaryColor),
                      contentPadding: const EdgeInsets.symmetric(
                          horizontal: 10, vertical: 0),
                      border: OutlineInputBorder(
                          borderRadius: BorderRadius.circular(10),
                          borderSide: BorderSide.none),
                    ),
                    alignment: Alignment.centerRight,
                    value: customerType,
                    items: [
                      'العملاء المستمرين',
                      'العملاء المتوقفين',
                      'عملاء القانونية'
                    ]
                        .map((type) => DropdownMenuItem(
                              value: type,
                              child: Align(
                                alignment: Alignment.centerRight,
                                child: Text(type,
                                    textAlign: TextAlign.right,
                                    style:
                                        const TextStyle(fontFamily: 'Cairo')),
                              ),
                            ))
                        .toList(),
                    onChanged: (value) async {
                      setState(() {
                        customerType = value!;
                      });
                      SharedPreferences prefs =
                          await SharedPreferences.getInstance();
                      await prefs.setString('customerType', value!);
                    },
                  ),
                  const SizedBox(height: 10),
                  TextField(
                    onChanged: (value) {
                      setState(() {
                        searchQuery = value;
                      });
                    },
                    decoration: InputDecoration(
                      filled: true,
                      fillColor: Theme.of(context).cardColor,
                      hintText: 'بحث...',
                      hintStyle:
                          const TextStyle(fontFamily: 'Cairo', fontSize: 14),
                      prefixIcon: const Icon(Icons.search,
                          color: AppTheme.primaryColor),
                      contentPadding: const EdgeInsets.symmetric(
                          horizontal: 10, vertical: 0),
                      border: OutlineInputBorder(
                          borderRadius: BorderRadius.circular(10),
                          borderSide: BorderSide.none),
                    ),
                  ),
                ],
              ),
            ),
            Expanded(
              child: ListView(
                padding:
                    const EdgeInsets.symmetric(horizontal: 15, vertical: 10),
                children: clients
                    .where((client) {
                      bool matchesSearch = client.name.contains(searchQuery) ||
                          searchQuery.isEmpty;

                      bool isLegal =
                          client.isLegal == "1" || client.isLegal == "true";

                      bool matchesType = false;
                      if (customerType == 'عملاء القانونية') {
                        matchesType = isLegal;
                      } else if (customerType == 'العملاء المستمرين') {
                        matchesType = isContinuous(client.lastPaymentDate,
                                client.dateSaleDevice) &&
                            !isLegal;
                      } else {
                        // العملاء المتوقفين
                        matchesType = !isContinuous(client.lastPaymentDate,
                                client.dateSaleDevice) &&
                            !isLegal;
                      }
                      return matchesSearch && matchesType;
                    })
                    .map((client) => ClientCard(
                          client: client,
                          delegateId: int.parse(selectedRepresentative!),
                          onPayment: () => _showPaymentDialog(client, context),
                        ))
                    .toList(),
              ),
            ),
            Container(
              padding: const EdgeInsets.all(20),
              decoration: BoxDecoration(
                color: Theme.of(context).cardColor,
                borderRadius: const BorderRadius.only(
                  topLeft: Radius.circular(30),
                  topRight: Radius.circular(30),
                ),
                boxShadow: [
                  BoxShadow(
                      color: Colors.black.withValues(alpha: 0.05),
                      blurRadius: 10,
                      offset: const Offset(0, -5)),
                ],
              ),
              child: Column(
                children: [
                  Row(
                    children: [
                      Expanded(
                        child: ElevatedButton.icon(
                          onPressed: () async {
                            if (selectedRepresentative != null) {
                              await _deleteDuplicatePaymentsExceptLocation();
                              if (!mounted) return;
                              searchQueryReceipt = '';
                              _loadPayments(int.parse(selectedRepresentative!));
                              _showPaymentsDialog(context);
                            }
                          },
                          icon: const Icon(Icons.receipt_long),
                          label: const Text('التسديدات',
                              style: TextStyle(
                                  fontFamily: 'Cairo', color: Colors.white)),
                          style: ElevatedButton.styleFrom(
                            backgroundColor: AppTheme.secondaryColor,
                            shape: RoundedRectangleBorder(
                                borderRadius: BorderRadius.circular(10)),
                          ),
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 15),
                  Container(
                    padding: const EdgeInsets.all(15),
                    decoration: BoxDecoration(
                      color: AppTheme.primaryColor.withValues(alpha: 0.1),
                      borderRadius: BorderRadius.circular(15),
                      border: Border.all(
                          color: AppTheme.primaryColor.withValues(alpha: 0.2)),
                    ),
                    child: Column(
                      children: [
                        Row(
                          mainAxisAlignment: MainAxisAlignment.spaceBetween,
                          children: [
                            const Text("عدد التسديدات",
                                style: TextStyle(
                                    fontFamily: 'Cairo',
                                    color: AppTheme.textColor)),
                            Text("$paymentCount",
                                style: const TextStyle(
                                    fontFamily: 'Cairo',
                                    fontWeight: FontWeight.bold,
                                    color: AppTheme.primaryColor)),
                          ],
                        ),
                        const Divider(),
                        Row(
                          mainAxisAlignment: MainAxisAlignment.spaceBetween,
                          children: [
                            const Text("مبلغ التسديد الكلي",
                                style: TextStyle(
                                    fontFamily: 'Cairo',
                                    color: AppTheme.textColor)),
                            Row(
                              children: [
                                Text(Formatters.formatNumber(totalPayments),
                                    style: const TextStyle(
                                        fontFamily: 'Cairo',
                                        fontWeight: FontWeight.bold,
                                        color: AppTheme.primaryColor)),
                                const SizedBox(width: 4),
                                const Text("دع",
                                    style: TextStyle(
                                        fontFamily: 'Cairo',
                                        fontWeight: FontWeight.bold,
                                        color: AppTheme.primaryColor)),
                              ],
                            ),
                          ],
                        ),
                      ],
                    ),
                  )
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class ClientCard extends StatelessWidget {
  final Client client;
  final VoidCallback onPayment;
  final int delegateId;

  const ClientCard(
      {super.key,
      required this.client,
      required this.onPayment,
      required this.delegateId});

  // ... (keeping other methods like _fetchCustomerData same if possible, but I can't skip lines easily with replace_file_content unless I target specific blocks)
  // I will target the class start and constructor first.

  // جلب بيانات العميل من API
  Future<Map<String, dynamic>> _fetchCustomerData(int customerId) async {
    SharedPreferences prefs = await SharedPreferences.getInstance();
    String linkDelegate = prefs.getString('LinkDelegate') ?? '0';
    final response = await http.get(
        Uri.parse('${linkDelegate}Customers/GetCustomersDataInfo/$customerId'));
    if (response.statusCode == 200) {
      return json.decode(response.body);
    } else {
      throw Exception('Failed to load customer data');
    }
  }

  // عرض Dialog بمعلومات العميل
  void _showCustomerDialog(int customerId, BuildContext context) async {
    try {
      // جلب بيانات العميل من API
      Map<String, dynamic> customerData = await _fetchCustomerData(customerId);
      if (!context.mounted) return;

      showDialog(
        context: context,
        builder: (BuildContext context) {
          return Directionality(
            textDirection: TextDirection.rtl,
            child: Dialog(
              shape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(20)),
              insetPadding: const EdgeInsets.all(15),
              backgroundColor: Theme.of(context).dialogTheme.backgroundColor ??
                  Theme.of(context).cardColor,
              child: Container(
                width: double.maxFinite,
                padding: const EdgeInsets.all(20),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text('معلومات العميل',
                        style: TextStyle(
                            fontFamily: 'Cairo',
                            fontSize: 20,
                            fontWeight: FontWeight.bold,
                            color:
                                Theme.of(context).textTheme.bodyLarge?.color)),
                    const SizedBox(height: 15),
                    const Divider(),
                    Expanded(
                      child: SingleChildScrollView(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.center,
                          children: [
                            // Info Rows with improved styling
                            _buildInfoRow(
                              Icons.person,
                              'الاسم:',
                              customerData['customerName'],
                              context,
                            ),
                            _buildInfoRow(Icons.home, 'العنوان:',
                                customerData['address'], context),
                            InkWell(
                              onTap: () async {
                                final Uri telUri = Uri(
                                    scheme: 'tel',
                                    path: customerData['phoneNumber']);
                                await launchUrl(telUri,
                                    mode: LaunchMode.platformDefault);
                              },
                              child: _buildInfoRow(
                                Icons.phone,
                                'رقم الهاتف:',
                                customerData['phoneNumber'],
                                context,
                                isLink: true,
                              ),
                            ),
                            _buildInfoRow(Icons.store, 'اسم المحل:',
                                customerData['shopName'], context),
                            _buildInfoRow(
                                Icons.date_range,
                                'تاريخ البيع:',
                                Formatters.formatDate(
                                    customerData['dateSaleDevice']),
                                context),
                            _buildInfoRow(
                                Icons.attach_money,
                                'سعر البيع:',
                                Formatters.formatNumber(double.parse(
                                    customerData['amountTotalSales']
                                        .toString())),
                                context,
                                isPrice: true),
                            _buildInfoRow(
                                Icons.monetization_on,
                                'سعر الشراء:',
                                Formatters.formatNumber(double.parse(
                                    customerData['costTotalSales'].toString())),
                                context,
                                isPrice: true),
                            _buildInfoRow(
                                Icons.credit_card,
                                'القسط اليومي:',
                                Formatters.formatNumber(double.parse(
                                    customerData['amountDaySales'].toString())),
                                context,
                                isPrice: true),
                            _buildInfoRow(
                                Icons.receipt,
                                'المبلغ الواصل:',
                                Formatters.formatNumber(double.parse(
                                    customerData['receiptsTotal'].toString())),
                                context,
                                isPrice: true),
                            _buildInfoRow(
                                Icons.attach_money,
                                'المبلغ المتبقي:',
                                Formatters.formatNumber(double.parse(
                                    customerData['amountRemaining']
                                        .toString())),
                                context,
                                isPrice: true),
                            _buildInfoRow(
                                Icons.shopping_bag,
                                'العناصر المباعة:',
                                customerData['itemsNames'],
                                context),
                            _buildInfoRow(
                                Icons.history,
                                'تاريخ اخر تسديد:',
                                (client.lastPaymentDate.isNotEmpty)
                                    ? Formatters.formatDate(
                                        client.lastPaymentDate)
                                    : 'لا يوجد',
                                context),

                            const SizedBox(height: 20),
                            Row(
                              children: [
                                Expanded(
                                  child: ElevatedButton.icon(
                                    onPressed: () {
                                      _showFullScreenSaleDialog(
                                          context, customerId);
                                    },
                                    icon: const Icon(Icons.list_alt,
                                        color: Colors.white),
                                    label: const Text('عرض المبيعات',
                                        style: TextStyle(
                                            fontFamily: 'Cairo',
                                            color: Colors.white)),
                                    style: ElevatedButton.styleFrom(
                                      backgroundColor: AppTheme.secondaryColor,
                                      shape: RoundedRectangleBorder(
                                          borderRadius:
                                              BorderRadius.circular(10)),
                                      padding: const EdgeInsets.symmetric(
                                          vertical: 12),
                                    ),
                                  ),
                                ),
                                const SizedBox(width: 10),
                                Expanded(
                                  child: ElevatedButton.icon(
                                    onPressed: () {
                                      _showFullScreenReceiptDialog(
                                          context, customerId);
                                    },
                                    icon: const Icon(Icons.receipt_long,
                                        color: Colors.white),
                                    label: const Text('عرض التسديدات',
                                        style: TextStyle(
                                            fontFamily: 'Cairo',
                                            color: Colors.white)),
                                    style: ElevatedButton.styleFrom(
                                      backgroundColor: AppTheme.primaryColor,
                                      shape: RoundedRectangleBorder(
                                          borderRadius:
                                              BorderRadius.circular(10)),
                                      padding: const EdgeInsets.symmetric(
                                          vertical: 12),
                                    ),
                                  ),
                                ),
                              ],
                            ),
                            const SizedBox(height: 10),
                            SizedBox(
                              width: double.infinity,
                              child: OutlinedButton.icon(
                                onPressed: () {
                                  Navigator.of(context).pop();
                                },
                                icon: Icon(Icons.close,
                                    color: Theme.of(context)
                                        .textTheme
                                        .bodyLarge
                                        ?.color),
                                label: Text('إغلاق',
                                    style: TextStyle(
                                        fontFamily: 'Cairo',
                                        color: Theme.of(context)
                                            .textTheme
                                            .bodyLarge
                                            ?.color)),
                                style: OutlinedButton.styleFrom(
                                  side: BorderSide(
                                      color: Theme.of(context)
                                              .textTheme
                                              .bodyLarge
                                              ?.color ??
                                          Colors.grey),
                                  shape: RoundedRectangleBorder(
                                      borderRadius: BorderRadius.circular(10)),
                                  padding:
                                      const EdgeInsets.symmetric(vertical: 12),
                                ),
                              ),
                            )
                          ],
                        ),
                      ),
                    ),
                  ],
                ),
              ),
            ),
          );
        },
      );
    } catch (e) {
      // عرض خطأ إذا فشل جلب البيانات
      showDialog(
        context: context,
        builder: (BuildContext context) {
          return Directionality(
            textDirection: TextDirection.rtl,
            child: AlertDialog(
              title: const Text('خطأ', style: TextStyle(fontFamily: 'Cairo')),
              content: const Text('فشل في جلب بيانات العميل',
                  style: TextStyle(fontFamily: 'Cairo')),
              actions: [
                TextButton(
                  child: const Text('إغلاق',
                      style: TextStyle(fontFamily: 'Cairo')),
                  onPressed: () {
                    Navigator.of(context).pop();
                  },
                ),
              ],
            ),
          );
        },
      );
    }
  }

  void _showFullScreenSaleDialog(BuildContext context, int customerId) {
    showGeneralDialog(
      context: context,
      barrierDismissible: true,
      barrierLabel: '',
      pageBuilder: (BuildContext context, Animation<double> animation,
          Animation<double> secondaryAnimation) {
        return Directionality(
          textDirection: TextDirection.rtl,
          child: Scaffold(
            body: AllSaleCustomer(
                customerId: customerId), // هنا يتم استدعاء الكلاس AllSale
          ),
        );
      },
      transitionBuilder: (context, animation1, animation2, child) {
        return FadeTransition(
          opacity: animation1,
          child: child,
        );
      },
    );
  }

  void _showFullScreenReceiptDialog(BuildContext context, int customerId) {
    showGeneralDialog(
      context: context,
      barrierDismissible: true,
      barrierLabel: '',
      pageBuilder: (BuildContext context, Animation<double> animation,
          Animation<double> secondaryAnimation) {
        return Directionality(
          textDirection: TextDirection.rtl,
          child: Scaffold(
            body: AllReceiptCustomer(
                customerId: customerId), // هنا يتم استدعاء الكلاس AllSale
          ),
        );
      },
      transitionBuilder: (context, animation1, animation2, child) {
        return FadeTransition(
          opacity: animation1,
          child: child,
        );
      },
    );
  }

  Widget _buildInfoRow(
      IconData icon, String label, String value, BuildContext context,
      {bool isLink = false, bool isPrice = false}) {
    return Container(
      margin: const EdgeInsets.only(bottom: 8),
      padding: const EdgeInsets.all(10),
      decoration: BoxDecoration(
        color: isLink
            ? AppTheme.primaryColor.withValues(alpha: 0.1)
            : Theme.of(context).cardColor,
        borderRadius: BorderRadius.circular(10),
        border: Border.all(
          color: isLink
              ? AppTheme.primaryColor.withValues(alpha: 0.3)
              : Colors.grey.withValues(alpha: 0.1),
        ),
      ),
      child: Row(
        children: [
          Container(
            padding: const EdgeInsets.all(8),
            decoration: BoxDecoration(
              color: AppTheme.primaryColor.withValues(alpha: 0.1),
              shape: BoxShape.circle,
            ),
            child: Icon(icon, color: AppTheme.primaryColor, size: 20),
          ),
          const SizedBox(width: 10),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  label,
                  style: TextStyle(
                    fontSize: 12,
                    color: Theme.of(context).hintColor,
                    fontFamily: 'Cairo',
                  ),
                ),
                if (isPrice)
                  Row(
                    children: [
                      Text(
                        value,
                        style: TextStyle(
                          fontSize: 14,
                          fontWeight: FontWeight.bold,
                          fontFamily: 'Cairo',
                          color: isLink
                              ? AppTheme.primaryColor
                              : Theme.of(context).textTheme.bodyLarge?.color,
                        ),
                      ),
                      const SizedBox(width: 4),
                      Text(
                        "دع",
                        style: TextStyle(
                          fontSize: 14,
                          fontWeight: FontWeight.bold,
                          fontFamily: 'Cairo',
                          color: Theme.of(context).textTheme.bodyLarge?.color,
                        ),
                      ),
                    ],
                  )
                else
                  Text(
                    value,
                    style: TextStyle(
                      fontSize: 14,
                      fontWeight: FontWeight.bold,
                      fontFamily: 'Cairo',
                      color: isLink
                          ? AppTheme.primaryColor
                          : Theme.of(context).textTheme.bodyLarge?.color,
                    ),
                  ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Container(
      margin: const EdgeInsets.only(bottom: 12),
      decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(20),
          boxShadow: [
            BoxShadow(
                color: Colors.black.withValues(alpha: 0.05),
                blurRadius: 10,
                offset: const Offset(0, 5))
          ]),
      child: Padding(
        padding: const EdgeInsets.all(20.0),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(client.name,
                          style: const TextStyle(
                              fontWeight: FontWeight.bold,
                              fontSize: 16,
                              fontFamily: 'Cairo')),
                      if (client.shopName.isNotEmpty)
                        Text(client.shopName,
                            style: TextStyle(
                                color: Colors.grey[600],
                                fontSize: 13,
                                fontFamily: 'Cairo')),
                      if (client.phone.isNotEmpty)
                        Text(client.phone,
                            style: const TextStyle(
                                color: AppTheme.primaryColor,
                                fontWeight: FontWeight.bold,
                                fontSize: 13,
                                fontFamily: 'Cairo')),
                    ],
                  ),
                ),
                InkWell(
                  onTap: () async {
                    final Uri telUri = Uri(scheme: 'tel', path: client.phone);
                    await launchUrl(telUri, mode: LaunchMode.platformDefault);
                  },
                  child: Container(
                    padding: const EdgeInsets.all(8),
                    decoration: BoxDecoration(
                        color: AppTheme.primaryColor.withValues(alpha: 0.1),
                        shape: BoxShape.circle),
                    child:
                        const Icon(Icons.phone, color: AppTheme.primaryColor),
                  ),
                )
              ],
            ),
            const Divider(height: 25),
            _buildDetailRow("العنوان", client.address),
            _buildDetailRow(
                "سعر البيع", Formatters.formatNumber(client.salePrice),
                isPrice: true),
            _buildDetailRow("الباقي", Formatters.formatNumber(client.remaining),
                isBold: true, color: Colors.red, isPrice: true),
            _buildDetailRow("القسط اليومي",
                Formatters.formatNumber(client.dailyInstallment),
                isBold: true, color: AppTheme.primaryColor, isPrice: true),
            _buildDetailRow(
                "تاريخ اخر تسديد",
                (client.lastPaymentDate.isNotEmpty)
                    ? Formatters.formatDate(client.lastPaymentDate)
                    : 'لا يوجد'),
            const SizedBox(height: 15),
            Row(
              children: [
                if (client.isLegal == "false" ||
                    (client.isLegal != "1" && client.isLegal != "true"))
                  Expanded(
                    child: ElevatedButton.icon(
                      onPressed: onPayment,
                      icon: const Icon(Icons.payment, color: Colors.white),
                      label: const Text('تسديد',
                          style: TextStyle(
                              fontFamily: 'Cairo',
                              fontWeight: FontWeight.bold,
                              color: Colors.white)),
                      style: ElevatedButton.styleFrom(
                        backgroundColor: AppTheme.primaryColor,
                        padding: const EdgeInsets.symmetric(vertical: 12),
                        elevation: 2,
                        shape: RoundedRectangleBorder(
                            borderRadius: BorderRadius.circular(12)),
                      ),
                    ),
                  ),
                if (client.isLegal == "false" ||
                    (client.isLegal != "1" && client.isLegal != "true"))
                  const SizedBox(width: 10),
                Expanded(
                  child: OutlinedButton.icon(
                    onPressed: () =>
                        _showCustomerDialog(client.customerId, context),
                    icon: const Icon(Icons.folder_copy_outlined,
                        color: AppTheme.primaryColor),
                    label: const Text('الملف',
                        style: TextStyle(
                            fontFamily: 'Cairo',
                            fontWeight: FontWeight.bold,
                            color: AppTheme.primaryColor)),
                    style: OutlinedButton.styleFrom(
                        padding: const EdgeInsets.symmetric(vertical: 12),
                        shape: RoundedRectangleBorder(
                            borderRadius: BorderRadius.circular(12)),
                        side: const BorderSide(
                            color: AppTheme.primaryColor, width: 1.5)),
                  ),
                )
              ],
            )
          ],
        ),
      ),
    );
  }

  Widget _buildDetailRow(String label, String value,
      {bool isBold = false, Color? color, bool isPrice = false}) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 8),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Text(label,
              style: const TextStyle(
                  color: Colors.grey, fontFamily: 'Cairo', fontSize: 13)),
          if (isPrice)
            Row(
              children: [
                Text(value,
                    style: TextStyle(
                        fontFamily: 'Cairo',
                        fontWeight:
                            isBold ? FontWeight.bold : FontWeight.normal,
                        color: color ?? AppTheme.textColor,
                        fontSize: 14)),
                const SizedBox(width: 4),
                Text("دع",
                    style: TextStyle(
                        fontFamily: 'Cairo',
                        fontWeight:
                            isBold ? FontWeight.bold : FontWeight.normal,
                        color: color ?? AppTheme.textColor,
                        fontSize: 14)),
              ],
            )
          else
            Text(value,
                style: TextStyle(
                    fontFamily: 'Cairo',
                    fontWeight: isBold ? FontWeight.bold : FontWeight.normal,
                    color: color ?? AppTheme.textColor,
                    fontSize: 14)),
        ],
      ),
    );
  }
}

class Client {
  final String name;
  final String phone;
  final String address;
  final String shopName;
  final double salePrice;
  final double receipt;
  final double dailyInstallment;
  final double remaining;
  final String itemsNames;
  final double amount1;
  final double amount2;
  final double amount3;
  final double amount4;
  final double amount5;
  final double amount6;
  final double amount7;
  final String phoneNumberCompany;
  final String countReceiptDevice;
  final String numberOfDayPayment;
  final String isLegal;
  final String lastPaymentDate;
  final String dateSaleDevice;
  final int customerId;

  Client({
    required this.name,
    required this.phone,
    required this.address,
    required this.shopName,
    required this.salePrice,
    required this.receipt,
    required this.dailyInstallment,
    required this.remaining,
    required this.itemsNames,
    required this.amount1,
    required this.amount2,
    required this.amount3,
    required this.amount4,
    required this.amount5,
    required this.amount6,
    required this.amount7,
    required this.phoneNumberCompany,
    required this.countReceiptDevice,
    required this.numberOfDayPayment,
    required this.isLegal,
    required this.lastPaymentDate,
    required this.dateSaleDevice,
    required this.customerId,
  });
}

class PaymentsList extends StatefulWidget {
  final int delegateId;
  final Client client;

  const PaymentsList(
      {super.key, required this.delegateId, required this.client});

  @override
  State<PaymentsList> createState() => _PaymentsListState();
}

class _PaymentsListState extends State<PaymentsList> {
  List<Map<String, dynamic>> payments = [];

  @override
  void initState() {
    super.initState();
    _loadPayments();
  }

  Future<void> _loadPayments() async {
    final db = await DatabaseHelper().database;
    final List<Map<String, dynamic>> paymentData = await db.query(
      'Payments',
      where: 'DelegateId = ?',
      whereArgs: [widget.delegateId],
    );

    if (mounted) {
      setState(() {
        payments = paymentData;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final clientPayments = payments
        .where((p) =>
            p['CustomerId'].toString() == widget.client.customerId.toString())
        .toList();

    if (clientPayments.isEmpty) {
      return const Center(
          child:
              Text("لا توجد تسديدات", style: TextStyle(fontFamily: 'Cairo')));
    }

    return ListView.builder(
      itemCount: clientPayments.length,
      itemBuilder: (context, index) {
        final payment = clientPayments[index];
        return Container(
          margin: const EdgeInsets.only(bottom: 10),
          child: Card(
            elevation: 2,
            shape:
                RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
            child: Padding(
              padding: const EdgeInsets.all(12),
              child: Column(
                children: [
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      Text(
                          "مبلغ: ${Formatters.formatNumber(double.parse(payment['Amount'].toString()))}",
                          style: const TextStyle(
                              fontFamily: 'Cairo',
                              fontWeight: FontWeight.bold)),
                      Text("${payment['PaymentDate'] ?? ''}",
                          style: const TextStyle(
                              fontFamily: 'Cairo', color: Colors.grey)),
                    ],
                  ),
                  const SizedBox(height: 10),
                  _buildPrintButton(payment),
                ],
              ),
            ),
          ),
        );
      },
    );
  }

  Widget _buildPrintButton(Map<String, dynamic> payment) {
    return ElevatedButton.icon(
      onPressed: () async {
        Client client = widget.client;
        SharedPreferences prefs = await SharedPreferences.getInstance();
        String receiptName = prefs.getString('ReceiptName') ?? '';
        String delegateName = prefs.getString('DelegateName') ?? '';
        String phoneNumberCompany = client.phoneNumberCompany;

        String itemsNames = client.itemsNames;
        double amountTotalSales = client.salePrice;
        double receiptsTotal = client.receipt;
        double amountRemaining = client.remaining;

        double amountPush = 0.0;
        try {
          amountPush = double.parse(payment['Amount'].toString());
        } catch (e) {
          amountPush = 0.0;
        }

        List<String> lastSevenDays = [
          "اليوم",
          "أمس",
          "قبل يومين",
          "قبل 3 أيام",
          "قبل 4 أيام",
          "قبل 5 أيام",
          "قبل 6 أيام"
        ];
        List<double> lastSevenAmounts = [
          client.amount1,
          client.amount2,
          client.amount3,
          client.amount4,
          client.amount5,
          client.amount6,
          client.amount7
        ];

        final report = ReportReceipt(
          customerName: client.name,
          receiptName: receiptName,
          delegateName: delegateName,
          itemsNames: itemsNames,
          amountTotalSales: amountTotalSales,
          receiptsTotal: receiptsTotal,
          amountRemaining: amountRemaining,
          amountPush: amountPush,
          phoneNumberCompany: phoneNumberCompany,
          lastSevenDays: lastSevenDays,
          lastSevenAmounts: lastSevenAmounts,
          countReceiptDevice: client.countReceiptDevice,
        );

        if (context.mounted) {
          await report.printReceipt(context);
        }
      },
      icon: const Icon(Icons.print, color: Colors.white),
      label: const Text('طباعة الوصل',
          style: TextStyle(fontFamily: 'Cairo', color: Colors.white)),
      style: ElevatedButton.styleFrom(
          backgroundColor: AppTheme.secondaryColor,
          shape:
              RoundedRectangleBorder(borderRadius: BorderRadius.circular(8))),
    );
  }
}

class CustomerCard extends StatelessWidget {
  final int customerId;
  final Client? initialClient;

  const CustomerCard({super.key, required this.customerId, this.initialClient});

  @override
  Widget build(BuildContext context) {
    if (initialClient == null) {
      return const Center(
          child:
              Text("جاري التحميل...", style: TextStyle(fontFamily: 'Cairo')));
    }
    // Placeholder implementation for CustomerCard detail view
    return SingleChildScrollView(
      padding: const EdgeInsets.all(20),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text("اسم العميل: ${initialClient!.name}",
              style: const TextStyle(
                  fontFamily: 'Cairo',
                  fontWeight: FontWeight.bold,
                  fontSize: 18)),
          const SizedBox(height: 10),
          Text("العنوان: ${initialClient!.address}",
              style: const TextStyle(fontFamily: 'Cairo')),
          const SizedBox(height: 10),
          Text("الهاتف: ${initialClient!.phone}",
              style: const TextStyle(fontFamily: 'Cairo')),
          const SizedBox(height: 10),
          Text("الباقي: ${Formatters.formatNumber(initialClient!.remaining)}",
              style: const TextStyle(fontFamily: 'Cairo', color: Colors.red)),
        ],
      ),
    );
  }
}
