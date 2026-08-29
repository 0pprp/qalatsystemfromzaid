import 'package:flutter/material.dart';
import '../../services/api_service.dart';
import '../../utils/formatters.dart';
import '../../widgets/modern_stat_card.dart';
import '../../widgets/states.dart';

class SuppliersListScreen extends StatefulWidget {
  const SuppliersListScreen({super.key});
  @override
  State<SuppliersListScreen> createState() => _SuppliersListScreenState();
}

class _SuppliersListScreenState extends State<SuppliersListScreen> {
  final ApiService _api = ApiService();
  List<Map<String, dynamic>> _suppliers = [];
  bool _isLoading = true;
  String _searchText = '';

  @override
  void initState() { super.initState(); _load(); }

  Future<void> _load() async {
    setState(() => _isLoading = true);
    try {
      final search = _searchText.isEmpty ? 'null' : _searchText;
      final res = await _api.get('Suppliers/Suppliers_GetAll/$search');
      if (res.data is List) {
        setState(() { _suppliers = (res.data as List).cast<Map<String, dynamic>>(); _isLoading = false; });
      } else { setState(() => _isLoading = false); }
    } catch (_) { setState(() => _isLoading = false); }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('الموردين'),
        actions: [IconButton(icon: const Icon(Icons.refresh), onPressed: _load)],
      ),
      body: _isLoading
          ? const LoadingIndicator()
          : _suppliers.isEmpty
              ? const EmptyState(message: 'لا يوجد موردين')
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
                      ModernStatCard.compact(title: 'عدد الموردين', value: Formatters.formatNumber(_suppliers.length), icon: Icons.people, color: 'primary'),
                      ModernStatCard.compact(title: 'المبلغ الكلي', value: Formatters.formatCurrency(_suppliers.fold(0.0, (s, i) => s + (i['amountDenar'] ?? 0).toDouble())), icon: Icons.attach_money, color: 'success'),
                    ]),
                  ),
                  Expanded(child: ListView.builder(
                    padding: const EdgeInsets.all(8),
                    itemCount: _suppliers.length,
                    itemBuilder: (_, i) {
                      final s = _suppliers[i];
                      return Card(
                        margin: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                        child: ListTile(
                          leading: CircleAvatar(backgroundColor: const Color(0xFFF2994A).withAlpha(51), child: const Icon(Icons.local_shipping, color: Color(0xFFF2994A))),
                          title: Text(s['supplierName'] ?? '', style: const TextStyle(fontWeight: FontWeight.bold)),
                          subtitle: Text('${s['phoneNumber'] ?? '-'} | ${Formatters.formatCurrency(s['amountDenar'])}', style: const TextStyle(fontSize: 12)),
                        ),
                      );
                    },
                  )),
                ]),
    );
  }
}
