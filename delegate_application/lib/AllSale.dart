import 'dart:convert';
import 'package:delegate_application/utils/AppTheme.dart';
import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';

class AllSale extends StatefulWidget {
  const AllSale({super.key});

  @override
  _AllSaleState createState() => _AllSaleState();
}

class _AllSaleState extends State<AllSale> {
  List<Map<String, dynamic>> customers = [];
  List<Map<String, dynamic>> filteredCustomers = [];
  double totalSalePrice = 0.0;
  final TextEditingController _searchController = TextEditingController();

  String formatNumber(double number) {
    String numberStr =
        number % 1 == 0 ? number.toInt().toString() : number.toString();
    String reversedStr = numberStr.split('').reversed.join('');
    String formattedReversedStr =
        reversedStr.replaceAllMapped(RegExp(r'\d{3}'), (match) {
      return '${match.group(0)},';
    });
    String formattedStr = formattedReversedStr
        .split('')
        .reversed
        .join('')
        .replaceFirst(RegExp(r'^,'), '');
    return formattedStr;
  }

  @override
  void initState() {
    super.initState();
  }

  Future<void> fetchCustomers(String name) async {
    SharedPreferences prefs = await SharedPreferences.getInstance();
    String linkDelegate = prefs.getString('LinkDelegate') ?? '0';
    int delegateId = int.parse(prefs.getString('DelegateID') ?? '0');

    final response = await http.get(Uri.parse(
        '${linkDelegate}CustomersSales/GetCustomersSalesCustomerName/id=$delegateId&&customerName=$name'));

    if (response.statusCode == 200) {
      List<dynamic> data = jsonDecode(response.body);
      if (mounted) {
        setState(() {
          customers = data
              .map((customer) => {
                    'customerName': customer['customerName'],
                    'itemsNames': customer['itemsNames'],
                    'dateCreate': customer['dateCreate'],
                    'amountTotalSalesDenar': customer['amountTotalSalesDenar'],
                  })
              .toList();
          filteredCustomers = customers;
          calculateTotalSalePrice();
        });
      }
    } else {
      print('Failed to load customers');
    }
  }

  void calculateTotalSalePrice() {
    totalSalePrice = filteredCustomers.fold(0.0, (sum, customer) {
      var price = customer['amountTotalSalesDenar'];
      if (price is String) {
        price = double.parse(price.replaceAll(',', ''));
      }
      return sum + price;
    });
  }

  double roundToNearestThousand(double value) {
    return (value / 1000).round() * 1000;
  }

  String formatDate(String dateString) {
    DateTime parsedDate = DateTime.parse(dateString);
    String formattedDate =
        '${parsedDate.year}/${parsedDate.month.toString().padLeft(2, '0')}/${parsedDate.day.toString().padLeft(2, '0')}';
    return formattedDate;
  }

  void handleSearch() {
    String searchText = _searchController.text.trim();
    if (searchText.isNotEmpty) {
      fetchCustomers(searchText);
    }
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
          title: Container(
            height: 45,
            decoration: BoxDecoration(
              color: Colors.white,
              borderRadius: BorderRadius.circular(22.5),
            ),
            padding: const EdgeInsets.symmetric(horizontal: 10),
            child: TextField(
              controller: _searchController,
              textAlignVertical: TextAlignVertical.center,
              style: const TextStyle(fontFamily: 'Cairo'),
              decoration: InputDecoration(
                hintText: 'بحث...',
                border: InputBorder.none,
                enabledBorder: InputBorder.none,
                focusedBorder: InputBorder.none,
                suffixIcon: IconButton(
                  icon: const Icon(Icons.search, color: AppTheme.primaryColor),
                  onPressed: handleSearch,
                  padding: EdgeInsets.zero,
                  constraints: const BoxConstraints(),
                  style: const ButtonStyle(
                    tapTargetSize: MaterialTapTargetSize.shrinkWrap,
                  ),
                ),
                contentPadding: const EdgeInsets.symmetric(horizontal: 10),
              ),
              onSubmitted: (_) => handleSearch(),
            ),
          ),
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
                      padding: const EdgeInsets.all(15),
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
                                          fontFamily: 'Cairo',
                                          fontWeight: FontWeight.bold,
                                          fontSize: 16)),
                                  Text(formatDate(customer['dateCreate']),
                                      style: const TextStyle(
                                          color: Colors.grey,
                                          fontFamily: 'Cairo',
                                          fontSize: 12)),
                                ],
                              ),
                              const Divider(),
                              Text('المباع: ${customer['itemsNames']}',
                                  style: TextStyle(
                                      color: Colors.grey[700],
                                      fontFamily: 'Cairo')),
                              const SizedBox(height: 10),
                              Row(
                                mainAxisAlignment: MainAxisAlignment.end,
                                children: [
                                  Row(
                                    children: [
                                      Text(
                                          formatNumber(roundToNearestThousand(
                                              double.parse(customer[
                                                      'amountTotalSalesDenar']
                                                  .toString()))),
                                          style: const TextStyle(
                                              color: AppTheme.primaryColor,
                                              fontFamily: 'Cairo',
                                              fontWeight: FontWeight.bold,
                                              fontSize: 16)),
                                      const SizedBox(width: 4),
                                      const Text('دع',
                                          style: TextStyle(
                                              color: AppTheme.primaryColor,
                                              fontFamily: 'Cairo',
                                              fontWeight: FontWeight.bold,
                                              fontSize: 16)),
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
                  const Text('مجموع سعر البيع',
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
                          formatNumber(roundToNearestThousand(
                              double.parse(totalSalePrice.toString()))),
                          style: const TextStyle(
                              fontSize: 16,
                              color: AppTheme.primaryColor,
                              fontFamily: 'Cairo',
                              fontWeight: FontWeight.bold),
                        ),
                        const SizedBox(width: 4),
                        const Text(
                          'دع',
                          style: TextStyle(
                              fontSize: 16,
                              color: AppTheme.primaryColor,
                              fontFamily: 'Cairo',
                              fontWeight: FontWeight.bold),
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
