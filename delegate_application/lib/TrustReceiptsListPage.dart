import 'dart:convert';
import 'package:delegate_application/utils/AppTheme.dart';
import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';
import 'package:delegate_application/services/TrustReceiptPdfService.dart';
import 'package:delegate_application/TrustReceiptFormPage.dart';

class TrustReceiptsListPage extends StatefulWidget {
  const TrustReceiptsListPage({super.key});

  @override
  _TrustReceiptsListPageState createState() => _TrustReceiptsListPageState();
}

class _TrustReceiptsListPageState extends State<TrustReceiptsListPage> {
  final TextEditingController _searchController = TextEditingController();
  final ScrollController _scrollController = ScrollController();
  
  List<dynamic> _receipts = [];
  int _totalCount = 0;
  int _pageNumber = 1;
  final int _pageSize = 10;
  bool _isLoading = false;
  bool _hasMore = true;

  @override
  void initState() {
    super.initState();
    _fetchReceipts();
    _scrollController.addListener(() {
      if (_scrollController.position.pixels == _scrollController.position.maxScrollExtent) {
        _fetchReceipts(loadMore: true);
      }
    });
  }

  @override
  void dispose() {
    _searchController.dispose();
    _scrollController.dispose();
    super.dispose();
  }

  Future<void> _fetchReceipts({bool loadMore = false}) async {
    if (_isLoading || (!loadMore && !_hasMore && _receipts.isNotEmpty)) return;

    if (!loadMore) {
      setState(() {
        _pageNumber = 1;
        _receipts.clear();
        _hasMore = true;
        _isLoading = true;
      });
    } else {
      setState(() {
        _isLoading = true;
      });
    }

    try {
      SharedPreferences prefs = await SharedPreferences.getInstance();
      String linkDelegate = prefs.getString('LinkDelegate') ?? '0';
      int delegateId = int.parse(prefs.getString('DelegateID') ?? '0');

      var url = Uri.parse(
          '${linkDelegate}TrustReceipts/paged?pageNumber=$_pageNumber&pageSize=$_pageSize&searchTerm=${_searchController.text}&delegateId=$delegateId');
      
      var response = await http.get(url, headers: {"Content-Type": "application/json"});

      if (response.statusCode == 200) {
        var data = json.decode(response.body);
        List<dynamic> newReceipts = data['data'] ?? [];
        int total = data['totalCount'] ?? 0;

        setState(() {
          _totalCount = total;
          _receipts.addAll(newReceipts);
          if (newReceipts.length < _pageSize) {
            _hasMore = false;
          } else {
            _pageNumber++;
          }
          _isLoading = false;
        });
      } else {
        setState(() {
          _isLoading = false;
        });
      }
    } catch (e) {
      setState(() {
        _isLoading = false;
      });
    }
  }

  void _onSearchChanged() {
    _hasMore = true;
    _fetchReceipts();
  }

  @override
  Widget build(BuildContext context) {
    return Directionality(
      textDirection: TextDirection.rtl,
      child: Scaffold(
        appBar: AppBar(
          title: const Text('وصولات الأمانة', style: TextStyle(fontFamily: 'Cairo')),
          backgroundColor: AppTheme.primaryColor,
          foregroundColor: Colors.white,
        ),
        body: Column(
          children: [
            _buildSummaryHeader(),
            _buildSearchBar(),
            Expanded(
              child: _receipts.isEmpty && !_isLoading
                  ? const Center(child: Text("لا توجد وصولات", style: TextStyle(fontFamily: 'Cairo')))
                  : ListView.builder(
                      controller: _scrollController,
                      itemCount: _receipts.length + (_hasMore ? 1 : 0),
                      itemBuilder: (context, index) {
                        if (index == _receipts.length) {
                          return const Center(
                              child: Padding(
                            padding: EdgeInsets.all(8.0),
                            child: CircularProgressIndicator(color: AppTheme.primaryColor),
                          ));
                        }
                        return _buildReceiptCard(_receipts[index]);
                      },
                    ),
            ),
          ],
        ),
        floatingActionButton: FloatingActionButton(
          onPressed: () async {
            final result = await Navigator.push<bool>(
              context,
              MaterialPageRoute(builder: (context) => const TrustReceiptFormPage()),
            );
            if (result == true) {
              _hasMore = true;
              _fetchReceipts();
            }
          },
          backgroundColor: AppTheme.primaryColor,
          child: const Icon(Icons.add, color: Colors.white),
        ),
      ),
    );
  }

