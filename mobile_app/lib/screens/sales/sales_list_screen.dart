import 'package:flutter/material.dart';
import '../../services/api_service.dart';
import '../../utils/formatters.dart';
import '../../widgets/modern_stat_card.dart';
import '../../widgets/states.dart';
import '../../utils/excel_export.dart';

class SalesListScreen extends StatefulWidget {
  const SalesListScreen({super.key});

  @override
  State<SalesListScreen> createState() => _SalesListScreenState();
}

class _SalesListScreenState extends State<SalesListScreen> {
  final ApiService _api = ApiService();
  List<Map<String, dynamic>> _sales = [];
  List<Map<String, dynamic>> _delegates = [];
  List<Map<String, dynamic>> _stores = [];
  List<Map<String, dynamic>> _items = [];
  bool _isLoading = true;
  DateTime _fromDate = DateTime.now().subtract(const Duration(days: 30));
  DateTime _toDate = DateTime.now().add(const Duration(days: 1));
  int? _selectedDelegateID;
  String _customerName = '';
  String _itemName = '';
  String _saleName = '';

  // Calculated totals
  double _totalSales = 0, _totalCost = 0, _totalPrice = 0, _totalFinalPrice = 0;
  double _totalInstallment = 0, _totalFinalInstallment = 0, _totalReceived = 0, _totalRemaining = 0;
  int _salesCount = 0;

  @override
  void initState() {
    super.initState();
    _loadAll();
  }

  Future<void> _loadAll() async {
    await Future.wait([_loadDelegates(), _loadStores()]);
    await _loadSales();
  }

  Future<void> _loadDelegates() async {
    try {
      final res = await _api.get('Delegates/Delegates_GetDataAll');
      if (res.data is List) {
        _delegates = (res.data as List).cast<Map<String, dynamic>>();
      }
    } catch (_) {}
  }

  Future<void> _loadStores() async {
    try {
      final res = await _api.get('Stores/StoresData_GetAll');
      if (res.data is List) {
        _stores = (res.data as List).cast<Map<String, dynamic>>();
      }
    } catch (_) {}
  }

  Future<void> _loadItems(int storeID) async {
    try {
      final res = await _api.get('Items/Items_GetByItemSale/$storeID');
      if (res.data is List) {
        _items = (res.data as List).cast<Map<String, dynamic>>();
      }
    } catch (_) {}
  }

  Future<void> _loadSales() async {
    setState(() => _isLoading = true);
    try {
      final from = Formatters.formatDate(_fromDate.toIso8601String().split('T')[0]);
      final to = Formatters.formatDate(_toDate.toIso8601String().split('T')[0]);
      final delegateParam = _selectedDelegateID?.toString() ?? 'null';
      final cName = _customerName.isEmpty ? 'null' : _customerName;
      final iName = _itemName.isEmpty ? 'null' : _itemName;
      final sName = _saleName.isEmpty ? 'null' : _saleName;

      final res = await _api.get(
        'CustomersSales/CustomersSales_GetAll/$from&&$to&&$delegateParam&&$cName&&$iName&&$sName',
      );
      if (res.data is List) {
        setState(() {
          _sales = (res.data as List).cast<Map<String, dynamic>>();
          _calcTotals();
          _isLoading = false;
        });
      } else {
        setState(() => _isLoading = false);
      }
    } catch (e) {
      setState(() => _isLoading = false);
    }
  }

  void _calcTotals() {
    _salesCount = _sales.length;
    _totalSales = _sales.fold(0, (s, i) => s + (i['amountTotalSalesDenar'] ?? 0).toDouble());
    _totalCost = _sales.fold(0, (s, i) => s + (i['amountTotalCostDenar'] ?? 0).toDouble());
    _totalPrice = _sales.fold(0, (s, i) => s + (i['amountTotalDenar'] ?? 0).toDouble());
    _totalFinalPrice = _sales.fold(0, (s, i) => s + (i['amountTotalSalesDenar'] ?? 0).toDouble());
    _totalInstallment = _sales.fold(0, (s, i) => s + (i['amountTotalDayDenar'] ?? 0).toDouble());
    _totalFinalInstallment = _sales.fold(0, (s, i) => s + (i['amountDaySalesDenar'] ?? 0).toDouble());
    _totalReceived = _sales.fold(0, (s, i) => s + (i['receiptsTotal'] ?? 0).toDouble());
    _totalRemaining = _sales.fold(0, (s, i) => s + (i['amountRemaining'] ?? 0).toDouble());
  }

