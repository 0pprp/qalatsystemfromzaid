import 'package:flutter/material.dart';
import '../../services/api_service.dart';
import '../../utils/formatters.dart';
import '../../widgets/modern_stat_card.dart';
import '../../widgets/states.dart';
import 'cash_history_screen.dart';

class CashRegistersScreen extends StatefulWidget {
  const CashRegistersScreen({super.key});
  @override
  State<CashRegistersScreen> createState() => _CashRegistersScreenState();
}

class _CashRegistersScreenState extends State<CashRegistersScreen> {
  final ApiService _api = ApiService();
  List<Map<String, dynamic>> _boxes = [];
  bool _isLoading = true;

  @override
  void initState() { super.initState(); _load(); }

  Future<void> _load() async {
    setState(() => _isLoading = true);
    try {
      final res = await _api.get('Accounts/Boxs_GetAll');
      if (res.data is List) {
        setState(() { _boxes = (res.data as List).cast<Map<String, dynamic>>(); _isLoading = false; });
      } else { setState(() => _isLoading = false); }
    } catch (_) { setState(() => _isLoading = false); }
  }

  void _openHistory(String title, String listEndpoint, String idKey, String amountKey, String dateKey, IconData icon) {
    Navigator.push(context, MaterialPageRoute(builder: (_) => CashHistoryScreen(
      title: title, listEndpoint: listEndpoint, idKey: idKey, amountKey: amountKey, dateKey: dateKey, icon: icon,
    )));
  }

  @override
  Widget build(BuildContext context) {
    final totalAmount = _boxes.fold(0.0, (s, b) => s + (b['totalAmount'] ?? 0).toDouble());
    return Scaffold(
      appBar: AppBar(
        title: const Text('الخزائن'),
        actions: [IconButton(icon: const Icon(Icons.refresh), onPressed: _load)],
      ),
      body: _isLoading
          ? const LoadingIndicator()
          : _boxes.isEmpty
              ? const EmptyState(message: 'لا توجد خزائن')
              : Column(children: [
                  SizedBox(height: 80, child: ListView(scrollDirection: Axis.horizontal, padding: const EdgeInsets.symmetric(horizontal: 8), children: [
                    ModernStatCard.compact(title: 'عدد الخزائن', value: Formatters.formatNumber(_boxes.length), icon: Icons.account_balance, color: 'primary'),
                    ModernStatCard.compact(title: 'المبلغ الكلي', value: Formatters.formatCurrency(totalAmount), icon: Icons.attach_money, color: 'success'),
                  ])),
                  Expanded(child: ListView.builder(
                    padding: const EdgeInsets.all(8), itemCount: _boxes.length,
                    itemBuilder: (_, i) {
                      final b = _boxes[i];
                      final isActive = b['isActive'] == true || b['isActive'] == 'true';
                      return Card(margin: const EdgeInsets.symmetric(horizontal: 8, vertical: 4), child: ListTile(
                        leading: CircleAvatar(backgroundColor: (isActive ? Colors.green : Colors.grey).withAlpha(51), child: Icon(Icons.account_balance_wallet, color: isActive ? Colors.green : Colors.grey)),
                        title: Text(b['boxName'] ?? '', style: const TextStyle(fontWeight: FontWeight.bold)),
                        subtitle: Text(Formatters.formatCurrency(b['totalAmount']), style: const TextStyle(fontSize: 14, fontWeight: FontWeight.w600)),
                        trailing: PopupMenuButton<String>(
                          onSelected: (v) {
                            final boxID = b['boxID'];
                            final from = Formatters.formatDate(DateTime.now().subtract(const Duration(days: 365)).toIso8601String().split('T')[0]);
                            final to = Formatters.formatDate(DateTime.now().add(const Duration(days: 1)).toIso8601String().split('T')[0]);
                            switch (v) {
                              case 'deposits': _openHistory('الإضافات', 'Accounts/AddToBoxs_GetByDateByboxID/{from}&&{to}&&{boxID}', 'addToBoxID', 'amountDenar', 'dateCreate', Icons.add_circle_outline); break;
                              case 'withdrawals': _openHistory('السحوبات', 'Accounts/WithdrawalFromBoxs_GetByDateByboxID/{from}&&{to}&&{boxID}', 'withdrawalFromBoxID', 'amountDenar', 'dateCreate', Icons.remove_circle_outline); break;
                              case 'transfers': _openHistory('التحويلات', 'Accounts/TransferBoxs_GetByDate/{from}&&{to}', 'transferBoxID', 'amountDenar', 'dateCreate', Icons.swap_horiz); break;
                            }
                          },
                          itemBuilder: (_) => ['deposits', 'withdrawals', 'transfers'].map((v) {
                            final labels = {'deposits': 'الإضافات', 'withdrawals': 'السحوبات', 'transfers': 'التحويلات'};
                            final icons = {'deposits': Icons.add_circle_outline, 'withdrawals': Icons.remove_circle_outline, 'transfers': Icons.swap_horiz};
                            return PopupMenuItem(value: v, child: Row(children: [Icon(icons[v]!, size: 20), const SizedBox(width: 8), Text(labels[v]!)]));
                          }).toList(),
                        ),
                      ));
                    },
                  )),
                ]),
    );
  }
}
