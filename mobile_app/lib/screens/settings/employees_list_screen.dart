import 'package:flutter/material.dart';
import '../../services/api_service.dart';
import '../../utils/formatters.dart';
import '../../widgets/modern_stat_card.dart';
import '../../widgets/states.dart';

class EmployeesListScreen extends StatefulWidget {
  const EmployeesListScreen({super.key});
  @override
  State<EmployeesListScreen> createState() => _EmployeesListScreenState();
}

class _EmployeesListScreenState extends State<EmployeesListScreen> {
  final ApiService _api = ApiService();
  List<Map<String, dynamic>> _employees = [];
  bool _isLoading = true;

  @override
  void initState() { super.initState(); _load(); }

  Future<void> _load() async {
    setState(() => _isLoading = true);
    try {
      final res = await _api.get('Employees/Employees_GetAll');
      if (res.data is List) {
        setState(() { _employees = (res.data as List).cast<Map<String, dynamic>>(); _isLoading = false; });
      } else { setState(() => _isLoading = false); }
    } catch (_) { setState(() => _isLoading = false); }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('الموظفين'),
        actions: [IconButton(icon: const Icon(Icons.refresh), onPressed: _load)],
      ),
      body: _isLoading
          ? const LoadingIndicator()
          : _employees.isEmpty
              ? const EmptyState(message: 'لا يوجد موظفين')
              : Column(children: [
                  SizedBox(
                    height: 80,
                    child: ListView(scrollDirection: Axis.horizontal, padding: const EdgeInsets.symmetric(horizontal: 8), children: [
                      ModernStatCard.compact(title: 'عدد الموظفين', value: Formatters.formatNumber(_employees.length), icon: Icons.people, color: 'primary'),
                      ModernStatCard.compact(title: 'الرصيد', value: Formatters.formatCurrency(_employees.fold(0.0, (s, i) => s + (i['amountDenar'] ?? 0).toDouble())), icon: Icons.account_balance_wallet, color: 'success'),
                    ]),
                  ),
                  Expanded(child: ListView.builder(
                    padding: const EdgeInsets.all(8),
                    itemCount: _employees.length,
                    itemBuilder: (_, i) {
                      final e = _employees[i];
                      return Card(
                        margin: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                        child: ListTile(
                          leading: CircleAvatar(backgroundColor: const Color(0xFF36D1DC).withAlpha(51), child: const Icon(Icons.person, color: Color(0xFF36D1DC))),
                          title: Text(e['employeeName'] ?? '', style: const TextStyle(fontWeight: FontWeight.bold)),
                          subtitle: Text('${e['phoneNumber'] ?? '-'} | ${Formatters.formatCurrency(e['amountDenar'])}', style: const TextStyle(fontSize: 12)),
                        ),
                      );
                    },
                  )),
                ]),
    );
  }
}
