import 'package:delegate_application/utils/AppTheme.dart';
import 'package:flutter/material.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:sqflite/sqflite.dart';

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

  @override
  void initState() {
    super.initState();
  }

  Future<void> _setSelectDelegate() async {
    SharedPreferences prefs = await SharedPreferences.getInstance();
    String? delegateIdStr = prefs.getString('DelegateID');
    int delegateId = int.tryParse(delegateIdStr ?? '0') ?? 0;
    String? linkDelegate = prefs.getString('LinkDelegate');

    if (delegateId > 0 && linkDelegate != null) {
      await _clearTable('SelectDelegate');
      final response = await http.get(
          Uri.parse('${linkDelegate}Delegates/GetDelegateSelect/$delegateId'),
          headers: {"Content-Type": "application/json"});

      debugPrint(
          response.statusCode.toString()); // Replaced print with debugPrint
      if (response.statusCode == 200) {
        List<dynamic> data = jsonDecode(response.body);
        for (var element in data) {
          debugPrint(element['delegateId']
              .toString()); // Replaced print with debugPrint
          await _addSelectDelegate(
            element['delegateId'],
            element['delegateName'],
            element['receiptName'],
            element['updateReceipt'],
            element['deleteReceipt'],
            element['devicePaymentState'],
          );
        }
      }
    }
  }

  Future<void> _addSelectDelegate(
      dynamic delegateId,
      dynamic delegateName,
      dynamic receiptName,
      dynamic updateReceipt,
      dynamic deleteReceipt,
      dynamic devicePaymentState) async {
    final db = await DatabaseHelper().database;
    await db.insert(
      'SelectDelegate',
      {
        'DelegateId': int.tryParse(delegateId?.toString() ?? '0') ?? 0,
        'DelegateName': delegateName?.toString() ?? '',
        'ReceiptName': receiptName?.toString() ?? '',
        'UpdateReceipt': (updateReceipt == true || updateReceipt == 1) ? 1 : 0,
        'DeleteReceipt': (deleteReceipt == true || deleteReceipt == 1) ? 1 : 0,
        'DevicePaymentState':
            (devicePaymentState == true || devicePaymentState == 1) ? 1 : 0,
      },
      conflictAlgorithm: ConflictAlgorithm.replace,
    );
  }

  Future<void> _setDateWeek() async {
    SharedPreferences prefs = await SharedPreferences.getInstance();
    String? linkDelegate = prefs.getString('LinkDelegate');

    if (linkDelegate != null) {
      await _clearDateWeek();
      final response =
          await http.get(Uri.parse('${linkDelegate}Customers/GetDateWeek'));

      if (response.statusCode == 200) {
        var data = jsonDecode(response.body);
        await _addDateWeek(
          data['date1'],
          data['date2'],
          data['date3'],
          data['date4'],
          data['date5'],
          data['date6'],
          data['date7'],
        );
      }
    }
  }

  Future<void> _addDateWeek(
      dynamic date1,
      dynamic date2,
      dynamic date3,
      dynamic date4,
      dynamic date5,
      dynamic date6,
      dynamic date7) async {
    final db = await DatabaseHelper().database;
    await db.insert(
      'DateWeek',
      {
        'Date1': date1?.toString() ?? '',
        'Date2': date2?.toString() ?? '',
        'Date3': date3?.toString() ?? '',
        'Date4': date4?.toString() ?? '',
        'Date5': date5?.toString() ?? '',
        'Date6': date6?.toString() ?? '',
        'Date7': date7?.toString() ?? '',
      },
      conflictAlgorithm: ConflictAlgorithm.replace,
    );
  }

  Future<void> _setCustomers() async {
    SharedPreferences prefs = await SharedPreferences.getInstance();
    String? delegateIdStr = prefs.getString('DelegateID');
    int delegateId = int.tryParse(delegateIdStr ?? '0') ?? 0;
    String? linkDelegate = prefs.getString('LinkDelegate');

    if (delegateId > 0 && linkDelegate != null) {
      await _clearTable('Customer');
      final responseDelegate = await http.get(
          Uri.parse('${linkDelegate}Delegates/GetDelegateSelect/$delegateId'));
      if (responseDelegate.statusCode == 200) {
        List<dynamic> delegateData = jsonDecode(responseDelegate.body);
        for (var delegateElement in delegateData) {
          print(delegateElement['delegateId']);
          final responseCustomer = await http.get(Uri.parse(
              '${linkDelegate}Customers/GetCustomersDelegateAll/${delegateElement['delegateId']}'));
          if (responseCustomer.statusCode == 200) {
            List<dynamic> customerData = jsonDecode(responseCustomer.body);
            for (var customerElement in customerData) {
              print(customerElement['customerId']);
              await _addCustomer({
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
        }
      }
    }
  }

  Future<void> _addCustomer(Map<String, dynamic> customerData) async {
    final db = await DatabaseHelper().database;
    await db.insert(
      'Customer',
      customerData,
      conflictAlgorithm: ConflictAlgorithm.replace,
    );
  }

  Future<void> _clearTable(String tableName) async {
    final db = await DatabaseHelper().database;
    await db.delete(tableName);
  }

  Future<void> _clearDateWeek() async {
    final db = await DatabaseHelper().database;
    await db.delete('DateWeek');
  }

  Future<void> _startSync() async {
    setState(() {
      _isSyncing = true;
      _syncStatus = 'جارٍ المزامنة...';
    });

    await _setSelectDelegate();
    await _setDateWeek();
    await _setCustomers();

    setState(() {
      _isSyncing = false;
      _syncStatus = 'تمت المزامنة بنجاح';
    });
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
