import 'package:flutter/material.dart';
import '../../config/theme.dart';
import '../../services/api_service.dart';
import '../../models/statistics.dart';
import '../../models/delegate.dart';
import '../../utils/formatters.dart';
import '../../widgets/dashboard_header.dart';
import '../../widgets/modern_stat_card.dart';
import '../../widgets/states.dart';
import '../../animations/list_animations.dart';
import '../../utils/excel_export.dart';

class DashboardScreen extends StatefulWidget {
  const DashboardScreen({super.key});

  @override
  State<DashboardScreen> createState() => _DashboardScreenState();
}

class _DashboardScreenState extends State<DashboardScreen> {
  final ApiService _api = ApiService();
  StatisticsAppModel? _stats;
  List<DelegateStatisticsModel> _delegateStats = [];
  bool _isLoadingStats = true;
  bool _isLoadingDelegates = true;
  DateTime _fromDate = DateTime.now().subtract(const Duration(days: 30));
  DateTime _toDate = DateTime.now().add(const Duration(days: 1));

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) => _loadAll());
  }

  Future<void> _loadAll() async {
    await Future.wait([_fetchStats(), _fetchDelegateStats()]);
  }

  Future<void> _fetchStats() async {
    setState(() => _isLoadingStats = true);
    try {
      final response = await _api.get('Statistics/StatisticsApp_GetAll');
      if (response.data != null) {
        setState(() {
          _stats = StatisticsAppModel.fromJson(response.data);
          _isLoadingStats = false;
        });
      }
    } catch (e) {
      setState(() => _isLoadingStats = false);
    }
  }

  Future<void> _fetchDelegateStats() async {
    setState(() => _isLoadingDelegates = true);
    try {
      final from = Formatters.formatDate(_fromDate.toIso8601String().split('T')[0]);
      final to = Formatters.formatDate(_toDate.toIso8601String().split('T')[0]);
      final response = await _api.get('Delegates/Delegates_Statistics/$from&&$to');
      if (response.data is List) {
        setState(() {
          _delegateStats = (response.data as List).map((j) => DelegateStatisticsModel.fromJson(j)).toList();
          _isLoadingDelegates = false;
        });
      }
    } catch (e) {
      setState(() => _isLoadingDelegates = false);
    }
  }

  Future<void> _pickDateRange() async {
    final range = await showDateRangePicker(
      context: context,
      firstDate: DateTime(2020),
      lastDate: DateTime.now().add(const Duration(days: 365)),
      initialDateRange: DateTimeRange(start: _fromDate, end: _toDate),
    );
    if (range != null) {
      setState(() { _fromDate = range.start; _toDate = range.end; });
      _fetchDelegateStats();
    }
  }

  Future<void> _exportDelegateStats() async {
    try {
      await ExcelExport.exportDelegateStats(
        data: _delegateStats.map((s) => {
          'delegateName': s.delegateName, 'numberOfCustomer': s.numberOfCustomer,
          'amountPrice': s.amountPrice, 'amountCost': s.amountCost, 'amountDay': s.amountDay,
          'numberOfItemSale': s.numberOfItemSale, 'amountReceipt': s.amountReceipt,
          'numberOfCustomerZero': s.numberOfCustomerZero, 'amountPriceZero': s.amountPriceZero, 'amountDayZero': s.amountDayZero,
        }).toList(),
        fileName: 'احصائيات_المندوبين_${Formatters.todayEnCA()}',
      );
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('تم التصدير')));
    } catch (_) {}
  }

  // Public getters so HomeShell can access them for AppBar actions
  bool get isLoading => _isLoadingStats && _isLoadingDelegates;
  Future<void> Function() get onRefresh => _loadAll;
  Future<void> Function() get onExport => _exportDelegateStats;

  @override
  Widget build(BuildContext context) {
    return RefreshIndicator(
      onRefresh: _loadAll,
      child: SingleChildScrollView(
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.only(bottom: 16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            // Header
            DashboardHeader(
              title: 'لوحة التحكم الرئيسية',
              subtitle: 'نظرة شاملة على إحصائيات النظام والمؤشرات الرئيسية',
              icon: Icons.dashboard_rounded,
            ),

            // Summary Cards
            if (_isLoadingStats)
              const Padding(padding: EdgeInsets.all(20), child: Center(child: CircularProgressIndicator()))
            else ...[
              Padding(
                padding: const EdgeInsets.symmetric(horizontal: 12),
                child: Row(
                  children: [
                    Expanded(child: ModernStatCard(title: 'إجمالي العملاء', value: Formatters.formatNumber(_stats?.numberOfCustomers), icon: Icons.people_rounded, color: 'primary')),
                    const SizedBox(width: 8),
                    Expanded(child: ModernStatCard(title: 'إجمالي المبيعات', value: Formatters.formatNumber(_stats?.numberOfSales), icon: Icons.shopping_bag_rounded, color: 'success')),
                  ],
                ),
              ),
              const SizedBox(height: 8),
              Padding(
                padding: const EdgeInsets.symmetric(horizontal: 12),
                child: Row(
                  children: [
                    Expanded(child: ModernStatCard(title: 'إجمالي المشتريات', value: Formatters.formatNumber(_stats?.numberOfPurchases), icon: Icons.shopping_cart_rounded, color: 'info')),
                    const SizedBox(width: 8),
                    Expanded(child: ModernStatCard(title: 'المخازن', value: Formatters.formatNumber(_stats?.numberOfStores), icon: Icons.warehouse_rounded, color: 'warning')),
                  ],
                ),
              ),
            ],

            const SizedBox(height: 16),

            // Detailed Stats
            if (!_isLoadingStats && _stats != null) _buildDetailedStats(),

            const SizedBox(height: 16),

            // Delegate Section
            _buildDelegateSection(),
          ],
        ),
      ),
    );
  }

  Widget _buildDetailedStats() {
    final items = [
      {'title': 'عدد العناصر', 'value': _stats!.numberOfItems, 'icon': Icons.inventory_2_rounded, 'color': 'primary'},
      {'title': 'عدد الموردين', 'value': _stats!.numberOfSuppliers, 'icon': Icons.local_shipping_rounded, 'color': 'info'},
      {'title': 'عدد المندوبين', 'value': _stats!.numberOfDelegates, 'icon': Icons.person_search_rounded, 'color': 'success'},
      {'title': 'عدد التسديدات', 'value': _stats!.numberOfPayments, 'icon': Icons.payment_rounded, 'color': 'warning'},
      {'title': 'الخزائن النقدية', 'value': _stats!.numberOfCashBoxes, 'icon': Icons.account_balance_wallet_rounded, 'color': 'secondary'},
      {'title': 'إضافات الصندوق', 'value': _stats!.numberOfAdditionsToBox, 'icon': Icons.add_circle_outline_rounded, 'color': 'primary'},
      {'title': 'سحوبات الصندوق', 'value': _stats!.numberOfWithdrawalsFromBox, 'icon': Icons.remove_circle_outline_rounded, 'color': 'error'},
      {'title': 'تحويلات الصناديق', 'value': _stats!.numberOfTransfersBetweenBoxes, 'icon': Icons.swap_horiz_rounded, 'color': 'info'},
    ];

    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 12),
      child: GridView.builder(
        shrinkWrap: true,
        physics: const NeverScrollableScrollPhysics(),
        gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(crossAxisCount: 2, childAspectRatio: 1.5, crossAxisSpacing: 8, mainAxisSpacing: 8),
        itemCount: items.length,
        itemBuilder: (context, index) {
          final item = items[index];
          return StaggeredListItem(
            index: index,
            child: ModernStatCard(
              title: item['title'] as String,
              value: Formatters.formatNumber(item['value']),
              icon: item['icon'] as IconData,
              color: item['color'] as String,
            ),
          );
        },
      ),
    );
  }

  Widget _buildDelegateSection() {
    final scheme = Theme.of(context).colorScheme;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Padding(
          padding: const EdgeInsets.symmetric(horizontal: 16),
          child: Row(
            children: [
              Text('إحصائيات المندوبين', style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold, color: scheme.onSurface)),
              const Spacer(),
              TextButton.icon(
                onPressed: _pickDateRange,
                icon: const Icon(Icons.date_range, size: 18),
                label: Text(
                  '${Formatters.formatDate(_fromDate.toIso8601String().split('T')[0])} - ${Formatters.formatDate(_toDate.toIso8601String().split('T')[0])}',
                  style: const TextStyle(fontSize: 11),
                ),
              ),
            ],
          ),
        ),
        const SizedBox(height: 8),
        if (!_isLoadingDelegates) _buildTotalsCards(),
        const SizedBox(height: 12),
        if (_isLoadingDelegates)
          const LoadingIndicator()
        else if (_delegateStats.isEmpty)
          const EmptyState(message: 'لا توجد بيانات للمندوبين')
        else
          _buildDelegateTable(),
      ],
    );
  }

  Widget _buildTotalsCards() {
    final totalCustomers = _delegateStats.fold<int>(0, (sum, s) => sum + s.numberOfCustomer);
    final totalPrice = _delegateStats.fold<double>(0, (sum, s) => sum + s.amountPrice);
    final totalCost = _delegateStats.fold<double>(0, (sum, s) => sum + s.amountCost);
    final totalDay = _delegateStats.fold<double>(0, (sum, s) => sum + s.amountDay);
    final totalSale = _delegateStats.fold<int>(0, (sum, s) => sum + s.numberOfItemSale);
    final totalReceipt = _delegateStats.fold<double>(0, (sum, s) => sum + s.amountReceipt);
    final totalZero = _delegateStats.fold<int>(0, (sum, s) => sum + s.numberOfCustomerZero);

    return SizedBox(
      height: 120,
      child: ListView(
        scrollDirection: Axis.horizontal,
        padding: const EdgeInsets.symmetric(horizontal: 12),
        children: [
          SizedBox(width: 160, child: ModernStatCard.compact(title: 'عدد العملاء', value: Formatters.formatNumber(totalCustomers), icon: Icons.people_rounded, color: 'primary')),
          const SizedBox(width: 8),
          SizedBox(width: 160, child: ModernStatCard.compact(title: 'سعر البيع', value: Formatters.formatCurrency(totalPrice), icon: Icons.monetization_on_rounded, color: 'info')),
          const SizedBox(width: 8),
          SizedBox(width: 160, child: ModernStatCard.compact(title: 'سعر الشراء', value: Formatters.formatCurrency(totalCost), icon: Icons.money_rounded, color: 'secondary')),
          const SizedBox(width: 8),
          SizedBox(width: 160, child: ModernStatCard.compact(title: 'القسط الكلي', value: Formatters.formatCurrency(totalDay), icon: Icons.calendar_month_rounded, color: 'success')),
          const SizedBox(width: 8),
          SizedBox(width: 160, child: ModernStatCard.compact(title: 'عدد المباع', value: Formatters.formatNumber(totalSale), icon: Icons.check_circle_rounded, color: 'warning')),
          const SizedBox(width: 8),
          SizedBox(width: 160, child: ModernStatCard.compact(title: 'الواصل الكلي', value: Formatters.formatCurrency(totalReceipt), icon: Icons.credit_card_rounded, color: 'success')),
          const SizedBox(width: 8),
          SizedBox(width: 160, child: ModernStatCard.compact(title: 'عدد المصفرين', value: Formatters.formatNumber(totalZero), icon: Icons.person_off_rounded, color: 'primary')),
        ],
      ),
    );
  }

  Widget _buildDelegateTable() {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 12),
      child: Card(
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
        child: SingleChildScrollView(
          scrollDirection: Axis.horizontal,
          child: DataTable(
            columns: const [
              DataColumn(label: Text('المندوب')),
              DataColumn(label: Text('العملاء'), numeric: true),
              DataColumn(label: Text('سعر البيع'), numeric: true),
              DataColumn(label: Text('القسط'), numeric: true),
              DataColumn(label: Text('المباع'), numeric: true),
              DataColumn(label: Text('الواصل'), numeric: true),
            ],
            rows: _delegateStats.map((s) {
              return DataRow(cells: [
                DataCell(Text(s.delegateName)),
                DataCell(Chip(label: Text('${s.numberOfCustomer}', style: const TextStyle(fontSize: 11)), backgroundColor: AppTheme.primaryColor.withAlpha(25))),
                DataCell(Text(Formatters.formatCurrency(s.amountPrice))),
                DataCell(Text(Formatters.formatCurrency(s.amountDay))),
                DataCell(Chip(label: Text('${s.numberOfItemSale}', style: const TextStyle(fontSize: 11)), backgroundColor: AppTheme.warningColor.withAlpha(25))),
                DataCell(Text(Formatters.formatCurrency(s.amountReceipt))),
              ]);
            }).toList(),
          ),
        ),
      ),
    );
  }
}
