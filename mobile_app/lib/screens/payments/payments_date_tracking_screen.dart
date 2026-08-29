import 'package:flutter/material.dart';
import '../../services/api_service.dart';
import '../../models/customer.dart';
import '../../models/delegate.dart';
import '../../utils/formatters.dart';
import '../../widgets/modern_stat_card.dart';
import '../../widgets/states.dart';

class PaymentsDateTrackingScreen extends StatefulWidget {
  const PaymentsDateTrackingScreen({super.key});

  @override
  State<PaymentsDateTrackingScreen> createState() => _PaymentsDateTrackingScreenState();
}

class _PaymentsDateTrackingScreenState extends State<PaymentsDateTrackingScreen> {
  final ApiService _api = ApiService();
  List<CustomerModel> _data = [];
  List<DelegateModel> _delegates = [];
  bool _isLoading = true;
  int? _selectedDelegateID;
  String _paymentDate = Formatters.todayEnCA();
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
      final response = await _api.get('Customers/Customers_Follow/$delegate&&$_paymentDate&&$_showType');
      if (response.data is List) {
        setState(() {
          _data = (response.data as List).map((j) => CustomerModel.fromJson(j)).toList();
          _isLoading = false;
        });
      }
    } catch (e) {
      setState(() => _isLoading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final totalSales = _data.fold<double>(0, (s, c) => s + c.amountTotalSales);
    final totalDay = _data.fold<double>(0, (s, c) => s + c.amountDaySales);
    final totalReceipt = _data.fold<double>(0, (s, c) => s + c.receiptsTotal);
    final totalRemaining = _data.fold<double>(0, (s, c) => s + c.amountRemaining);

    return Scaffold(
      appBar: AppBar(title: const Text('متابعة حسب التاريخ')),
      body: _isLoading
          ? const LoadingIndicator()
          : RefreshIndicator(
              onRefresh: _load,
              child: ListView(
                padding: const EdgeInsets.all(12),
                children: [
                  // Filters
                  Row(
                    children: [
                      Expanded(
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
                      const SizedBox(width: 8),
                      Expanded(
                        child: ListTile(
                          title: const Text('التاريخ', style: TextStyle(fontSize: 12)),
                          subtitle: Text(_paymentDate),
                          trailing: const Icon(Icons.calendar_today, size: 20),
                          onTap: () async {
                            final date = await showDatePicker(
                              context: context,
                              initialDate: DateTime.now(),
                              firstDate: DateTime(2020),
                              lastDate: DateTime.now().add(const Duration(days: 365)),
                            );
                            if (date != null) {
                              setState(() => _paymentDate = Formatters.formatDate(date.toIso8601String().split('T')[0]));
                              _load();
                            }
                          },
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 8),
                  SizedBox(
                    height: 120,
                    child: ListView(
                      scrollDirection: Axis.horizontal,
                      children: [
                        SizedBox(width: 140, child: ModernStatCard(title: 'عدد العملاء', value: '${_data.length}', icon: Icons.people_rounded, color: 'primary')),
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
                  if (_data.isEmpty)
                    const EmptyState(message: 'لا توجد بيانات')
                  else
                    ..._data.map((c) => Card(
                          margin: const EdgeInsets.only(bottom: 8),
                          child: ListTile(
                            title: Text(c.customerName, style: const TextStyle(fontWeight: FontWeight.w600)),
                            subtitle: Text('${c.delegateName} | ${c.itemsNames}', style: const TextStyle(fontSize: 12)),
                            trailing: Column(
                              mainAxisSize: MainAxisSize.min,
                              children: [
                                Text('القسط: ${Formatters.formatCurrency(c.amountDaySales)}', style: TextStyle(fontSize: 11)),
                                Text('الباقي: ${Formatters.formatCurrency(c.amountRemaining)}', style: TextStyle(fontSize: 11, color: Colors.red[700])),
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
