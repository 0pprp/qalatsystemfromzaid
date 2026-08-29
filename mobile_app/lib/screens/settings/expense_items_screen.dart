import 'package:flutter/material.dart';
import '../../services/api_service.dart';
import '../../utils/formatters.dart';
import '../../widgets/modern_stat_card.dart';
import '../../widgets/states.dart';

class ExpenseItemsScreen extends StatefulWidget {
  const ExpenseItemsScreen({super.key});
  @override
  State<ExpenseItemsScreen> createState() => _ExpenseItemsScreenState();
}

class _ExpenseItemsScreenState extends State<ExpenseItemsScreen> {
  final ApiService _api = ApiService();
  List<Map<String, dynamic>> _items = [];
  bool _isLoading = true;

  @override
  void initState() { super.initState(); _load(); }

  Future<void> _load() async {
    setState(() => _isLoading = true);
    try {
      final res = await _api.get('ExchangesItems/ExchangesItems_GetAll');
      if (res.data is List) {
        setState(() { _items = (res.data as List).cast<Map<String, dynamic>>(); _isLoading = false; });
      } else { setState(() => _isLoading = false); }
    } catch (_) { setState(() => _isLoading = false); }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('مواد الصرف'),
        actions: [IconButton(icon: const Icon(Icons.refresh), onPressed: _load)],
      ),
      body: _isLoading
          ? const LoadingIndicator()
          : _items.isEmpty
              ? const EmptyState(message: 'لا توجد مواد صرف')
              : Column(children: [
                  SizedBox(
                    height: 80,
                    child: ListView(scrollDirection: Axis.horizontal, padding: const EdgeInsets.symmetric(horizontal: 8), children: [
                      ModernStatCard.compact(title: 'عدد المواد', value: Formatters.formatNumber(_items.length), icon: Icons.receipt, color: 'primary'),
                      ModernStatCard.compact(title: 'المبلغ', value: Formatters.formatCurrency(_items.fold(0.0, (s, i) => s + (i['amountDenar'] ?? 0).toDouble())), icon: Icons.attach_money, color: 'success'),
                    ]),
                  ),
                  Expanded(child: ListView.builder(
                    padding: const EdgeInsets.all(8),
                    itemCount: _items.length,
                    itemBuilder: (_, i) {
                      final item = _items[i];
                      return Card(
                        margin: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                        child: ListTile(
                          leading: CircleAvatar(backgroundColor: const Color(0xFFF2994A).withAlpha(51), child: const Icon(Icons.currency_exchange, color: Color(0xFFF2994A))),
                          title: Text(item['exchangeItemName'] ?? '', style: const TextStyle(fontWeight: FontWeight.bold)),
                          subtitle: Text('${Formatters.formatCurrency(item['amountDenar'])} | الحد: ${Formatters.formatCurrency(item['limitDenar'])}', style: const TextStyle(fontSize: 12)),
                        ),
                      );
                    },
                  )),
                ]),
    );
  }
}
