import 'package:flutter/material.dart';
import '../../services/api_service.dart';
import '../../utils/formatters.dart';
import '../../widgets/states.dart';

class DailyFollowupScreen extends StatefulWidget {
  const DailyFollowupScreen({super.key});

  @override
  State<DailyFollowupScreen> createState() => _DailyFollowupScreenState();
}

class _DailyFollowupScreenState extends State<DailyFollowupScreen> {
  final ApiService _api = ApiService();
  List<Map<String, dynamic>> _customers = [];
  bool _isLoading = true;
  DateTime _date = DateTime.now();

  @override
  void initState() { super.initState(); _load(); }

  Future<void> _load() async {
    setState(() => _isLoading = true);
    try {
      final date = Formatters.formatDate(_date.toIso8601String().split('T')[0]);
      final res = await _api.get('Customers/Customers_Follow/$date');
      if (res.data is List) { setState(() { _customers = (res.data as List).cast<Map<String, dynamic>>(); _isLoading = false; }); }
      else { setState(() => _isLoading = false); }
    } catch (_) { setState(() => _isLoading = false); }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('المتابعة اليومية', style: TextStyle()),
        actions: [
          IconButton(icon: const Icon(Icons.date_range), onPressed: () async {
            final picked = await showDatePicker(context: context, initialDate: _date, firstDate: DateTime(2020), lastDate: DateTime.now().add(const Duration(days: 365)));
            if (picked != null) { _date = picked; _load(); }
          }),
          IconButton(icon: const Icon(Icons.print), onPressed: () { Navigator.pushNamed(context, '/daily-followup-print'); }),
          IconButton(icon: const Icon(Icons.refresh), onPressed: _load),
        ],
      ),
      body: _isLoading ? const LoadingIndicator() : _customers.isEmpty ? const EmptyState(message: 'لا توجد متابعة لهذا اليوم') : ListView.builder(
        padding: const EdgeInsets.all(8), itemCount: _customers.length,
        itemBuilder: (_, i) {
          final c = _customers[i];
          return Card(margin: const EdgeInsets.symmetric(horizontal: 8, vertical: 4), child: ExpansionTile(
            leading: CircleAvatar(backgroundColor: const Color(0xFF1e5799).withOpacity(0.2), child: const Icon(Icons.person, color: Color(0xFF1e5799))),
            title: Text(c['customerName'] ?? '', style: const TextStyle(fontWeight: FontWeight.bold)),
            subtitle: Text(c['delegateName'] ?? '', style: const TextStyle(fontSize: 12)),
            children: [Padding(padding: const EdgeInsets.all(16), child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              _row('المبلغ الكلي', Formatters.formatCurrency(c['amountTotalSales'])),
              _row('التقسيط اليومي', Formatters.formatCurrency(c['amountDaySales'])),
              _row('المستلم', Formatters.formatCurrency(c['receiptsTotal'])),
              _row('المتبقي', Formatters.formatCurrency(c['amountRemaining'])),
              _row('آخر تسديد', Formatters.formatDate(c['lastPaymentDate']?.toString())),
            ]))],
          ));
        },
      ),
    );
  }

  Widget _row(String l, String v) => Padding(padding: const EdgeInsets.symmetric(vertical: 3), child: Row(children: [
    Expanded(child: Text(l, style: const TextStyle(color: Colors.grey, fontSize: 13))),
    Text(v, style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 13)),
  ]));
}
