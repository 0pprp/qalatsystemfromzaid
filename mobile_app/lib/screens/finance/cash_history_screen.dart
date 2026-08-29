import 'package:flutter/material.dart';
import '../../services/api_service.dart';
import '../../utils/formatters.dart';
import '../../widgets/modern_stat_card.dart';
import '../../widgets/states.dart';

class CashHistoryScreen extends StatefulWidget {
  final String title;
  final String listEndpoint;
  final String idKey;
  final String amountKey;
  final String dateKey;
  final String? boxNameKey;
  final String? extraField;
  final String? extraFieldKey;
  final IconData icon;

  const CashHistoryScreen({
    super.key,
    required this.title,
    required this.listEndpoint,
    this.idKey = 'addToBoxID',
    this.amountKey = 'amountDenar',
    this.dateKey = 'dateCreate',
    this.boxNameKey = 'boxName',
    this.extraField,
    this.extraFieldKey,
    this.icon = Icons.history,
  });

  @override
  State<CashHistoryScreen> createState() => _CashHistoryScreenState();
}

class _CashHistoryScreenState extends State<CashHistoryScreen> {
  final ApiService _api = ApiService();
  List<Map<String, dynamic>> _items = [];
  List<Map<String, dynamic>> _boxes = [];
  bool _isLoading = true;
  DateTime _from = DateTime.now().subtract(const Duration(days: 30));
  DateTime _to = DateTime.now().add(const Duration(days: 1));
  int? _boxID;

  @override
  void initState() {
    super.initState();
    _loadBoxes();
    _load();
  }

  Future<void> _loadBoxes() async {
    try {
      final r = await _api.get('Accounts/Boxs_GetAllData');
      if (r.data is List) _boxes = (r.data as List).cast<Map<String, dynamic>>();
    } catch (_) {}
  }

  Future<void> _load() async {
    setState(() => _isLoading = true);
    try {
      final from = Formatters.formatDate(_from.toIso8601String().split('T')[0]);
      final to = Formatters.formatDate(_to.toIso8601String().split('T')[0]);
      final boxParam = _boxID?.toString() ?? 'null';
      final endpoint = widget.listEndpoint.replaceAll('{from}', from).replaceAll('{to}', to).replaceAll('{boxID}', boxParam);
      final res = await _api.get(endpoint);
      if (res.data is List) {
        setState(() {
          _items = (res.data as List).cast<Map<String, dynamic>>();
          _isLoading = false;
        });
      } else {
        setState(() => _isLoading = false);
      }
    } catch (_) {
      setState(() => _isLoading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final total = _items.fold(0.0, (s, i) => s + (i[widget.amountKey] ?? 0).toDouble());
    return Scaffold(
      appBar: AppBar(
        title: Text(widget.title),
        actions: [
          IconButton(
            icon: const Icon(Icons.date_range),
            onPressed: () async {
              final r = await showDateRangePicker(
                context: context,
                firstDate: DateTime(2020),
                lastDate: DateTime.now().add(const Duration(days: 365)),
                initialDateRange: DateTimeRange(start: _from, end: _to),
              );
              if (r != null) {
                _from = r.start;
                _to = r.end;
                _load();
              }
            },
          ),
          IconButton(icon: const Icon(Icons.refresh), onPressed: _load),
        ],
      ),
      body: _isLoading
          ? const LoadingIndicator()
          : _items.isEmpty
              ? const EmptyState(message: 'لا توجد بيانات')
              : Column(
                  children: [
                    if (_boxes.isNotEmpty)
                      Padding(
                        padding: const EdgeInsets.symmetric(horizontal: 12),
                        child: DropdownButtonFormField<int?>(
                          value: _boxID,
                          decoration: const InputDecoration(labelText: 'الصندوق'),
                          items: [
                            const DropdownMenuItem(value: null, child: Text('الجميع')),
                            ..._boxes.map((b) => DropdownMenuItem(value: b['boxID'] as int?, child: Text((b['boxName'] ?? '').toString()))),
                          ],
                          onChanged: (v) {
                            _boxID = v;
                            _load();
                          },
                        ),
                      ),
                    SizedBox(
                      height: 90,
                      child: ListView(
                        scrollDirection: Axis.horizontal,
                        padding: const EdgeInsets.symmetric(horizontal: 8),
                        children: [
                          ModernStatCard.compact(title: 'العدد', value: Formatters.formatNumber(_items.length), icon: Icons.receipt, color: 'primary'),
                          ModernStatCard.compact(title: 'المبلغ', value: Formatters.formatCurrency(total), icon: Icons.attach_money, color: 'success'),
                        ],
                      ),
                    ),
                    Expanded(child: _buildList()),
                  ],
                ),
    );
  }

  Widget _buildList() {
    return ListView.builder(
      padding: const EdgeInsets.all(8),
      itemCount: _items.length,
      itemBuilder: (_, i) {
        final it = _items[i];
        return Card(
          margin: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
          child: ListTile(
            leading: CircleAvatar(backgroundColor: const Color(0xFF1e5799).withAlpha(51), child: Icon(widget.icon, color: const Color(0xFF1e5799))),
            title: Text(Formatters.formatCurrency(it[widget.amountKey]), style: const TextStyle(fontWeight: FontWeight.bold)),
            subtitle: Text('${it[widget.boxNameKey] ?? ''} | ${Formatters.formatDate(it[widget.dateKey]?.toString())}', style: const TextStyle(fontSize: 12)),
          ),
        );
      },
    );
  }
}
