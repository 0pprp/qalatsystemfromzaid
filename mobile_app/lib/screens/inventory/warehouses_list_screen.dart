import 'package:flutter/material.dart';
import '../../services/api_service.dart';
import '../../utils/formatters.dart';
import '../../widgets/modern_stat_card.dart';
import '../../widgets/states.dart';

class WarehousesListScreen extends StatefulWidget {
  const WarehousesListScreen({super.key});
  @override
  State<WarehousesListScreen> createState() => _WarehousesListScreenState();
}

class _WarehousesListScreenState extends State<WarehousesListScreen> {
  final ApiService _api = ApiService();
  List<Map<String, dynamic>> _stores = [];
  bool _isLoading = true;
  String _searchText = '';

  @override
  void initState() { super.initState(); _load(); }

  Future<void> _load() async {
    setState(() => _isLoading = true);
    try {
      final search = _searchText.isEmpty ? 'null' : _searchText;
      final res = await _api.get('Stores/Stores_GetAll/$search');
      if (res.data is List) {
        setState(() { _stores = (res.data as List).cast<Map<String, dynamic>>(); _isLoading = false; });
      } else { setState(() => _isLoading = false); }
    } catch (_) { setState(() => _isLoading = false); }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('المخازن'),
        actions: [IconButton(icon: const Icon(Icons.refresh), onPressed: _load)],
      ),
      body: _isLoading
          ? const LoadingIndicator()
          : _stores.isEmpty
              ? const EmptyState(message: 'لا توجد مخازن')
              : Column(children: [
                  Padding(
                    padding: const EdgeInsets.all(12),
                    child: TextField(
                      decoration: InputDecoration(hintText: 'بحث...', prefixIcon: const Icon(Icons.search), border: OutlineInputBorder(borderRadius: BorderRadius.circular(12))),
                      onSubmitted: (v) { _searchText = v; _load(); },
                    ),
                  ),
                  SizedBox(
                    height: 80,
                    child: ListView(scrollDirection: Axis.horizontal, padding: const EdgeInsets.symmetric(horizontal: 8), children: [
                      ModernStatCard.compact(title: 'عدد المخازن', value: Formatters.formatNumber(_stores.length), icon: Icons.warehouse, color: 'primary'),
                      ModernStatCard.compact(title: 'السعر الكلي', value: Formatters.formatCurrency(_stores.fold(0.0, (s, i) => s + (i['totalPrice'] ?? 0).toDouble())), icon: Icons.attach_money, color: 'success'),
                    ]),
                  ),
                  Expanded(child: ListView.builder(
                    padding: const EdgeInsets.all(8),
                    itemCount: _stores.length,
                    itemBuilder: (_, i) {
                      final s = _stores[i];
                      return Card(
                        margin: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                        child: ListTile(
                          leading: CircleAvatar(backgroundColor: const Color(0xFF1e5799).withAlpha(51), child: const Icon(Icons.warehouse, color: Color(0xFF1e5799))),
                          title: Text(s['storeName'] ?? '', style: const TextStyle(fontWeight: FontWeight.bold)),
                          subtitle: Text('المكان: ${s['storePlace'] ?? '-'} | السعر: ${Formatters.formatCurrency(s['totalPrice'])}', style: const TextStyle(fontSize: 12)),
                        ),
                      );
                    },
                  )),
                ]),
    );
  }
}
