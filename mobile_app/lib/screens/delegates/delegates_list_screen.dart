import 'package:flutter/material.dart';
import '../../services/api_service.dart';
import '../../utils/formatters.dart';
import '../../widgets/modern_stat_card.dart';
import '../../widgets/states.dart';

class DelegatesListScreen extends StatefulWidget {
  const DelegatesListScreen({super.key});

  @override
  State<DelegatesListScreen> createState() => _DelegatesListScreenState();
}

class _DelegatesListScreenState extends State<DelegatesListScreen> {
  final ApiService _api = ApiService();
  List<Map<String, dynamic>> _delegates = [];
  bool _isLoading = true;
  String _searchText = '';
  int _totalCustomers = 0;
  int _totalZeroed = 0;
  int _totalNonZeroed = 0;
  int _totalLegal = 0;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() => _isLoading = true);
    try {
      final search = _searchText.isEmpty ? 'null' : _searchText;
      final res = await _api.get('Delegates/Delegates_GetAll/$search');
      if (res.data is List) {
        setState(() {
          _delegates = (res.data as List).cast<Map<String, dynamic>>();
          _calcTotals();
          _isLoading = false;
        });
      } else {
        setState(() => _isLoading = false);
      }
    } catch (e) {
      setState(() => _isLoading = false);
    }
  }

  void _calcTotals() {
    _totalCustomers = _delegates.fold(0, (s, i) => s + (i['numberOfCustomer'] ?? 0) as int);
    _totalZeroed = _delegates.fold(0, (s, i) => s + (i['numberOfCustomerIsZero'] ?? 0) as int);
    _totalNonZeroed = _delegates.fold(0, (s, i) => s + (i['numberOfCustomerIsNotZero'] ?? 0) as int);
    _totalLegal = _delegates.fold(0, (s, i) => s + (i['numberOfCustomerIsLegal'] ?? 0) as int);
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('المندوبين'),
        actions: [
          IconButton(icon: const Icon(Icons.refresh), onPressed: _load),
        ],
      ),
      body: _isLoading
          ? const LoadingIndicator()
          : _delegates.isEmpty
              ? const EmptyState(message: 'لا يوجد مندوبين')
              : Column(
                  children: [
                    Padding(
                      padding: const EdgeInsets.all(12),
                      child: TextField(
                        decoration: InputDecoration(
                          hintText: 'بحث عن مندوب...',
                          prefixIcon: const Icon(Icons.search),
                          border: OutlineInputBorder(borderRadius: BorderRadius.circular(12)),
                        ),
                        onSubmitted: (v) {
                          _searchText = v;
                          _load();
                        },
                      ),
                    ),
                    _buildStatsRow(),
                    Expanded(child: _buildList()),
                  ],
                ),
    );
  }

  Widget _buildStatsRow() {
    return SizedBox(
      height: 100,
      child: ListView(
        scrollDirection: Axis.horizontal,
        padding: const EdgeInsets.symmetric(horizontal: 8),
        children: [
          ModernStatCard.compact(title: 'عدد المندوبين', value: Formatters.formatNumber(_delegates.length), icon: Icons.people, color: 'primary'),
          ModernStatCard.compact(title: 'جميع العملاء', value: Formatters.formatNumber(_totalCustomers), icon: Icons.groups, color: 'info'),
          ModernStatCard.compact(title: 'غير مصفرين', value: Formatters.formatNumber(_totalNonZeroed), icon: Icons.person, color: 'success'),
          ModernStatCard.compact(title: 'مصفرين', value: Formatters.formatNumber(_totalZeroed), icon: Icons.person_off, color: 'warning'),
          ModernStatCard.compact(title: 'قانونية', value: Formatters.formatNumber(_totalLegal), icon: Icons.gavel, color: 'error'),
        ],
      ),
    );
  }

  Widget _buildList() {
    return ListView.builder(
      padding: const EdgeInsets.all(8),
      itemCount: _delegates.length,
      itemBuilder: (context, idx) {
        final d = _delegates[idx];
        return Card(
          margin: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
          child: ListTile(
            leading: CircleAvatar(backgroundColor: const Color(0xFF8E2DE2).withAlpha(51), child: const Icon(Icons.person, color: Color(0xFF8E2DE2))),
            title: Text(d['delegateName'] ?? '', style: const TextStyle(fontWeight: FontWeight.bold)),
            subtitle: Text(
              'عملاء: ${d['numberOfCustomer']} | المتبقي: ${Formatters.formatCurrency(d['amountRemaining'])}',
              style: const TextStyle(fontSize: 12),
            ),
          ),
        );
      },
    );
  }
}
