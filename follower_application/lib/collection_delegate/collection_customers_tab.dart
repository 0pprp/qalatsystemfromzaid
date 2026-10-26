import 'dart:convert';

import 'package:follower_application/AsyncIdChecker.dart';
import 'package:follower_application/collection_delegate/ReportReceipt.dart';
import 'package:follower_application/collection_delegate/services/receipt_week_amounts.dart';
import 'package:follower_application/collection_delegate/utils/thousands_separator_input_formatter.dart';
import 'package:follower_application/config/app_env.dart';
import 'package:follower_application/utils/AppTheme.dart';
import 'package:follower_application/utils/Formatters.dart';
import 'package:follower_application/utils/display_name.dart';
import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';
import 'package:uuid/uuid.dart';

class CollectionCustomersTab extends StatefulWidget {
  final int listId;
  final String listName;
  final VoidCallback? onPaid;

  const CollectionCustomersTab({
    super.key,
    required this.listId,
    required this.listName,
    this.onPaid,
  });

  @override
  State<CollectionCustomersTab> createState() => CollectionCustomersTabState();
}

class CollectionCustomersTabState extends State<CollectionCustomersTab> {
  bool _loading = true;
  String _error = '';
  String _query = '';
  final _search = TextEditingController();
  List<Map<String, dynamic>> _rows = [];

  @override
  void initState() {
    super.initState();
    reload();
  }

  @override
  void dispose() {
    _search.dispose();
    super.dispose();
  }

  Future<Map<String, String>> _session() async {
    final prefs = await SharedPreferences.getInstance();
    return {
      'asyncId': prefs.getString('AsyncId') ?? '',
      'link': AppEnv.apiBase(fallback: prefs.getString('LinkDelegate') ?? ''),
      'name': prefs.getString('DelegateName') ?? 'مسؤول اللجنة',
    };
  }

  Future<void> reload() async {
    setState(() {
      _loading = true;
      _error = '';
    });
    try {
      final s = await _session();
      final uri = Uri.parse('${s['link']}Followers/Collection/Customers')
          .replace(queryParameters: {
        'asyncId': s['asyncId'],
        'listId': '${widget.listId}',
      });
      final res = await http.get(uri).timeout(const Duration(seconds: 60));
      if (res.statusCode == 401) {
        await AsyncIdChecker.logout();
        if (mounted) Navigator.pushReplacementNamed(context, '/Login');
        return;
      }
      if (res.statusCode != 200) {
        String msg = 'تعذر تحميل الزبائن';
        try {
          final b = json.decode(res.body);
          msg = '${b['message'] ?? b['Message'] ?? msg}';
        } catch (_) {}
        throw Exception(msg);
      }
      final data = json.decode(res.body) as Map;
      final raw = data['items'] ?? data['Items'] ?? [];
      final rows = (raw as List)
          .map((e) => Map<String, dynamic>.from(e as Map))
          .toList();
      if (mounted) {
        setState(() {
          _rows = rows;
          _loading = false;
        });
      }
    } catch (e) {
      if (mounted) {
        setState(() {
          _loading = false;
          _error = e.toString().replaceFirst('Exception: ', '');
        });
      }
    }
  }

  double _d(dynamic v) {
    if (v == null) return 0;
    if (v is num) return v.toDouble();
    return double.tryParse('$v') ?? 0;
  }

  List<Map<String, dynamic>> get _visible {
    final q = _query.trim();
    if (q.isEmpty) return _rows;
    return _rows
        .where((r) =>
            '${r['customerName'] ?? r['CustomerName'] ?? ''}'.contains(q) ||
            '${r['phoneNumber'] ?? r['PhoneNumber'] ?? ''}'.contains(q))
        .toList();
  }

