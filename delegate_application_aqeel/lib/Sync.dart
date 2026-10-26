import 'package:delegate_application/utils/AppTheme.dart';
import 'package:flutter/material.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'package:http/http.dart' as http;
import 'dart:convert';
import 'package:delegate_application/services/DatabaseHelper.dart';
import 'HomePage.dart';

class Sync extends StatefulWidget {
  const Sync({super.key});

  @override
  _Sync createState() => _Sync();
}

class _Sync extends State<Sync> {
  bool _isSyncing = false;
  String _syncStatus = 'مزامنة';

  Future<Map<String, String>> _authHeaders() async {
    final prefs = await SharedPreferences.getInstance();
    final asyncId = prefs.getString('AsyncId') ?? '';
    final delegateId = prefs.getString('DelegateID') ?? '';
    return {
      "Content-Type": "application/json",
      if (asyncId.isNotEmpty) "X-Async-Id": asyncId,
      if (delegateId.isNotEmpty && delegateId != '0') "X-Delegate-Id": delegateId,
    };
  }

  Future<void> _setSelectDelegate() async {
    SharedPreferences prefs = await SharedPreferences.getInstance();
    String? delegateIdStr = prefs.getString('DelegateID');
    int delegateId = int.tryParse(delegateIdStr ?? '0') ?? 0;
    String? linkDelegate = prefs.getString('LinkDelegate');

    if (delegateId <= 0 || linkDelegate == null) return;

    final response = await http.get(
        Uri.parse('${linkDelegate}Delegates/GetDelegateSelect/$delegateId'),
        headers: await _authHeaders());

    if (response.statusCode != 200) {
      throw StateError('فشل جلب المندوبين البدلاء (${response.statusCode})');
    }

    List<dynamic> data = jsonDecode(response.body);
    final rows = <Map<String, dynamic>>[];
    for (var element in data) {
      rows.add({
        'DelegateId': int.tryParse(element['delegateId']?.toString() ?? '0') ?? 0,
        'DelegateName': element['delegateName']?.toString() ?? '',
        'ReceiptName': element['receiptName']?.toString() ?? '',
        'UpdateReceipt':
            (element['updateReceipt'] == true || element['updateReceipt'] == 1)
                ? 1
                : 0,
        'DeleteReceipt':
            (element['deleteReceipt'] == true || element['deleteReceipt'] == 1)
                ? 1
                : 0,
        'DevicePaymentState': (element['devicePaymentState'] == true ||
                element['devicePaymentState'] == 1)
            ? 1
            : 0,
      });
    }
    await DatabaseHelper().replaceTableRows('SelectDelegate', rows);
  }

  Future<void> _setDateWeek() async {
    SharedPreferences prefs = await SharedPreferences.getInstance();
    String? linkDelegate = prefs.getString('LinkDelegate');
    if (linkDelegate == null) return;

    final response = await http.get(
      Uri.parse('${linkDelegate}Customers/GetDateWeek'),
      headers: await _authHeaders(),
    );
    if (response.statusCode != 200) {
      throw StateError('فشل جلب تواريخ الأسبوع (${response.statusCode})');
    }

    var data = jsonDecode(response.body);
    await DatabaseHelper().replaceTableRows('DateWeek', [
      {
        'Date1': data['date1']?.toString() ?? '',
        'Date2': data['date2']?.toString() ?? '',
        'Date3': data['date3']?.toString() ?? '',
        'Date4': data['date4']?.toString() ?? '',
        'Date5': data['date5']?.toString() ?? '',
        'Date6': data['date6']?.toString() ?? '',
        'Date7': data['date7']?.toString() ?? '',
      }
    ]);
  }

  Future<void> _setCustomers() async {
    SharedPreferences prefs = await SharedPreferences.getInstance();
    String? delegateIdStr = prefs.getString('DelegateID');
    int delegateId = int.tryParse(delegateIdStr ?? '0') ?? 0;
    String? linkDelegate = prefs.getString('LinkDelegate');

    if (delegateId <= 0 || linkDelegate == null) return;

    final headers = await _authHeaders();
    final responseDelegate = await http.get(
      Uri.parse('${linkDelegate}Delegates/GetDelegateSelect/$delegateId'),
      headers: headers,
    );
    if (responseDelegate.statusCode != 200) {
      throw StateError(
          'فشل جلب قائمة المندوبين (${responseDelegate.statusCode})');
    }

    List<dynamic> delegateData = jsonDecode(responseDelegate.body);
    final customers = <Map<String, dynamic>>[];

    for (var delegateElement in delegateData) {
      final responseCustomer = await http.get(
        Uri.parse(
            '${linkDelegate}Customers/GetCustomersDelegateAll/${delegateElement['delegateId']}'),
        headers: headers,
      );
      if (responseCustomer.statusCode != 200) {
        throw StateError(
            'فشل جلب العملاء للمندوب ${delegateElement['delegateId']} (${responseCustomer.statusCode})');
      }
      List<dynamic> customerData = jsonDecode(responseCustomer.body);
      for (var customerElement in customerData) {
        customers.add({
          'CustomerId': customerElement['customerId'],
          'CustomerName': customerElement['customerName']?.toString() ?? '',
          'DelegateId': customerElement['delegateId'],
          'PhoneNumber': customerElement['phoneNumber']?.toString() ?? '',
          'AmountTotalSales': customerElement['amountTotalSales'],
          'AmountDaySales': customerElement['amountDaySales'],
          'ReceiptsTotal': customerElement['receiptsTotal'],
          'AmountRemaining': customerElement['amountRemaining'],
          'ItemsNames': customerElement['itemsNames']?.toString() ?? '',
          'CityId': customerElement['cityId'],
          'Amount1': customerElement['amount1'],
          'Amount2': customerElement['amount2'],
          'Amount3': customerElement['amount3'],
          'Amount4': customerElement['amount4'],
          'Amount5': customerElement['amount5'],
          'Amount6': customerElement['amount6'],
          'Amount7': customerElement['amount7'],
          'PhoneNumberCompany':
              customerElement['phoneNumberCompany']?.toString() ?? '',
          'CountReceiptDevice': customerElement['countReceiptDevice'],
          'Address': customerElement['address']?.toString() ?? '',
          'ShopName': customerElement['shopName']?.toString() ?? '',
          'NumberOfDayPayment': customerElement['numberOfDayPayment'],
          'IsLegal': customerElement['isLegal']?.toString() ?? 'false',
          'LastPaymentDate':
              customerElement['lastPaymentDate']?.toString() ?? '',
          'DateSaleDevice':
              customerElement['dateSaleDevice']?.toString() ?? '',
        });
      }
    }

    // Only wipe/replace after every fetch succeeded.
    await DatabaseHelper().replaceTableRows('Customer', customers);
  }

