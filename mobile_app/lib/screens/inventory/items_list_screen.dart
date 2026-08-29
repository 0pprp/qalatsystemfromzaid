import 'package:flutter/material.dart';
import '../../services/api_service.dart';
import '../../utils/formatters.dart';
import '../../widgets/modern_stat_card.dart';
import '../../widgets/states.dart';

class ItemsListScreen extends StatefulWidget {
  const ItemsListScreen({super.key});
  @override
  State<ItemsListScreen> createState() => _ItemsListScreenState();
}

class _ItemsListScreenState extends State<ItemsListScreen> {
  final ApiService _api = ApiService();
  List<Map<String, dynamic>> _items = [];
  List<Map<String, dynamic>> _stores = [];
  bool _isLoading = true;
  String _searchText = '';
  int? _storeID;
  String _showType = 'الكل';

  @override
  void initState() { super.initState(); _loadStores(); _load(); }

  Future<void> _loadStores() async {
    try { final r = await _api.get('Stores/StoresData_GetAll'); if (r.data is List) _stores = (r.data as List).cast<Map<String, dynamic>>(); } catch (_) {}
  }

  Future<void> _load() async {
    setState(() => _isLoading = true);
    try {
      final search = _searchText.isEmpty ? 'null' : _searchText;
      final store = _storeID?.toString() ?? 'null';
      final res = await _api.get('Items/Items_GetAll/$store&&$search&&$_showType');
      if (res.data is List) {
        setState(() { _items = (res.data as List).cast<Map<String, dynamic>>(); _isLoading = false; });
      } else { setState(() => _isLoading = false); }
    } catch (_) { setState(() => _isLoading = false); }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('المواد'),
        actions: [
          PopupMenuButton<String>(onSelected: (v) { _showType = v; _load(); }, itemBuilder: (_) => ['الكل', 'متوفر', 'غير متوفر'].map((t) => PopupMenuItem(value: t, child: Text(t))).toList()),
          IconButton(icon: const Icon(Icons.refresh), onPressed: _load),
        ],
      ),
      body: _isLoading
          ? const LoadingIndicator()
          : _items.isEmpty
              ? const EmptyState(message: 'لا توجد مواد')
              : Column(children: [
                  Padding(
                    padding: const EdgeInsets.all(12),
                    child: Row(children: [
                      Expanded(
                        flex: 2,
                        child: DropdownButtonFormField<int?>(
                          value: _storeID,
                          decoration: const InputDecoration(labelText: 'المخزن', contentPadding: EdgeInsets.symmetric(horizontal: 12)),
                          items: [const DropdownMenuItem(value: null, child: Text('الكل')), ..._stores.map((s) => DropdownMenuItem(value: s['storeID'] as int?, child: Text((s['storeName'] ?? '').toString())))],
                          onChanged: (v) { _storeID = v; _load(); },
                        ),
                      ),
                      const SizedBox(width: 8),
                      Expanded(
                        flex: 3,
                        child: TextField(
                          decoration: InputDecoration(hintText: 'بحث...', prefixIcon: const Icon(Icons.search), contentPadding: const EdgeInsets.symmetric(horizontal: 12, vertical: 14), border: OutlineInputBorder(borderRadius: BorderRadius.circular(12))),
                          onSubmitted: (v) { _searchText = v; _load(); },
                        ),
                      ),
                    ]),
                  ),
                  SizedBox(
                    height: 80,
                    child: ListView(scrollDirection: Axis.horizontal, padding: const EdgeInsets.symmetric(horizontal: 8), children: [
                      ModernStatCard.compact(title: 'عدد المواد', value: Formatters.formatNumber(_items.length), icon: Icons.inventory, color: 'primary'),
                      ModernStatCard.compact(title: 'الكمية', value: Formatters.formatNumber(_items.fold(0, (s, i) => s + (i['quantity'] ?? 0) as int)), icon: Icons.numbers, color: 'info'),
                      ModernStatCard.compact(title: 'القيمة', value: Formatters.formatCurrency(_items.fold(0.0, (s, i) => s + (i['totalPrice'] ?? 0).toDouble())), icon: Icons.attach_money, color: 'success'),
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
                          leading: CircleAvatar(backgroundColor: const Color(0xFF11998e).withAlpha(51), child: const Icon(Icons.inventory_2, color: Color(0xFF11998e))),
                          title: Text(item['itemName'] ?? '', style: const TextStyle(fontWeight: FontWeight.bold)),
                          subtitle: Text('الكمية: ${item['quantity']} | السعر: ${Formatters.formatCurrency(item['itemPriceDenar'])} | القسط: ${Formatters.formatCurrency(item['amountDayDenar'])}', style: const TextStyle(fontSize: 12)),
                        ),
                      );
                    },
                  )),
                ]),
    );
  }
}