  Future<void> _pickDateRange() async {
    final range = await showDateRangePicker(
      context: context,
      firstDate: DateTime(2020),
      lastDate: DateTime.now().add(const Duration(days: 365)),
      initialDateRange: DateTimeRange(start: _fromDate, end: _toDate),
    );
    if (range != null) {
      _fromDate = range.start;
      _toDate = range.end;
      _loadSales();
    }
  }

  Color _statusColor(String status) {
    switch (status) {
      case 'مصفر': return Colors.green;
      case 'قانونية': return Colors.orange;
      case 'متوقف': return Colors.red;
      default: return Colors.blue;
    }
  }

  String _getStatus(Map<String, dynamic> sale) {
    if ((sale['amountRemaining'] ?? 0).toDouble() == 0) return 'مصفر';
    if ((sale['isLegal'] ?? false) == true) return 'قانونية';
    if (sale['lastPaymentDate'] != null && sale['lastPaymentDate'].toString().isNotEmpty) {
      try {
        final last = DateTime.parse(sale['lastPaymentDate']);
        if (DateTime.now().difference(last).inDays > 365) return 'متوقف';
      } catch (_) {}
    }
    return 'عادي';
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('المبيعات', style: TextStyle()),
        actions: [
          IconButton(icon: const Icon(Icons.table_chart_outlined), tooltip: 'تصدير Excel', onPressed: _exportSales),
          IconButton(icon: const Icon(Icons.date_range), onPressed: _pickDateRange),
          IconButton(icon: const Icon(Icons.filter_alt), onPressed: _showFilters),
          IconButton(icon: const Icon(Icons.refresh), onPressed: _loadSales),
        ],
      ),
      body: _isLoading
          ? const LoadingIndicator()
          : Column(
              children: [
                _buildStatsRow(),
                Expanded(child: _sales.isEmpty ? const EmptyState(message: 'لا توجد مبيعات') : _buildSalesList()),
              ],
            ),
    );
  }

  Widget _buildStatsRow() {
    return SizedBox(
      height: 100,
      child: ListView(
        scrollDirection: Axis.horizontal,
        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 8),
        children: [
          ModernStatCard.compact(title: 'عدد البيعات', value: Formatters.formatNumber(_salesCount), icon: Icons.shopping_cart, color: 'primary'),
          ModernStatCard.compact(title: 'السعر النهائي', value: Formatters.formatCurrency(_totalFinalPrice), icon: Icons.monetization_on, color: 'success'),
          ModernStatCard.compact(title: 'التقسيط النهائي', value: Formatters.formatCurrency(_totalFinalInstallment), icon: Icons.payments, color: 'warning'),
          ModernStatCard.compact(title: 'المستلم', value: Formatters.formatCurrency(_totalReceived), icon: Icons.download_done, color: 'info'),
          ModernStatCard.compact(title: 'المتبقي', value: Formatters.formatCurrency(_totalRemaining), icon: Icons.pending, color: 'error'),
        ],
      ),
    );
  }

  Widget _buildSalesList() {
    return ListView.builder(
      padding: const EdgeInsets.all(8),
      itemCount: _sales.length,
      itemBuilder: (context, index) {
        final s = _sales[index];
        final status = _getStatus(s);
        return Card(
          margin: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
          child: ExpansionTile(
            leading: CircleAvatar(backgroundColor: _statusColor(status).withOpacity(0.2), child: Icon(Icons.receipt_long, color: _statusColor(status))),
            title: Text(s['customerName'] ?? '', style: const TextStyle(fontWeight: FontWeight.bold)),
            subtitle: Text('${Formatters.formatCurrency(s['amountTotalSalesDenar'])} | ${Formatters.formatDate(s['dateCreate']?.toString())}', style: const TextStyle(fontSize: 12)),
            trailing: Chip(label: Text(status, style: const TextStyle(fontSize: 10, color: Colors.white)), backgroundColor: _statusColor(status), padding: EdgeInsets.zero),
            children: [
              Padding(
                padding: const EdgeInsets.all(16),
                child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                  _detailRow('رقم السند', s['boundNumber']?.toString()),
                  _detailRow('المندوب', s['delegateName']),
                  _detailRow('المخزن', s['storeName']),
                  _detailRow('الأصناف', s['itemsNames']),
                  _detailRow('العدد', s['numberOfItemsSales']?.toString()),
                  _detailRow('السعر الكلي', Formatters.formatCurrency(s['amountTotalDenar'])),
                  _detailRow('بعد الخصم', Formatters.formatCurrency(s['amountTotalSalesDenar'])),
                  _detailRow('التقسيط اليومي', Formatters.formatCurrency(s['amountTotalDayDenar'])),
                  _detailRow('بعد خصم التقسيط', Formatters.formatCurrency(s['amountDaySalesDenar'])),
                  _detailRow('المستلم', Formatters.formatCurrency(s['receiptsTotal'])),
                  _detailRow('المتبقي', Formatters.formatCurrency(s['amountRemaining'])),
                ]),
              ),
            ],
          ),
        );
      },
    );
  }

  Widget _detailRow(String label, String? value) => Padding(
    padding: const EdgeInsets.symmetric(vertical: 3),
    child: Row(children: [
      Expanded(child: Text(label, style: const TextStyle(color: Colors.grey, fontSize: 13))),
      Text(value ?? '-', style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 13)),
    ]),
  );

  Future<void> _exportSales() async {
    try {
      await ExcelExport.exportSales(_sales, fileName: 'المبيعات_${Formatters.todayEnCA()}');
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('تم التصدير', style: TextStyle())));
    } catch (_) {}
  }

  void _showFilters() {
    showModalBottomSheet(
      context: context,
      isScrollControlled: true,
      shape: const RoundedRectangleBorder(borderRadius: BorderRadius.vertical(top: Radius.circular(20))),
      builder: (ctx) => StatefulBuilder(builder: (ctx, setModalState) {
        return Padding(
          padding: const EdgeInsets.all(20),
          child: Column(mainAxisSize: MainAxisSize.min, children: [
            const Text('تصفية', style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold)),
            const SizedBox(height: 12),
            DropdownButtonFormField<int?>(
              value: _selectedDelegateID,
              decoration: const InputDecoration(labelText: 'المندوب'),
              items: [{null: 'الجميع'}, ..._delegates.map((d) => {d['delegateID'] as int?: d['delegateName']})].expand((e) => e.entries.map((kv) => DropdownMenuItem(value: kv.key, child: Text(kv.value.toString() ?? '', style: const TextStyle())))).toList(),
              onChanged: (v) => setModalState(() => _selectedDelegateID = v),
            ),
            const SizedBox(height: 12),
            TextField(decoration: const InputDecoration(labelText: 'اسم الزبون'), onChanged: (v) => _customerName = v),
            const SizedBox(height: 12),
            TextField(decoration: const InputDecoration(labelText: 'اسم المادة'), onChanged: (v) => _itemName = v),
            const SizedBox(height: 12),
            TextField(decoration: const InputDecoration(labelText: 'اسم البيع'), onChanged: (v) => _saleName = v),
            const SizedBox(height: 20),
            ElevatedButton.icon(
              onPressed: () { Navigator.pop(ctx); _loadSales(); },
              icon: const Icon(Icons.search),
              label: const Text('بحث', style: TextStyle()),
            ),
          ]),
        );
      }),
    );
  }
}
