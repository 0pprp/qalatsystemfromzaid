import 'package:flutter/material.dart';
import 'package:sales_employee_application/data/sales_models.dart';
import 'package:sales_employee_application/data/sales_repository_factory.dart';
import 'package:sales_employee_application/services/api_client.dart';
import 'package:sales_employee_application/utils/app_theme.dart';
import 'package:sales_employee_application/utils/sales_format.dart';

class WarehouseScreen extends StatefulWidget {
  const WarehouseScreen({super.key});

  @override
  State<WarehouseScreen> createState() => _WarehouseScreenState();
}

class _WarehouseScreenState extends State<WarehouseScreen> {
  final _query = TextEditingController();
  List<SalesInventoryItem> _all = [];
  bool _loading = true;
  String? _error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    _query.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final rows = await SalesRepositoryFactory.instance.inventory();
      if (!mounted) return;
      setState(() {
        _all = rows;
        _loading = false;
      });
    } on ApiException catch (e) {
      if (!mounted) return;
      setState(() {
        _loading = false;
        _error = e.message;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _loading = false;
        _error = 'تعذر تحميل المخزن';
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final q = _query.text.trim();
    final rows = q.isEmpty
        ? _all
        : _all.where((i) => i.productName.contains(q) || (i.notes ?? '').contains(q)).toList();
    return Scaffold(
      appBar: AppBar(title: const Text('مخزن الفرع')),
      body: Column(
        children: [
          Padding(
            padding: const EdgeInsets.all(AppSpacing.md),
            child: TextField(
              controller: _query,
              onChanged: (_) => setState(() {}),
              decoration: const InputDecoration(
                hintText: 'بحث محلي في المواد',
                prefixIcon: Icon(Icons.search),
              ),
            ),
          ),
          if (_loading) const LinearProgressIndicator(minHeight: 2),
          Expanded(
            child: _error != null
                ? Center(child: Text(_error!, style: const TextStyle(color: AppColors.danger)))
                : rows.isEmpty && !_loading
                    ? const Center(child: Text('لا توجد مواد', style: TextStyle(color: AppColors.muted)))
                    : ListView.builder(
                        padding: const EdgeInsets.all(AppSpacing.md),
                        itemCount: rows.length,
                        itemBuilder: (context, i) {
                          final item = rows[i];
                          final empty = item.availableQuantity <= 0;
                          return Card(
                            child: ListTile(
                              title: Text(item.productName,
                                  style: const TextStyle(fontWeight: FontWeight.w700)),
                              subtitle: Text(
                                '${MoneyFormat.iqd(item.salePrice)}\n${empty ? 'غير متوفر' : 'المتوفر: ${item.availableQuantity}'}${item.notes == null || item.notes!.isEmpty ? '' : '\n${item.notes}'}',
                              ),
                              isThreeLine: true,
                            ),
                          );
                        },
                      ),
          ),
        ],
      ),
    );
  }
}
