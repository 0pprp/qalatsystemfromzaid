import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../config/theme.dart';
import '../../providers/auth_provider.dart';
import '../../services/api_service.dart';
import '../../models/customer.dart';
import '../../models/delegate.dart';
import '../../models/payment.dart';
import '../../utils/formatters.dart';
import '../../widgets/modern_stat_card.dart';
import '../../widgets/states.dart';
import '../../utils/excel_export.dart';

class CustomersListScreen extends StatefulWidget {
  const CustomersListScreen({super.key});

  @override
  State<CustomersListScreen> createState() => _CustomersListScreenState();
}

class _CustomersListScreenState extends State<CustomersListScreen> {
  final ApiService _api = ApiService();
  List<CustomerModel> _customers = [];
  List<DelegateModel> _delegates = [];
  bool _isLoading = true;
  int? _selectedDelegateID;
  String _searchText = '';
  String _showType = 'الجميع';

  // Payment dialog
  final _paymentAmountCtrl = TextEditingController();
  String _paymentDate = Formatters.todayEnCA();
  CustomerModel? _selectedCustomer;

  @override
  void initState() {
    super.initState();
    _loadDelegates();
    _loadCustomers();
  }

  Future<void> _loadDelegates() async {
    try {
      final response = await _api.get('Delegates/Delegates_GetDataAll');
      if (response.data is List) {
        setState(() => _delegates = (response.data as List).map((j) => DelegateModel.fromJson(j)).toList());
      }
    } catch (_) {}
  }

