import 'dart:convert';
import 'package:delegate_application/utils/AppTheme.dart';
import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';
import 'package:delegate_application/utils/Formatters.dart';

class AllReceiptCustomer extends StatefulWidget {
  final int customerId;

  const AllReceiptCustomer({super.key, required this.customerId});

  @override
  _AllReceiptCustomerState createState() => _AllReceiptCustomerState();
}

class _AllReceiptCustomerState extends State<AllReceiptCustomer> {
  List<Map<String, dynamic>> customers = [];
  List<Map<String, dynamic>> filteredCustomers = [];
  double totalReceipt = 0.0;

  @override
  void initState() {
    super.initState();
    fetchCustomers();
  }

  Future<void> fetchCustomers() async {
    SharedPreferences prefs = await SharedPreferences.getInstance();
    String linkDelegate = prefs.getString('LinkDelegate') ?? '0';

    final response = await http.get(Uri.parse(
        '${linkDelegate}CustomersPayments/GetCustomersPaymentsCustomer/${widget.customerId}'));

    if (response.statusCode == 200) {
      List<dynamic> data = jsonDecode(response.body);
      if (mounted) {
        setState(() {
          customers = data
              .map((customer) => {
                    'customerName': customer['customerName'],
                    'paymentDate': customer['paymentDate'],
                    'amountDenar': customer['amountDenar'],
                  })
              .toList();
          filteredCustomers = customers;
          calculatetotalReceipt();
        });
      }
    } else {
      print('Failed to load customers');
    }
  }

  void calculatetotalReceipt() {
    totalReceipt = filteredCustomers.fold(0.0, (sum, customer) {
      var price = customer['amountDenar'];
      if (price is String) {
        price = double.parse(price.replaceAll(',', ''));
      }
      return sum + price;
    });
  }

  double roundToNearestThousand(double value) {
    return (value / 1000).round() * 1000;
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
          title: const Text('تسديدات العميل',
              style: TextStyle(fontFamily: 'Cairo', color: Colors.white)),
          centerTitle: true,
          iconTheme: const IconThemeData(color: Colors.white),
        ),
        body: Column(
          children: [
            Expanded(
              child: filteredCustomers.isEmpty
                  ? const Center(
                      child: Text('لا توجد بيانات للعرض',
                          style: TextStyle(
                              fontFamily: 'Cairo', color: Colors.grey)))
                  : ListView.builder(
                      padding: const EdgeInsets.all(16),
                      itemCount: filteredCustomers.length,
                      itemBuilder: (context, index) {
                        final customer = filteredCustomers[index];
                        return Container(
                          margin: const EdgeInsets.only(bottom: 12),
                          decoration: BoxDecoration(
                              color: Theme.of(context).cardColor,
                              borderRadius: BorderRadius.circular(15),
                              boxShadow: [
                                BoxShadow(
                                    color: Colors.black.withValues(alpha: 0.05),
                                    blurRadius: 8,
                                    offset: const Offset(0, 4))
                              ]),
                          padding: const EdgeInsets.all(16.0),
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Row(
                                mainAxisAlignment:
                                    MainAxisAlignment.spaceBetween,
                                children: [
                                  Text('${customer['customerName']}',
                                      style: const TextStyle(
                                          color: AppTheme.textColor,
                                          fontWeight: FontWeight.bold,
                                          fontSize: 16,
                                          fontFamily: 'Cairo')),
                                  Text(
                                      Formatters.formatDate(
                                          customer['paymentDate']),
                                      style: const TextStyle(
                                          color: Colors.grey,
                                          fontSize: 12,
                                          fontFamily: 'Cairo')),
                                ],
                              ),
                              const Divider(),
                              Row(
                                mainAxisAlignment: MainAxisAlignment.end,
                                children: [
                                  Row(
                                    children: [
                                      Text(
                                          Formatters.formatNumber(
                                              roundToNearestThousand(
                                                  double.parse(
                                                      customer['amountDenar']
                                                          .toString()))),
                                          style: const TextStyle(
                                              color: AppTheme.primaryColor,
                                              fontWeight: FontWeight.bold,
                                              fontSize: 16,
                                              fontFamily: 'Cairo')),
                                      const SizedBox(width: 4),
                                      const Text("دع",
                                          style: TextStyle(
                                              color: AppTheme.primaryColor,
                                              fontWeight: FontWeight.bold,
                                              fontSize: 16,
                                              fontFamily: 'Cairo')),
                                    ],
                                  ),
                                ],
                              ),
                            ],
                          ),
                        );
                      },
                    ),
            ),
            Container(
              padding: const EdgeInsets.all(20),
              decoration: BoxDecoration(
                  color: Theme.of(context).cardColor,
                  borderRadius: const BorderRadius.only(
                      topLeft: Radius.circular(30),
                      topRight: Radius.circular(30)),
                  boxShadow: [
                    BoxShadow(
                        color: Colors.black.withValues(alpha: 0.05),
                        blurRadius: 10,
                        offset: const Offset(0, -4))
                  ]),
              child: Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  const Text('مجموع المبلغ الواصل',
                      style: TextStyle(
                          fontSize: 16,
                          fontFamily: 'Cairo',
                          color: AppTheme.textColor)),
                  Container(
                    padding: const EdgeInsets.symmetric(
                        horizontal: 20.0, vertical: 10.0),
                    decoration: BoxDecoration(
                        color: AppTheme.primaryColor.withValues(alpha: 0.1),
                        borderRadius: BorderRadius.circular(12),
                        border: Border.all(
                            color:
                                AppTheme.primaryColor.withValues(alpha: 0.3))),
                    child: Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Text(
                          Formatters.formatNumber(roundToNearestThousand(
                              double.parse(totalReceipt.toString()))),
                          style: const TextStyle(
                              fontSize: 16,
                              color: AppTheme.primaryColor,
                              fontWeight: FontWeight.bold,
                              fontFamily: 'Cairo'),
                        ),
                        const SizedBox(width: 4),
                        const Text(
                          "دع",
                          style: TextStyle(
                              fontSize: 16,
                              color: AppTheme.primaryColor,
                              fontWeight: FontWeight.bold,
                              fontFamily: 'Cairo'),
                        ),
                      ],
                    ),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}
