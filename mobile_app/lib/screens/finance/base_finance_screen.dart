import 'package:flutter/material.dart';
import '../../services/api_service.dart';
import '../../utils/formatters.dart';
import '../../widgets/modern_stat_card.dart';
import '../../widgets/states.dart';

class BaseCrudScreen extends StatefulWidget {
  final String title;
  final String listEndpoint;
  final String deleteEndpoint; // e.g. "Accounts/AddToBox_Delete/{id}"
  final IconData icon;
  final String statLabel;
  final String statIcon;
  final String statColor;
  final List<StatItem> Function(List<Map<String, dynamic>> items) statBuilders;
  final List<Map<String, dynamic>> Function()? onAdd;
  final void Function(Map<String, dynamic> item)? onEdit;
  final Widget Function(Map<String, dynamic> item, VoidCallback onDelete) itemBuilder;
  final bool showAdd;
  final VoidCallback? onFloatingAdd;
  final List<Widget>? extraActions;
  final Map<String, dynamic>? extraParams;

  const BaseCrudScreen({
    super.key,
    required this.title,
    required this.listEndpoint,
    required this.deleteEndpoint,
    required this.icon,
    this.statLabel = 'الإجمالي',
    this.statIcon = 'primary',
    this.statColor = 'primary',
    required this.statBuilders,
    this.onAdd,
    this.onEdit,
    required this.itemBuilder,
    this.showAdd = false,
    this.onFloatingAdd,
    this.extraActions,
    this.extraParams,
  });

  @override
  State<BaseCrudScreen> createState() => _BaseCrudScreenState();
}

class StatItem {
  final String title;
  final String value;
  final IconData icon;
  final String color;
  StatItem({required this.title, required this.value, required this.icon, this.color = 'primary'});
}

class _BaseCrudScreenState extends State<BaseCrudScreen> {
  final ApiService _api = ApiService();
  List<Map<String, dynamic>> _items = [];
  bool _isLoading = true;

  @override
  void initState() { super.initState(); _load(); }

  Future<void> _load() async {
    setState(() => _isLoading = true);
    try {
      final res = await _api.get(widget.listEndpoint);
      if (res.data is List) { setState(() { _items = (res.data as List).cast<Map<String, dynamic>>(); _isLoading = false; }); }
      else { setState(() => _isLoading = false); }
    } catch (_) { setState(() => _isLoading = false); }
  }

  Future<void> _delete(String endpoint, int id) async {
    final ok = await showDialog<bool>(context: context, builder: (ctx) => AlertDialog(
      title: const Text('تأكيد الحذف', style: TextStyle()),
      content: const Text('هل تريد الحذف؟', style: TextStyle()),
      actions: [TextButton(onPressed: () => Navigator.pop(ctx, false), child: const Text('إلغاء')), TextButton(onPressed: () => Navigator.pop(ctx, true), child: const Text('حذف', style: TextStyle(color: Colors.red)))],
    ));
    if (ok == true) { try { await _api.delete(endpoint.replaceAll('{id}', id.toString())); _load(); } catch (_) {} }
  }

  @override
  Widget build(BuildContext context) {
    final stats = widget.statBuilders(_items);
    return Scaffold(
      appBar: AppBar(
        title: Text(widget.title, style: const TextStyle()),
        actions: [...?widget.extraActions, IconButton(icon: const Icon(Icons.refresh), onPressed: _load)],
      ),
      floatingActionButton: widget.onFloatingAdd != null ? FloatingActionButton(onPressed: widget.onFloatingAdd, child: const Icon(Icons.add)) : null,
      body: _isLoading ? const LoadingIndicator() : Column(children: [
        SizedBox(height: 90, child: ListView(scrollDirection: Axis.horizontal, padding: const EdgeInsets.symmetric(horizontal: 8), children: stats.map((s) => ModernStatCard.compact(title: s.title, value: s.value, icon: s.icon, color: s.color)).toList())),
        Expanded(child: _items.isEmpty ? const EmptyState(message: 'لا توجد بيانات') : ListView.builder(
          padding: const EdgeInsets.all(8), itemCount: _items.length,
          itemBuilder: (_, i) => widget.itemBuilder(_items[i], () => _delete(widget.deleteEndpoint, _items[i]['id'] ?? _items[i]['${widget.title}ID'] ?? 0)),
        )),
      ]),
    );
  }
}

// ---- SHARED helpers used by finance screens ----
class FinanceHelper {
  static List<StatItem> countAndTotal(List<Map<String, dynamic>> items, String amountKey) => [
    StatItem(title: 'العدد', value: Formatters.formatNumber(items.length), icon: Icons.receipt, color: 'primary'),
    StatItem(title: 'المبلغ', value: Formatters.formatCurrency(items.fold(0.0, (s,i) => s + (i[amountKey]??0).toDouble())), icon: Icons.attach_money, color: 'success'),
  ];

  static Future<void> pickDateRangeAndReload(BuildContext context, DateTime from, DateTime to, Future<void> Function(DateTime, DateTime) onPicked) async {
    final range = await showDateRangePicker(context: context, firstDate: DateTime(2020), lastDate: DateTime.now().add(const Duration(days: 365)), initialDateRange: DateTimeRange(start: from, end: to));
    if (range != null) onPicked(range.start, range.end);
  }
}
