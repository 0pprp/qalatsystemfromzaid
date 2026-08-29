import 'package:flutter/material.dart';
import '../../services/api_service.dart';
import '../../models/payment.dart';
import '../../models/delegate.dart';
import '../../utils/formatters.dart';
import '../../widgets/modern_stat_card.dart';
import '../../widgets/states.dart';

class PaymentsMonthScreen extends StatefulWidget {
  const PaymentsMonthScreen({super.key});

  @override
  State<PaymentsMonthScreen> createState() => _PaymentsMonthScreenState();
}

class _PaymentsMonthScreenState extends State<PaymentsMonthScreen> {
  final ApiService _api = ApiService();
  List<WeekPaymentModel> _payments = [];
  List<DelegateModel> _delegates = [];
  bool _isLoading = true;
  int? _selectedDelegateID;
  String _showType = 'الجميع';

  @override
  void initState() {
    super.initState();
    _loadDelegates();
    _load();
  }

  Future<void> _loadDelegates() async {
    try {
      final response = await _api.get('Delegates/Delegates_GetDataAll');
      if (response.data is List) {
        setState(() => _delegates = (response.data as List).map((j) => DelegateModel.fromJson(j)).toList());
      }
    } catch (_) {}
  }

  Future<void> _load() async {
    setState(() => _isLoading = true);
    try {
      final delegate = _selectedDelegateID ?? 'null';
      final response = await _api.get('Customers/Customers_GetMonthReceipt/$delegate&&$_showType');
      if (response.data is List) {
        setState(() {
          _payments = (response.data as List).map((j) => WeekPaymentModel.fromJson(j, days: 30)).toList();
          _isLoading = false;
        });
      }
    } catch (e) {
      setState(() => _isLoading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final totalSales = _payments.fold<double>(0, (s, p) => s + p.amountTotalSales);
    final totalDay = _payments.fold<double>(0, (s, p) => s + p.amountDaySales);
    final totalReceipt = _payments.fold<double>(0, (s, p) => s + p.receiptsTotal);
    final totalRemaining = _payments.fold<double>(0, (s, p) => s + p.amountRemaining);

    return Scaffold(
      appBar: AppBar(title: const Text('تسديدات الشهر')),
      body: _isLoading
          ? const LoadingIndicator()
          : RefreshIndicator(
              onRefresh: _load,
              child: ListView(
                padding: const EdgeInsets.all(12),
                children: [
                  Padding(
                    padding: const EdgeInsets.only(bottom: 8),
                    child: DropdownButtonFormField<int?>(
                      value: _selectedDelegateID,
                      decoration: const InputDecoration(labelText: 'المندوب', contentPadding: EdgeInsets.symmetric(horizontal: 12)),
                      items: [
                        const DropdownMenuItem(value: null, child: Text('الجميع', style: TextStyle())),
                        ..._delegates.map((d) => DropdownMenuItem(value: d.delegateID, child: Text(d.delegateName, style: const TextStyle()))),
                      ],
                      onChanged: (v) {
                        _selectedDelegateID = v;
                        _load();
                      },
                    ),
                  ),
                  SizedBox(
                    height: 120,
                    child: ListView(
                      scrollDirection: Axis.horizontal,
                      children: [
                        SizedBox(width: 140, child: ModernStatCard(title: 'عدد العملاء', value: '${_payments.length}', icon: Icons.people_rounded, color: 'primary')),
                        const SizedBox(width: 8),
                        SizedBox(width: 140, child: ModernStatCard(title: 'سعر البيع', value: Formatters.formatCurrency(totalSales), icon: Icons.monetization_on_rounded, color: 'success')),
                        const SizedBox(width: 8),
                        SizedBox(width: 140, child: ModernStatCard(title: 'القسط', value: Formatters.formatCurrency(totalDay), icon: Icons.calendar_month_rounded, color: 'info')),
                        const SizedBox(width: 8),
                        SizedBox(width: 140, child: ModernStatCard(title: 'الواصل', value: Formatters.formatCurrency(totalReceipt), icon: Icons.credit_card_rounded, color: 'success')),
                        const SizedBox(width: 8),
                        SizedBox(width: 140, child: ModernStatCard(title: 'الباقي', value: Formatters.formatCurrency(totalRemaining), icon: Icons.money_off_rounded, color: 'error')),
                      ],
                    ),
                  ),
                  const SizedBox(height: 12),
                  if (_payments.isEmpty)
                    const EmptyState(message: 'لا توجد بيانات')
                  else
                    ..._payments.map((p) => Card(
                          margin: const EdgeInsets.only(bottom: 8),
                          child: ListTile(
                            title: Text(p.customerName, style: const TextStyle(fontWeight: FontWeight.w600)),
                            subtitle: Text('${p.delegateName} | ${p.itemsNames}', style: const TextStyle(fontSize: 12)),
                            trailing: Column(
                              mainAxisSize: MainAxisSize.min,
                              children: [
                                Text('القسط: ${Formatters.formatCurrency(p.amountDaySales)}', style: TextStyle(fontSize: 11, color: Colors.blue[700])),
                                Text('الباقي: ${Formatters.formatCurrency(p.amountRemaining)}', style: TextStyle(fontSize: 11, color: Colors.red[700])),
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