  Future<void> _startSync() async {
    setState(() {
      _isSyncing = true;
      _syncStatus = 'جارٍ المزامنة...';
    });

    try {
      await _setSelectDelegate();
      await _setDateWeek();
      await _setCustomers();
      if (!mounted) return;
      setState(() {
        _isSyncing = false;
        _syncStatus = 'تمت المزامنة بنجاح';
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _isSyncing = false;
        _syncStatus = 'فشلت المزامنة — بقيت البيانات المحلية';
      });
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(
            'لم تُحذف البيانات المحلية: $e',
            style: const TextStyle(fontFamily: 'Cairo'),
          ),
          backgroundColor: Colors.red.shade700,
        ),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    return PopScope(
      canPop: false,
      onPopInvokedWithResult: (didPop, result) async {
        if (didPop) return;
        Navigator.pushReplacement(
          context,
          MaterialPageRoute(builder: (context) => const HomePage()),
        );
      },
      child: Directionality(
        textDirection: TextDirection.rtl,
        child: Scaffold(
          backgroundColor: AppTheme.backgroundColor,
          appBar: AppBar(
            automaticallyImplyLeading: false,
            backgroundColor: AppTheme.primaryColor,
            elevation: 0,
            title: const Text('المزامنة',
                style: TextStyle(fontFamily: 'Cairo', color: Colors.white)),
            centerTitle: true,
            leading: IconButton(
              icon: const Icon(Icons.arrow_back, color: Colors.white),
              onPressed: () {
                Navigator.pushReplacement(context,
                    MaterialPageRoute(builder: (context) => const HomePage()));
              },
            ),
          ),
          body: Center(
            child: Container(
              padding: const EdgeInsets.all(30),
              margin: const EdgeInsets.all(20),
              decoration: BoxDecoration(
                  color: Theme.of(context).cardColor,
                  borderRadius: BorderRadius.circular(20),
                  boxShadow: [
                    BoxShadow(
                        color: Colors.black.withValues(alpha: 0.05),
                        blurRadius: 10,
                        offset: const Offset(0, 5))
                  ]),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  Container(
                    padding: const EdgeInsets.all(20),
                    decoration: BoxDecoration(
                        color: AppTheme.primaryColor.withValues(alpha: 0.1),
                        shape: BoxShape.circle),
                    child: Icon(Icons.sync,
                        size: 60, color: AppTheme.primaryColor),
                  ),
                  const SizedBox(height: 30),
                  const Text(
                    "مزامنة البيانات",
                    style: TextStyle(
                        fontFamily: 'Cairo',
                        fontSize: 20,
                        fontWeight: FontWeight.bold,
                        color: AppTheme.textColor),
                  ),
                  const SizedBox(height: 10),
                  Text(
                    "تأكد من وجود اتصال انترنت لتحديث البيانات",
                    textAlign: TextAlign.center,
                    style: TextStyle(
                        fontFamily: 'Cairo',
                        fontSize: 14,
                        color: Colors.grey[600]),
                  ),
                  const SizedBox(height: 40),
                  SizedBox(
                    width: double.infinity,
                    height: 50,
                    child: ElevatedButton(
                      onPressed: _isSyncing ? null : _startSync,
                      style: ElevatedButton.styleFrom(
                          backgroundColor: AppTheme.primaryColor,
                          elevation: 0,
                          shape: RoundedRectangleBorder(
                              borderRadius: BorderRadius.circular(10))),
                      child: _isSyncing
                          ? const SizedBox(
                              width: 20,
                              height: 20,
                              child: CircularProgressIndicator(
                                  color: Colors.white, strokeWidth: 2))
                          : Text(
                              _syncStatus == 'مزامنة'
                                  ? 'بدء المزامنة'
                                  : _syncStatus,
                              style: const TextStyle(
                                  fontFamily: 'Cairo',
                                  color: Colors.white,
                                  fontSize: 16),
                            ),
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}
