import 'package:flutter/material.dart';
import '../../services/api_service.dart';
import '../../models/payment.dart';
import '../../utils/formatters.dart';
import '../../widgets/modern_stat_card.dart';
import '../../widgets/states.dart';
import '../../utils/excel_export.dart';

class PaymentsTotalScreen extends StatefulWidget {
  const PaymentsTotalScreen({super.key});

  @override
  State<PaymentsTotalScreen> createState() => _PaymentsTotalScreenState();
}

class _PaymentsTotalScreenState extends State<PaymentsTotalScreen> {
  final ApiService _api = ApiService();
  List<PaymentModel> _payments = [];
  bool _isLoading = true;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() => _isLoading = true);
    try {
      final response = await _api.get('Customers/CustomersPayments_GetAll');
      if (response.data is List) {
        setState(() {
          _payments = (response.data as List).map((j) => PaymentModel.fromJson(j)).toList();
          _isLoading = false;
        });
      }
    } catch (e) {
      setState(() => _isLoading = false);
    }
  }

  Future<void> _export() async {
    try {
      await ExcelExport.exportPayments(_payments.map((p) => {
        'customerName': p.customerName, 'delegateName': p.delegateName,
        'amountDenar': p.amountDenar, 'paymentDate': p.paymentDate,
      }).toList(), fileName: 'التسديدات_${Formatters.todayEnCA()}');
      if (mounted) _showSnack('تم التصدير بنجاح');
    } catch (_) { _showSnack('فشل التصدير', isError: true); }
  }

  Future<void> _deletePayment(int id) async {
    try {
      await _api.delete('Customers/CustomersPayments_Delete/$id');
      _showSnack('تم الحذف بنجاح');
      _load();
    } catch (_) {
      _showSnack('فشل الحذف', isError: true);
    }
  }

  void _showSnack(String msg, {bool isError = false}) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text(msg, style: const TextStyle()),
        backgroundColor: isError ? Colors.red : Colors.green,
        behavior: SnackBarBehavior.floating,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final totalAmount = _payments.fold<double>(0, (s, p) => s + p.amountDenar);

    return Scaffold(
      appBar: AppBar(
        title: const Text('التسديدات الكلية'),
        actions: [
          IconButton(icon: const Icon(Icons.table_chart_outlined), tooltip: 'تصدير Excel', onPressed: _export),
          IconButton(icon: const Icon(Icons.refresh), onPressed: _load),
        ],
      ),
      body: _isLoading
          ? const LoadingIndicator()
          : RefreshIndicator(
              onRefresh: _load,
              child: ListView(
                padding: const EdgeInsets.all(12),
                children: [
                  SizedBox(
                    height: 120,
                    child: ListView(
                      scrollDirection: Axis.horizontal,
                      children: [
                        SizedBox(width: 140, child: ModernStatCard(title: 'عدد التسديدات', value: '${_payments.length}', icon: Icons.receipt_long_rounded, color: 'primary')),
                        const SizedBox(width: 8),
                        SizedBox(width: 160, child: ModernStatCard(title: 'مجموع التسديدات', value: Formatters.formatCurrency(totalAmount), icon: Icons.monetization_on_rounded, color: 'success')),
                      ],
                    ),
                  ),
                  const SizedBox(height: 12),
                  if (_payments.isEmpty)
                    const EmptyState(message: 'لا توجد تسديدات')
                  else
                    ..._payments.map((p) => Card(
                          margin: const EdgeInsets.only(bottom: 8),
                          child: ListTile(
                            title: Text(p.customerName, style: const TextStyle(fontWeight: FontWeight.w600)),
                            subtitle: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Text('${p.delegateName} | ${Formatters.formatDate(p.paymentDate)}', style: const TextStyle(fontSize: 12)),
                                Text('الواصل: ${Formatters.formatCurrency(p.receiptsTotal)} | الباقي: ${Formatters.formatCurrency(p.amountRemaining)}', style: TextStyle(fontSize: 11, color: Colors.grey[600])),
                              ],
                            ),
                            trailing: Row(
                              mainAxisSize: MainAxisSize.min,
                              children: [
                                Text(Formatters.formatCurrency(p.amountDenar), style: TextStyle(fontWeight: FontWeight.bold, color: Colors.green[700], fontSize: 15)),
                                const SizedBox(width: 4),
                                IconButton(
                                  icon: const Icon(Icons.delete_outline, color: Colors.red, size: 20),
                                  onPressed: () => _deletePayment(p.customerPaymentID),
                                ),
                              ],
                            ),
                          ),
                        )),
                ],
              ),
            ),
    );
  }
}
