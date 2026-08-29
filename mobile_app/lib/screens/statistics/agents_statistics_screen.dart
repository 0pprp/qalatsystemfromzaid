import 'package:flutter/material.dart';
import '../../services/api_service.dart';
import '../../models/statistics.dart';
import '../../utils/formatters.dart';
import '../../widgets/dashboard_header.dart';
import '../../widgets/modern_stat_card.dart';
import '../../widgets/states.dart';
import '../../utils/excel_export.dart';

class AgentsStatisticsScreen extends StatefulWidget {
  final bool excludeZeroed;
  const AgentsStatisticsScreen({super.key, this.excludeZeroed = false});

  @override
  State<AgentsStatisticsScreen> createState() => _AgentsStatisticsScreenState();
}

class _AgentsStatisticsScreenState extends State<AgentsStatisticsScreen> {
  final ApiService _api = ApiService();
  List<DelegateStatisticsModel> _stats = [];
  bool _isLoading = true;
  DateTime _fromDate = DateTime.now().subtract(const Duration(days: 30));
  DateTime _toDate = DateTime.now().add(const Duration(days: 1));

  // Totals
  int _totalCustomers = 0; double _totalPrice = 0, _totalCost = 0, _totalDay = 0; int _totalItems = 0; double _totalReceipts = 0;
  int _totalZeroedCustomers = 0; double _totalZeroedPrice = 0, _totalZeroedDay = 0;

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
      final endpoint = widget.excludeZeroed ? 'Delegates/Delegates_NoStatistics/$from&&$to' : 'Delegates/Delegates_Statistics/$from&&$to';
      final res = await _api.get(endpoint);
      if (res.data is List) {
        setState(() {
          _stats = (res.data as List).map((j) => DelegateStatisticsModel.fromJson(j)).toList();
          _calcTotals();
          _isLoading = false;
        });
      } else {
        setState(() => _isLoading = false);
      }
    } catch (_) {
      setState(() => _isLoading = false);
    }
  }

  void _calcTotals() {
    _totalCustomers = _stats.fold(0, (s, i) => s + i.numberOfCustomer);
    _totalPrice = _stats.fold(0.0, (s, i) => s + i.amountPrice);
    _totalCost = _stats.fold(0.0, (s, i) => s + i.amountCost);
    _totalDay = _stats.fold(0.0, (s, i) => s + i.amountDay);
    _totalItems = _stats.fold(0, (s, i) => s + i.numberOfItemSale);
    _totalReceipts = _stats.fold(0.0, (s, i) => s + i.amountReceipt);
    _totalZeroedCustomers = _stats.fold(0, (s, i) => s + i.numberOfCustomerZero);
    _totalZeroedPrice = _stats.fold(0.0, (s, i) => s + i.amountPriceZero);
    _totalZeroedDay = _stats.fold(0.0, (s, i) => s + i.amountDayZero);
  }

  Future<void> _exportStats() async {
    try {
      await ExcelExport.exportDelegateStats(
        data: _stats.map((s) => {
          'delegateName': s.delegateName, 'numberOfCustomer': s.numberOfCustomer,
          'amountPrice': s.amountPrice, 'amountCost': s.amountCost, 'amountDay': s.amountDay,
          'numberOfItemSale': s.numberOfItemSale, 'amountReceipt': s.amountReceipt,
          'numberOfCustomerZero': s.numberOfCustomerZero, 'amountPriceZero': s.amountPriceZero, 'amountDayZero': s.amountDayZero,
        }).toList(),
        fileName: '${widget.excludeZeroed ? 'بدون_مصفرين' : 'احصائيات'}_${Formatters.todayEnCA()}',
        excludeZeroed: widget.excludeZeroed,
      );
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('تم التصدير', style: TextStyle())));
    } catch (_) {}
  }

  Future<void> _pickDateRange() async {
    final range = await showDateRangePicker(
      context: context,
      firstDate: DateTime(2020),
      lastDate: DateTime.now().add(const Duration(days: 365)),
      initialDateRange: DateTimeRange(start: _fromDate, end: _toDate),
    );
    if (range != null) { _fromDate = range.start; _toDate = range.end; _load(); }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: Text(widget.excludeZeroed ? 'بدون المصفرين' : 'الإحصائيات', style: const TextStyle()),
        actions: [IconButton(icon: const Icon(Icons.table_chart_outlined), tooltip: 'تصدير Excel', onPressed: _exportStats), IconButton(icon: const Icon(Icons.date_range), onPressed: _pickDateRange), IconButton(icon: const Icon(Icons.refresh), onPressed: _load)],
      ),
      body: _isLoading ? const LoadingIndicator() : Column(children: [
        _buildStatsRow(),
        Expanded(child: _stats.isEmpty ? const EmptyState(message: 'لا توجد بيانات') : _buildTable()),
      ]),
    );
  }

  Widget _buildStatsRow() {
    return SizedBox(
      height: 90,
      child: ListView(scrollDirection: Axis.horizontal, padding: const EdgeInsets.symmetric(horizontal: 8), children: [
        ModernStatCard.compact(title: 'العملاء', value: Formatters.formatNumber(_totalCustomers), icon: Icons.people, color: 'primary'),
        ModernStatCard.compact(title: 'السعر', value: Formatters.formatCurrency(_totalPrice), icon: Icons.attach_money, color: 'success'),
        ModernStatCard.compact(title: 'التقسيط', value: Formatters.formatCurrency(_totalDay), icon: Icons.payments, color: 'warning'),
        ModernStatCard.compact(title: 'المستلم', value: Formatters.formatCurrency(_totalReceipts), icon: Icons.download_done, color: 'info'),
        if (!widget.excludeZeroed) ModernStatCard.compact(title: 'مصفرين', value: Formatters.formatNumber(_totalZeroedCustomers), icon: Icons.person_off, color: 'error'),
        if (!widget.excludeZeroed) ModernStatCard.compact(title: 'سعر المصفرين', value: Formatters.formatCurrency(_totalZeroedPrice), icon: Icons.money_off, color: 'secondary'),
      ]),
    );
  }

  Widget _buildTable() {
    return SingleChildScrollView(
      scrollDirection: Axis.horizontal,
      child: DataTable(
        columnSpacing: 16,
        columns: [
          const DataColumn(label: Text('المندوب', style: TextStyle(fontWeight: FontWeight.bold))),
          const DataColumn(label: Text('عملاء', style: TextStyle(fontWeight: FontWeight.bold))),
          const DataColumn(label: Text('السعر', style: TextStyle(fontWeight: FontWeight.bold))),
          const DataColumn(label: Text('الكلفة', style: TextStyle(fontWeight: FontWeight.bold))),
          const DataColumn(label: Text('تقسيط', style: TextStyle(fontWeight: FontWeight.bold))),
          const DataColumn(label: Text('مواد', style: TextStyle(fontWeight: FontWeight.bold))),
          const DataColumn(label: Text('مستلم', style: TextStyle(fontWeight: FontWeight.bold))),
          if (!widget.excludeZeroed) const DataColumn(label: Text('مصفرين', style: TextStyle(fontWeight: FontWeight.bold))),
          if (!widget.excludeZeroed) const DataColumn(label: Text('سعر مصفر', style: TextStyle(fontWeight: FontWeight.bold))),
        ],
        rows: _stats.map((s) => DataRow(cells: [
          DataCell(Text(s.delegateName, style: const TextStyle())),
          DataCell(Text(Formatters.formatNumber(s.numberOfCustomer), style: const TextStyle())),
          DataCell(Text(Formatters.formatCurrency(s.amountPrice), style: const TextStyle())),
          DataCell(Text(Formatters.formatCurrency(s.amountCost), style: const TextStyle())),
          DataCell(Text(Formatters.formatCurrency(s.amountDay), style: const TextStyle())),
          DataCell(Text(Formatters.formatNumber(s.numberOfItemSale), style: const TextStyle())),
          DataCell(Text(Formatters.formatCurrency(s.amountReceipt), style: const TextStyle())),
          if (!widget.excludeZeroed) DataCell(Text(Formatters.formatNumber(s.numberOfCustomerZero), style: const TextStyle())),
          if (!widget.excludeZeroed) DataCell(Text(Formatters.formatCurrency(s.amountPriceZero), style: const TextStyle())),
        ])).toList(),
      ),
    );
  }
}
