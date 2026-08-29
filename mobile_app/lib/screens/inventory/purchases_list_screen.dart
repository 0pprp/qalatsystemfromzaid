import 'package:flutter/material.dart';
import '../../services/api_service.dart';
import '../../utils/formatters.dart';
import '../../widgets/modern_stat_card.dart';
import '../../widgets/states.dart';

class PurchasesListScreen extends StatefulWidget {
  const PurchasesListScreen({super.key});

  @override
  State<PurchasesListScreen> createState() => _PurchasesListScreenState();
}

class _PurchasesListScreenState extends State<PurchasesListScreen> {
  final ApiService _api = ApiService();
  List<Map<String, dynamic>> _buys = [];
  bool _isLoading = true;
  DateTime _fromDate = DateTime.now().subtract(const Duration(days: 30));
  DateTime _toDate = DateTime.now().add(const Duration(days: 1));
  String _searchText = '';

  @override
  void initState() { super.initState(); _load(); }

  Future<void> _load() async {
    setState(() => _isLoading = true);
    try {
      final from = Formatters.formatDate(_fromDate.toIso8601String().split('T')[0]);
      final to = Formatters.formatDate(_toDate.toIso8601String().split('T')[0]);
      final search = _searchText.isEmpty ? 'null' : _searchText;
      final res = await _api.get('Buys/Buys_GetByDateByTextSearch/$from&&$to&&$search');
      if (res.data is List) { setState(() { _buys = (res.data as List).cast<Map<String, dynamic>>(); _isLoading = false; }); }
      else { setState(() => _isLoading = false); }
    } catch (_) { setState(() => _isLoading = false); }
  }

  Future<void> _delete(int id) async {
    final ok = await showDialog<bool>(context: context, builder: (ctx) => AlertDialog(
      title: const Text('تأكيد', style: TextStyle()), content: const Text('حذف هذه المشترية؟', style: TextStyle()),
      actions: [TextButton(onPressed: () => Navigator.pop(ctx, false), child: const Text('إلغاء')), TextButton(onPressed: () => Navigator.pop(ctx, true), child: const Text('حذف', style: TextStyle(color: Colors.red)))],
    ));
    if (ok == true) { try { await _api.delete('Buys/Buys_Delete/$id'); _load(); } catch (_) {} }
  }

  @override
  Widget build(BuildContext context) {
    final totalAmount = _buys.fold(0.0, (s,i) => s + (i['totalAmountDenar']??0).toDouble());
    final totalSpent = _buys.fold(0.0, (s,i) => s + (i['amountSpentDenar']??0).toDouble());
    return Scaffold(
      appBar: AppBar(
        title: const Text('المشتريات', style: TextStyle()),
        actions: [
          IconButton(icon: const Icon(Icons.date_range), onPressed: () async {
            final range = await showDateRangePicker(context: context, firstDate: DateTime(2020), lastDate: DateTime.now().add(const Duration(days: 365)), initialDateRange: DateTimeRange(start: _fromDate, end: _toDate));
            if (range != null) { _fromDate = range.start; _toDate = range.end; _load(); }
          }),
          IconButton(icon: const Icon(Icons.refresh), onPressed: _load),
        ],
      ),
      body: _isLoading ? const LoadingIndicator() : Column(children: [
        Padding(padding: const EdgeInsets.all(12), child: TextField(
          decoration: InputDecoration(hintText: 'بحث...', prefixIcon: const Icon(Icons.search), border: OutlineInputBorder(borderRadius: BorderRadius.circular(12))),
          onSubmitted: (v) { _searchText = v; _load(); },
        )),
        SizedBox(height: 80, child: ListView(scrollDirection: Axis.horizontal, padding: const EdgeInsets.symmetric(horizontal: 8), children: [
          ModernStatCard.compact(title: 'عدد السندات', value: Formatters.formatNumber(_buys.length), icon: Icons.receipt, color: 'primary'),
          ModernStatCard.compact(title: 'المبلغ الكلي', value: Formatters.formatCurrency(totalAmount), icon: Icons.attach_money, color: 'success'),
          ModernStatCard.compact(title: 'المصروف', value: Formatters.formatCurrency(totalSpent), icon: Icons.money_off, color: 'warning'),
        ])),
        Expanded(child: _buys.isEmpty ? const EmptyState(message: 'لا توجد مشتريات') : ListView.builder(
          padding: const EdgeInsets.all(8), itemCount: _buys.length,
          itemBuilder: (_, i) {
            final b = _buys[i];
            return Card(margin: const EdgeInsets.symmetric(horizontal: 8, vertical: 4), child: ExpansionTile(
              leading: CircleAvatar(backgroundColor: const Color(0xFF8E2DE2).withOpacity(0.2), child: const Icon(Icons.shopping_bag, color: Color(0xFF8E2DE2))),
              title: Text(b['supplierName'] ?? '', style: const TextStyle(fontWeight: FontWeight.bold)),
              subtitle: Text('${Formatters.formatDate(b['dateCreate']?.toString())} | ${Formatters.formatCurrency(b['totalAmountDenar'])}', style: const TextStyle(fontSize: 12)),
              trailing: IconButton(icon: const Icon(Icons.delete_outline, color: Colors.red, size: 20), onPressed: () => _delete(b['buyID'])),
              children: [Padding(padding: const EdgeInsets.all(16), child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                _d('رقم السند', b['boundNumber']?.toString()), _d('الأصناف', b['itemsNames']), _d('الصندوق', b['boxName']),
                _d('المبلغ', Formatters.formatCurrency(b['totalAmountDenar'])), _d('المصروف', Formatters.formatCurrency(b['amountSpentDenar'])),
              ]))],
            ));
          },
        )),
      ]),
    );
  }

  Widget _d(String l, String? v) => Padding(padding: const EdgeInsets.symmetric(vertical: 3), child: Row(children: [
    Expanded(child: Text(l, style: const TextStyle(color: Colors.grey, fontSize: 13))),
    Text(v ?? '-', style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 13)),
  ]));
}
