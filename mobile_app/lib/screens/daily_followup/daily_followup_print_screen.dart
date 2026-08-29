import 'package:flutter/material.dart';
import '../../services/api_service.dart';
import '../../utils/formatters.dart';
import '../../widgets/states.dart';

class DailyFollowupPrintScreen extends StatefulWidget {
  final Map<String, dynamic>? queryParams;
  const DailyFollowupPrintScreen({super.key, this.queryParams});

  @override
  State<DailyFollowupPrintScreen> createState() => _DailyFollowupPrintScreenState();
}

class _DailyFollowupPrintScreenState extends State<DailyFollowupPrintScreen> {
  final ApiService _api = ApiService();
  List<Map<String, dynamic>> _customers = [];
  bool _isLoading = true;
  late DateTime _from, _to;

  @override
  void initState() {
    super.initState();
    _from = widget.queryParams != null ? DateTime.tryParse(widget.queryParams!['from'] ?? '') ?? DateTime.now() : DateTime.now();
    _to = widget.queryParams != null ? DateTime.tryParse(widget.queryParams!['to'] ?? '') ?? DateTime.now().add(const Duration(days: 1)) : DateTime.now().add(const Duration(days: 1));
    _load();
  }

  Future<void> _load() async {
    setState(() => _isLoading = true);
    try {
      final from = Formatters.formatDate(_from.toIso8601String().split('T')[0]);
      final to = Formatters.formatDate(_to.toIso8601String().split('T')[0]);
      final res = await _api.get('Customers/Customers_Follow/$from&&$to');
      if (res.data is List) { setState(() { _customers = (res.data as List).cast<Map<String, dynamic>>(); _isLoading = false; }); }
      else { setState(() => _isLoading = false); }
    } catch (_) { setState(() => _isLoading = false); }
  }

  @override
  Widget build(BuildContext context) {
    final totalRemaining = _customers.fold(0.0, (s,c) => s + (c['amountRemaining']??0).toDouble());
    return Scaffold(
      appBar: AppBar(title: const Text('طباعة المتابعة', style: TextStyle()), actions: [IconButton(icon: const Icon(Icons.print), onPressed: () {})]),
      body: _isLoading ? const LoadingIndicator() : Column(children: [
        Padding(padding: const EdgeInsets.all(12), child: Row(children: [
          Expanded(child: ListTile(title: const Text('من'), subtitle: Text(Formatters.formatDate(_from.toIso8601String().split('T')[0])), onTap: () async {
            final d = await showDatePicker(context: context, initialDate: _from, firstDate: DateTime(2020), lastDate: DateTime.now().add(const Duration(days: 365)));
            if (d != null) { _from = d; _load(); }
          })),
          Expanded(child: ListTile(title: const Text('إلى'), subtitle: Text(Formatters.formatDate(_to.toIso8601String().split('T')[0])), onTap: () async {
            final d = await showDatePicker(context: context, initialDate: _to, firstDate: DateTime(2020), lastDate: DateTime.now().add(const Duration(days: 365)));
            if (d != null) { _to = d; _load(); }
          })),
        ])),
        Padding(padding: const EdgeInsets.all(16), child: Text('إجمالي المتبقي: ${Formatters.formatCurrency(totalRemaining)}', style: const TextStyle(fontSize: 18, fontWeight: FontWeight.bold))),
        Expanded(child: _customers.isEmpty ? const EmptyState(message: 'لا توجد بيانات') : SingleChildScrollView(
          scrollDirection: Axis.horizontal,
          child: DataTable(columns: const [
            DataColumn(label: Text('الزبون', style: TextStyle(fontWeight: FontWeight.bold))),
            DataColumn(label: Text('المندوب', style: TextStyle(fontWeight: FontWeight.bold))),
            DataColumn(label: Text('المبلغ', style: TextStyle(fontWeight: FontWeight.bold))),
            DataColumn(label: Text('المستلم', style: TextStyle(fontWeight: FontWeight.bold))),
            DataColumn(label: Text('المتبقي', style: TextStyle(fontWeight: FontWeight.bold))),
          ], rows: _customers.map((c) => DataRow(cells: [
            DataCell(Text(c['customerName'] ?? '', style: const TextStyle())),
            DataCell(Text(c['delegateName'] ?? '', style: const TextStyle())),
            DataCell(Text(Formatters.formatCurrency(c['amountTotalSales']), style: const TextStyle())),
            DataCell(Text(Formatters.formatCurrency(c['receiptsTotal']), style: const TextStyle())),
            DataCell(Text(Formatters.formatCurrency(c['amountRemaining']), style: const TextStyle(fontWeight: FontWeight.bold, color: Colors.red))),
          ])).toList()),
        )),
      ]),
    );
  }
}