  Widget _buildSummaryHeader() {
    return Container(
      padding: const EdgeInsets.all(16),
      color: AppTheme.primaryColor.withValues(alpha: 0.1),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          const Text(
            "إجمالي عدد الوصولات:",
            style: TextStyle(fontFamily: 'Cairo', fontWeight: FontWeight.bold, fontSize: 16),
          ),
          Text(
            "$_totalCount",
            style: const TextStyle(
                fontFamily: 'Cairo', fontWeight: FontWeight.bold, fontSize: 18, color: AppTheme.primaryColor),
          ),
        ],
      ),
    );
  }

  Widget _buildSearchBar() {
    return Padding(
      padding: const EdgeInsets.all(16.0),
      child: Row(
        children: [
          Expanded(
            child: TextField(
              controller: _searchController,
              decoration: InputDecoration(
                hintText: 'ابحث باسم العميل...',
                hintStyle: const TextStyle(fontFamily: 'Cairo'),
                prefixIcon: const Icon(Icons.search, color: AppTheme.primaryColor),
                border: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(15),
                  borderSide: const BorderSide(color: AppTheme.primaryColor),
                ),
                focusedBorder: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(15),
                  borderSide: const BorderSide(color: AppTheme.primaryColor, width: 2),
                ),
              ),
              onSubmitted: (_) => _onSearchChanged(),
            ),
          ),
          const SizedBox(width: 10),
          ElevatedButton(
            onPressed: _onSearchChanged,
            style: ElevatedButton.styleFrom(
              backgroundColor: AppTheme.primaryColor,
              shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(15)),
              padding: const EdgeInsets.symmetric(vertical: 15, horizontal: 20),
            ),
            child: const Text('بحث', style: TextStyle(fontFamily: 'Cairo', color: Colors.white)),
          )
        ],
      ),
    );
  }

  Widget _buildReceiptCard(dynamic receipt) {
    return Card(
      margin: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(15)),
      elevation: 3,
      child: Padding(
        padding: const EdgeInsets.all(16.0),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Expanded(
                  child: Text(
                    "العميل: ${receipt['buyerName'] ?? 'غير محدد'}",
                    style: const TextStyle(fontFamily: 'Cairo', fontWeight: FontWeight.bold, fontSize: 16),
                    overflow: TextOverflow.ellipsis,
                  ),
                ),
                Text(
                  "رقم العقد: ${receipt['contractNumber'] ?? '-'}",
                  style: const TextStyle(fontFamily: 'Cairo', color: Colors.grey, fontSize: 12),
                ),
              ],
            ),
            const SizedBox(height: 8),
            Text(
              "البضاعة: ${receipt['productName'] ?? '-'}",
              style: const TextStyle(fontFamily: 'Cairo', fontSize: 14),
            ),
            const SizedBox(height: 8),
            Text(
              "تاريخ العقد: ${receipt['contractDate'] != null ? receipt['contractDate'].substring(0, 10) : '-'}",
              style: const TextStyle(fontFamily: 'Cairo', fontSize: 12, color: Colors.grey),
            ),
            const Divider(),
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceEvenly,
              children: [
                TextButton.icon(
                  onPressed: () async {
                    final result = await Navigator.push<bool>(
                      context,
                      MaterialPageRoute(
                        builder: (context) => const TrustReceiptFormPage(),
                        settings: RouteSettings(arguments: receipt),
                      ),
                    );
                    if (result == true) {
                      _hasMore = true;
                      _fetchReceipts();
                    }
                  },
                  icon: const Icon(Icons.edit, color: AppTheme.primaryColor),
                  label: const Text('تعديل', style: TextStyle(fontFamily: 'Cairo', color: AppTheme.primaryColor)),
                ),
                TextButton.icon(
                  onPressed: () {
                    TrustReceiptPdfService.printReceipt(receipt);
                  },
                  icon: const Icon(Icons.print, color: Colors.green),
                  label: const Text('طباعة', style: TextStyle(fontFamily: 'Cairo', color: Colors.green)),
                ),
              ],
            )
          ],
        ),
      ),
    );
  }
}