  Future<void> _export() async {
    try {
      await ExcelExport.exportCustomers(_customers.map((c) => {
        'customerName': c.customerName, 'delegateName': c.delegateName,
        'phoneNumber': c.phoneNumber, 'amountTotalSales': c.amountTotalSales,
        'amountDaySales': c.amountDaySales, 'receiptsTotal': c.receiptsTotal,
        'amountRemaining': c.amountRemaining, 'lastPaymentDate': c.lastPaymentDate,
      }).toList(), fileName: 'العملاء_${Formatters.todayEnCA()}');
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('تم التصدير', style: TextStyle())));
    } catch (_) {}
  }

  Future<void> _loadCustomers() async {
    setState(() => _isLoading = true);
    try {
      final delegate = _selectedDelegateID ?? 'null';
      final search = _searchText.isEmpty ? 'null' : _searchText;
      final response = await _api.get('Customers/Customers_GetAll/$delegate&&$search&&$_showType');
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

  Future<void> _submitPayment() async {
    if (_selectedCustomer == null) return;
    final amount = double.tryParse(_paymentAmountCtrl.text) ?? 0;
    if (amount < 1000) {
      _showSnack('المبلغ يجب أن لا يقل عن 1000 د.ع', isError: true);
      return;
    }
    if (amount > _selectedCustomer!.amountRemaining) {
      _showSnack('المبلغ أكبر من المتبقي', isError: true);
      return;
    }

    try {
      await _api.post('Customers/AddCustomersPayment', data: {
        'customerID': _selectedCustomer!.customerID,
        'paymentAmount': amount,
        'paymentDate': _paymentDate,
      });
      if (mounted) {
        Navigator.pop(context);
        _showSnack('تم التسديد بنجاح');
        _loadCustomers();
      }
    } catch (e) {
      _showSnack('فشل التسديد', isError: true);
    }
  }

  void _showPaymentDialog(CustomerModel customer) {
    _selectedCustomer = customer;
    _paymentAmountCtrl.text = Formatters.formatNumber(customer.amountDaySales);
    _paymentDate = Formatters.todayEnCA();
    showModalBottomSheet(
      context: context,
      isScrollControlled: true,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(24)),
      ),
      builder: (_) => Padding(
        padding: EdgeInsets.only(
          left: 20,
          right: 20,
          top: 24,
          bottom: MediaQuery.of(context).viewInsets.bottom + 20,
        ),
        child: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Row(
                children: [
                  const Text('تسديد عميل', style: TextStyle(fontSize: 20, fontWeight: FontWeight.bold)),
                  const Spacer(),
                  IconButton(onPressed: () => Navigator.pop(context), icon: const Icon(Icons.close)),
                ],
              ),
              const SizedBox(height: 12),
              _infoRow('العميل', customer.customerName),
              _infoRow('المندوب', customer.delegateName),
              _infoRow('الباقي', Formatters.formatCurrency(customer.amountRemaining)),
              _infoRow('القسط', Formatters.formatCurrency(customer.amountDaySales)),
              const SizedBox(height: 16),
              TextFormField(
                controller: _paymentAmountCtrl,
                keyboardType: TextInputType.number,
                decoration: const InputDecoration(labelText: 'مبلغ التسديد', hintText: 'المبلغ بالدينار'),
              ),
              const SizedBox(height: 12),
              ListTile(
                title: const Text('تاريخ التسديد', style: TextStyle()),
                subtitle: Text(_paymentDate, style: const TextStyle()),
                trailing: const Icon(Icons.calendar_today),
                onTap: () async {
                  final date = await showDatePicker(
                    context: context,
                    initialDate: DateTime.now(),
                    firstDate: DateTime(2020),
                    lastDate: DateTime.now(),
                  );
                  if (date != null) setState(() => _paymentDate = Formatters.formatDate(date.toIso8601String().split('T')[0]));
                },
              ),
              const SizedBox(height: 20),
              ElevatedButton(
                onPressed: _submitPayment,
                style: ElevatedButton.styleFrom(
                  padding: const EdgeInsets.symmetric(vertical: 14),
                  backgroundColor: AppTheme.successColor,
                ),
                child: const Text('تأكيد التسديد', style: TextStyle(fontSize: 16)),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _infoRow(String label, String value) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 4),
      child: Row(
        children: [
          Text('$label: ', style: const TextStyle(fontWeight: FontWeight.w600, color: Colors.grey)),
          Text(value, style: const TextStyle()),
        ],
      ),
    );
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
    // Totals
    final totalCustomers = _customers.length;
    final totalSales = _customers.fold<double>(0, (s, c) => s + c.amountTotalSales);
    final totalDay = _customers.fold<double>(0, (s, c) => s + c.amountDaySales);
    final totalReceipt = _customers.fold<double>(0, (s, c) => s + c.receiptsTotal);
    final totalRemaining = _customers.fold<double>(0, (s, c) => s + c.amountRemaining);

    return Scaffold(
      appBar: AppBar(
        title: const Text('جميع العملاء'),
        actions: [
          IconButton(icon: const Icon(Icons.table_chart_outlined), tooltip: 'تصدير Excel', onPressed: _export),
          IconButton(icon: const Icon(Icons.refresh), onPressed: _loadCustomers),
          PopupMenuButton<String>(
            onSelected: (v) {
              _showType = v;
              _loadCustomers();
            },
            itemBuilder: (_) => ['الجميع', 'المسددين', 'المتوقفين'].map((t) => PopupMenuItem(value: t, child: Text(t, style: const TextStyle()))).toList(),
          ),
        ],
      ),
      body: Column(
        children: [
          // Filters
          Padding(
            padding: const EdgeInsets.all(12),
            child: Row(
              children: [
                Expanded(
                  flex: 2,
                  child: DropdownButtonFormField<int?>(
                    value: _selectedDelegateID,
                    decoration: const InputDecoration(labelText: 'المندوب', contentPadding: EdgeInsets.symmetric(horizontal: 12)),
                    items: [
                      const DropdownMenuItem(value: null, child: Text('الجميع', style: TextStyle())),
                      ..._delegates.map((d) => DropdownMenuItem(
                            value: d.delegateID,
                            child: Text(d.delegateName, style: const TextStyle()),
                          )),
                    ],
                    onChanged: (v) {
                      _selectedDelegateID = v;
                      _loadCustomers();
                    },
                  ),
                ),
                const SizedBox(width: 8),
                Expanded(
                  flex: 3,
                  child: TextFormField(
                    decoration: const InputDecoration(
                      labelText: 'بحث',
                      hintText: 'اسم العميل',
                      prefixIcon: Icon(Icons.search, size: 20),
                      contentPadding: EdgeInsets.symmetric(horizontal: 12, vertical: 14),
                    ),
                    onChanged: (v) {
                      _searchText = v;
                      Future.delayed(const Duration(milliseconds: 500), () {
                        if (_searchText == v) _loadCustomers();
                      });
                    },
                  ),
                ),
              ],
            ),
          ),

          // Totals cards
          SizedBox(
            height: 120,
            child: ListView(
              scrollDirection: Axis.horizontal,
              padding: const EdgeInsets.symmetric(horizontal: 12),
              children: [
                SizedBox(
                  width: 140,
                  child: ModernStatCard(title: 'عدد العملاء', value: '$totalCustomers', icon: Icons.people_rounded, color: 'primary'),
                ),
                const SizedBox(width: 8),
                SizedBox(
                  width: 140,
                  child: ModernStatCard(title: 'سعر البيع', value: Formatters.formatCurrency(totalSales), icon: Icons.monetization_on_rounded, color: 'success'),
                ),
                const SizedBox(width: 8),
                SizedBox(
                  width: 140,
                  child: ModernStatCard(title: 'القسط', value: Formatters.formatCurrency(totalDay), icon: Icons.calendar_month_rounded, color: 'info'),
                ),
                const SizedBox(width: 8),
                SizedBox(
                  width: 140,
                  child: ModernStatCard(title: 'الواصل', value: Formatters.formatCurrency(totalReceipt), icon: Icons.credit_card_rounded, color: 'success'),
                ),
                const SizedBox(width: 8),
                SizedBox(
                  width: 140,
                  child: ModernStatCard(title: 'الباقي', value: Formatters.formatCurrency(totalRemaining), icon: Icons.money_off_rounded, color: 'error'),
                ),
              ],
            ),
          ),

          const SizedBox(height: 8),

          // Customers list
          Expanded(
            child: _isLoading
                ? const LoadingIndicator()
                : _customers.isEmpty
                    ? const EmptyState(message: 'لا يوجد عملاء')
                    : RefreshIndicator(
                        onRefresh: _loadCustomers,
                        child: ListView.builder(
                          padding: const EdgeInsets.symmetric(horizontal: 12),
                          itemCount: _customers.length,
                          itemBuilder: (context, index) {
                            final c = _customers[index];
                            return Card(
                              margin: const EdgeInsets.only(bottom: 8),
                              child: ListTile(
                                leading: CircleAvatar(
                                  backgroundColor: AppTheme.primaryColor,
                                  child: Text(c.customerName.isNotEmpty ? c.customerName[0] : '?', style: const TextStyle(color: Colors.white)),
                                ),
                                title: Text(c.customerName, style: const TextStyle(fontWeight: FontWeight.w600)),
                                subtitle: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    Text('${c.delegateName} | ${c.phoneNumber}', style: const TextStyle(fontSize: 11)),
                                    Row(
                                      children: [
                                        Text('الواصل: ${Formatters.formatCurrency(c.receiptsTotal)}', style: TextStyle(fontSize: 11, color: Colors.green[700])),
                                        const SizedBox(width: 8),
                                        Text('الباقي: ${Formatters.formatCurrency(c.amountRemaining)}', style: TextStyle(fontSize: 11, color: Colors.red[700])),
                                      ],
                                    ),
                                  ],
                                ),
                                trailing: IconButton(
                                  icon: const Icon(Icons.payment_rounded, color: AppTheme.successColor),
                                  onPressed: () => _showPaymentDialog(c),
                                ),
                              ),
                            );
                          },
                        ),
                      ),
          ),
        ],
      ),
    );
  }
}
