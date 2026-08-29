import 'package:flutter/material.dart';
import '../../services/api_service.dart';
import '../../models/customer.dart';
import '../../utils/formatters.dart';
import '../../widgets/modern_stat_card.dart';
import '../../widgets/states.dart';

class CustomersArchivedScreen extends StatefulWidget {
  const CustomersArchivedScreen({super.key});

  @override
  State<CustomersArchivedScreen> createState() => _CustomersArchivedScreenState();
}

class _CustomersArchivedScreenState extends State<CustomersArchivedScreen> {
  final ApiService _api = ApiService();
  List<CustomerModel> _customers = [];
  bool _isLoading = true;
  DateTime _fromDate = DateTime.now().subtract(const Duration(days: 365));
  DateTime _toDate = DateTime.now();

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() => _isLoading = true);
    try {
      final from = Formatters.formatDate(_fromDate.toIso8601String().split('T')[0]);
      final to = Formatters.formatDate(_toDate.toIso8601String().split('T')[0]);
      final response = await _api.get('Customers/Customers_GetAllZero/$from&&$to');
      if (response.data is List) {
        setState(() {
          _customers = (response.data as List).map((j) => CustomerModel.fromJson(j)).toList();
          _isLoading = false;
        });
      }
    } catch (e) {
      setState(() => _isLoading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final totalSales = _customers.fold<double>(0, (s, c) => s + c.amountTotalSales);
    final totalDay = _customers.fold<double>(0, (s, c) => s + c.amountDaySales);
    final totalReceipt = _customers.fold<double>(0, (s, c) => s + c.receiptsTotal);
    final totalRemaining = _customers.fold<double>(0, (s, c) => s + c.amountRemaining);

    return Scaffold(
      appBar: AppBar(title: const Text('أرشيف المصفرين')),
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
                        SizedBox(width: 140, child: ModernStatCard(title: 'عدد المصفرين', value: '${_customers.length}', icon: Icons.archive_rounded, color: 'primary')),
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
                  if (_customers.isEmpty)
                    const EmptyState(message: 'لا يوجد عملاء مصفرين')
                  else
                    ..._customers.map((c) => Card(
                          margin: const EdgeInsets.only(bottom: 8),
                          child: ListTile(
                            title: Text(c.customerName, style: const TextStyle(fontWeight: FontWeight.w600)),
                            subtitle: Text('${c.delegateName} | ${c.itemsNames}', style: const TextStyle(fontSize: 12)),
                            trailing: Text(Formatters.formatCurrency(c.amountRemaining), style: TextStyle(color: Colors.red[700])),
                          ),
                        )),
                ],
              ),
            ),
    );
  }
}