  Future<void> _pay(Map<String, dynamic> row) async {
    final name = '${row['customerName'] ?? row['CustomerName'] ?? ''}';
    final due = _d(row['amountDaySales'] ?? row['AmountDaySales']);
    final customerId =
        int.tryParse('${row['customerId'] ?? row['CustomerId'] ?? 0}') ?? 0;
    if (customerId <= 0) return;
    if (row['paidToday'] == true || row['PaidToday'] == true) {
      ScaffoldMessenger.of(context).showSnackBar(const SnackBar(
          content: Text('تم التسديد لهذا الزبون اليوم',
              style: TextStyle(fontFamily: 'Cairo'))));
      return;
    }

    final amountController =
        TextEditingController(text: due > 0 ? due.toInt().toString() : '');
    var submitting = false;
    final ok = await showDialog<bool>(
      context: context,
      barrierDismissible: false,
      builder: (dialogContext) {
        return StatefulBuilder(builder: (dialogContext, setDialog) {
          return Directionality(
            textDirection: TextDirection.rtl,
            child: Dialog(
              shape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(20)),
              child: Padding(
                padding: const EdgeInsets.all(20),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    const Text('إضافة تسديد',
                        textAlign: TextAlign.center,
                        style: TextStyle(
                            fontFamily: 'Cairo',
                            fontSize: 20,
                            fontWeight: FontWeight.bold)),
                    const SizedBox(height: 8),
                    Text(name,
                        textAlign: TextAlign.center,
                        style: const TextStyle(
                            fontFamily: 'Cairo',
                            fontWeight: FontWeight.bold,
                            fontSize: 16)),
                    const SizedBox(height: 16),
                    TextField(
                      controller: amountController,
                      keyboardType: TextInputType.number,
                      inputFormatters: [ThousandsSeparatorInputFormatter()],
                      enabled: !submitting,
                      style: const TextStyle(fontFamily: 'Cairo'),
                      decoration: InputDecoration(
                        filled: true,
                        hintText: 'المبلغ',
                        hintStyle: const TextStyle(fontFamily: 'Cairo'),
                        prefixIcon: const Icon(Icons.attach_money,
                            color: AppTheme.primaryColor),
                        border: OutlineInputBorder(
                          borderRadius: BorderRadius.circular(12),
                          borderSide: BorderSide.none,
                        ),
                      ),
                    ),
                    if (submitting) ...[
                      const SizedBox(height: 12),
                      const LinearProgressIndicator(),
                    ],
                    const SizedBox(height: 20),
                    Row(
                      children: [
                        Expanded(
                          child: ElevatedButton.icon(
                            onPressed: submitting
                                ? null
                                : () async {
                                    final amount =
                                        ThousandsSeparatorInputFormatter
                                                .parseMoney(
                                                    amountController.text) ??
                                            0;
                                    if (amount <= 0) {
                                      ScaffoldMessenger.of(context)
                                          .showSnackBar(const SnackBar(
                                              content: Text('أدخل مبلغاً صالحاً',
                                                  style: TextStyle(
                                                      fontFamily: 'Cairo'))));
                                      return;
                                    }
                                    setDialog(() => submitting = true);
                                    final success = await _submitPayment(
                                      customerId: customerId,
                                      amount: amount,
                                      row: row,
                                    );
                                    if (!dialogContext.mounted) return;
                                    if (success) {
                                      Navigator.pop(dialogContext, true);
                                    } else {
                                      setDialog(() => submitting = false);
                                    }
                                  },
                            icon: const Icon(Icons.check_circle_outline,
                                color: Colors.white),
                            label: Text(submitting ? 'جاري...' : 'تسديد',
                                style: const TextStyle(
                                    fontFamily: 'Cairo', color: Colors.white)),
                            style: ElevatedButton.styleFrom(
                              backgroundColor: AppTheme.primaryColor,
                              padding:
                                  const EdgeInsets.symmetric(vertical: 12),
                            ),
                          ),
                        ),
                        const SizedBox(width: 10),
                        Expanded(
                          child: OutlinedButton(
                            onPressed: submitting
                                ? null
                                : () => Navigator.pop(dialogContext, false),
                            child: const Text('إلغاء',
                                style: TextStyle(fontFamily: 'Cairo')),
                          ),
                        ),
                      ],
                    ),
                  ],
                ),
              ),
            ),
          );
        });
      },
    );
    amountController.dispose();
    if (ok == true) {
      widget.onPaid?.call();
      await reload();
    }
  }

  Future<void> _reprint(Map<String, dynamic> row) async {
    final paid = row['paidToday'] == true || row['PaidToday'] == true;
    if (!paid) {
      ScaffoldMessenger.of(context).showSnackBar(const SnackBar(
          content: Text('الطباعة بعد التسديد فقط',
              style: TextStyle(fontFamily: 'Cairo'))));
      return;
    }
    try {
      final s = await _session();
      var amount = _d(row['paidTodayAmount'] ?? row['PaidTodayAmount']);
      if (amount <= 0) {
        amount = _d(row['amountDaySales'] ?? row['AmountDaySales']);
      }
      if (amount <= 0) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(
            content: Text('لا يوجد مبلغ تسديد للطباعة',
                style: TextStyle(fontFamily: 'Cairo'))));
        return;
      }
      final receiptNo = ReceiptWeekAmounts.printedReceiptNumber(
          '${row['countReceiptDevice'] ?? row['CountReceiptDevice'] ?? '0'}');
      final report = ReportReceipt(
        customerName: '${row['customerName'] ?? row['CustomerName'] ?? ''}',
        receiptName: s['name'] ?? 'مسؤول اللجنة',
        delegateName: widget.listName,
        itemsNames: '${row['itemsNames'] ?? row['ItemsNames'] ?? ''}',
        amountTotalSales:
            _d(row['amountTotalSales'] ?? row['AmountTotalSales']),
        receiptsTotal: _d(row['receiptsTotal'] ?? row['ReceiptsTotal']),
        amountRemaining:
            _d(row['amountRemaining'] ?? row['AmountRemaining']),
        amountPush: amount,
        phoneNumberCompany:
            '${row['phoneNumberCompany'] ?? row['PhoneNumberCompany'] ?? ''}',
        lastSevenDays: ReceiptWeekAmounts.arabicDayLabels,
        lastSevenAmounts: [
          _d(row['amount1'] ?? row['Amount1']),
          _d(row['amount2'] ?? row['Amount2']),
          _d(row['amount3'] ?? row['Amount3']),
          _d(row['amount4'] ?? row['Amount4']),
          _d(row['amount5'] ?? row['Amount5']),
          _d(row['amount6'] ?? row['Amount6']),
          _d(row['amount7'] ?? row['Amount7']),
        ],
        countReceiptDevice: receiptNo,
        receiptNumberAlreadyPrinted: true,
      );
      if (mounted) await report.printReceipt(context);
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(
            content: Text('تعذر الطباعة',
                style: TextStyle(fontFamily: 'Cairo'))));
      }
    }
  }

  Future<bool> _submitPayment({
    required int customerId,
    required double amount,
    required Map<String, dynamic> row,
  }) async {
    try {
      final s = await _session();
      final uuid = const Uuid().v4();
      final receiptNo = ReceiptWeekAmounts.printedReceiptNumber(
          '${row['countReceiptDevice'] ?? row['CountReceiptDevice'] ?? '0'}');
      final uri =
          Uri.parse('${s['link']}Followers/Collection/PostPaymentIdempotent');
      final res = await http
          .post(
            uri,
            headers: {'Content-Type': 'application/json'},
            body: json.encode({
              'asyncId': s['asyncId'],
              'listId': widget.listId,
              'customerId': customerId,
              'amount': amount,
              'location': 'follower-collection',
              'clientPaymentId': uuid,
              'createdAtUtc': DateTime.now().toUtc().toIso8601String(),
              'receiptNumber': receiptNo,
            }),
          )
          .timeout(const Duration(seconds: 30));
      if (!mounted) return false;
      if (res.statusCode != 200) {
        String msg = 'تعذر التسديد';
        try {
          final b = json.decode(res.body);
          msg = '${b['message'] ?? b['Message'] ?? msg}';
        } catch (_) {}
        ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(content: Text(msg, style: const TextStyle(fontFamily: 'Cairo'))));
        return false;
      }

      final receipts = _d(row['receiptsTotal'] ?? row['ReceiptsTotal']);
      final total = _d(row['amountTotalSales'] ?? row['AmountTotalSales']);
      final remaining = _d(row['amountRemaining'] ?? row['AmountRemaining']);
      final week = [
        _d(row['amount1'] ?? row['Amount1']),
        _d(row['amount2'] ?? row['Amount2']),
        _d(row['amount3'] ?? row['Amount3']),
        _d(row['amount4'] ?? row['Amount4']),
        _d(row['amount5'] ?? row['Amount5']),
        _d(row['amount6'] ?? row['Amount6']),
        _d(row['amount7'] ?? row['Amount7']),
      ];
      final report = ReportReceipt(
        customerName: '${row['customerName'] ?? row['CustomerName'] ?? ''}',
        receiptName: s['name'] ?? 'مسؤول اللجنة',
        delegateName: widget.listName,
        itemsNames: '${row['itemsNames'] ?? row['ItemsNames'] ?? ''}',
        amountTotalSales: total,
        receiptsTotal: receipts + amount,
        amountRemaining: (remaining - amount).clamp(0, double.infinity),
        amountPush: amount,
        phoneNumberCompany:
            '${row['phoneNumberCompany'] ?? row['PhoneNumberCompany'] ?? ''}',
        lastSevenDays: ReceiptWeekAmounts.arabicDayLabels,
        lastSevenAmounts: week,
        countReceiptDevice: receiptNo,
        receiptNumberAlreadyPrinted: true,
      );
      if (mounted) {
        await report.printReceipt(context);
      }
      return true;
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(
            content:
                Text('تعذر الاتصال', style: TextStyle(fontFamily: 'Cairo'))));
      }
      return false;
    }
  }

  @override
  Widget build(BuildContext context) {
    if (_loading) {
      return const Center(child: CircularProgressIndicator());
    }
    final rows = _visible;
    return Column(
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(12, 12, 12, 8),
          child: TextField(
            controller: _search,
            onChanged: (v) => setState(() => _query = v),
            decoration: const InputDecoration(
              hintText: 'بحث عن زبون...',
              hintStyle: TextStyle(fontFamily: 'Cairo'),
              prefixIcon: Icon(Icons.search),
              border: OutlineInputBorder(),
            ),
            style: const TextStyle(fontFamily: 'Cairo'),
          ),
        ),
        if (_error.isNotEmpty)
          Padding(
            padding: const EdgeInsets.all(12),
            child: Text(_error,
                style: const TextStyle(fontFamily: 'Cairo', color: Colors.red)),
          ),
        Expanded(
          child: RefreshIndicator(
            onRefresh: reload,
            child: rows.isEmpty
                ? ListView(
                    children: const [
                      SizedBox(height: 80),
                      Center(
                          child: Text('لا يوجد زبائن',
                              style: TextStyle(
                                  fontFamily: 'Cairo', color: Colors.grey))),
                    ],
                  )
                : ListView.separated(
                    padding: const EdgeInsets.fromLTRB(12, 0, 12, 24),
                    itemCount: rows.length,
                    separatorBuilder: (_, __) => const NameListSeparator(),
                    itemBuilder: (_, i) {
                      final r = rows[i];
                      final name =
                          '${r['customerName'] ?? r['CustomerName'] ?? ''}';
                      final phone =
                          '${r['phoneNumber'] ?? r['PhoneNumber'] ?? ''}';
                      final rem =
                          _d(r['amountRemaining'] ?? r['AmountRemaining']);
                      final due =
                          _d(r['amountDaySales'] ?? r['AmountDaySales']);
                      final paid = r['paidToday'] == true ||
                          r['PaidToday'] == true;
                      return Padding(
                        padding: const EdgeInsets.symmetric(vertical: 4),
                        child: Column(
                            crossAxisAlignment: CrossAxisAlignment.stretch,
                            children: [
                              Row(
                                children: [
                                  Expanded(
                                    child: PersonNameText(name),
                                  ),
                                  if (paid)
                                    Container(
                                      padding: const EdgeInsets.symmetric(
                                          horizontal: 8, vertical: 2),
                                      decoration: BoxDecoration(
                                        color: Colors.green.shade50,
                                        borderRadius: BorderRadius.circular(12),
                                      ),
                                      child: Text('مسدد اليوم',
                                          style: TextStyle(
                                              fontFamily: 'Cairo',
                                              color: Colors.green.shade800,
                                              fontSize: 12)),
                                    ),
                                ],
                              ),
                              if (phone.isNotEmpty) ...[
                                const SizedBox(height: 4),
                                Text(phone,
                                    style: const TextStyle(
                                        fontFamily: 'Cairo',
                                        color: Colors.grey)),
                              ],
                              const SizedBox(height: 8),
                              Text(
                                'الباقي: ${Formatters.formatNumber(rem)} · القسط: ${Formatters.formatNumber(due)}',
                                style: const TextStyle(fontFamily: 'Cairo'),
                              ),
                              const SizedBox(height: 10),
                              Row(
                                children: [
                                  Expanded(
                                    child: ElevatedButton.icon(
                                      onPressed: paid ? null : () => _pay(r),
                                      icon:
                                          const Icon(Icons.payments, size: 18),
                                      label: const Text('تسديد',
                                          style: TextStyle(fontFamily: 'Cairo')),
                                      style: ElevatedButton.styleFrom(
                                        backgroundColor: AppTheme.primaryColor,
                                        foregroundColor: Colors.white,
                                      ),
                                    ),
                                  ),
                                  const SizedBox(width: 8),
                                  Expanded(
                                    child: ElevatedButton.icon(
                                      onPressed:
                                          paid ? () => _reprint(r) : null,
                                      icon: const Icon(Icons.print, size: 18),
                                      label: const Text('طباعة',
                                          style: TextStyle(fontFamily: 'Cairo')),
                                      style: ElevatedButton.styleFrom(
                                        backgroundColor: paid
                                            ? AppTheme.secondaryColor
                                            : Colors.grey.shade400,
                                        foregroundColor: Colors.white,
                                      ),
                                    ),
                                  ),
                                ],
                              ),
                            ],
                          ),
                      );
                    },
                  ),
          ),
        ),
      ],
    );
  }
}
