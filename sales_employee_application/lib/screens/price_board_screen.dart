import 'package:sales_employee_application/widgets/company_loader.dart';
import 'package:flutter/material.dart';
import 'package:sales_employee_application/services/api_client.dart';
import 'package:sales_employee_application/utils/app_theme.dart';
import 'package:sales_employee_application/utils/sales_format.dart';

class PriceBoardScreen extends StatefulWidget {
  const PriceBoardScreen({super.key});

  @override
  State<PriceBoardScreen> createState() => _PriceBoardScreenState();
}

class _PriceBoardScreenState extends State<PriceBoardScreen> {
  List<Map<String, dynamic>> _rows = [];
  bool _loading = true;
  String? _error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final raw = await ApiClient.get('sales/price-board');
      final list = raw is List
          ? raw
              .whereType<Map>()
              .map((e) => Map<String, dynamic>.from(e))
              .toList()
          : <Map<String, dynamic>>[];
      if (!mounted) return;
      setState(() {
        _rows = list;
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
        _error = 'تعذر تحميل لوحة الأسعار';
      });
    }
  }

  String _money(dynamic v) {
    if (v == null) return '—';
    final n = num.tryParse('$v');
    if (n == null) return '$v';
    return '${MoneyFormat.grouped(n)} دع';
  }

  String _str(Map<String, dynamic> row, List<String> keys) {
    for (final k in keys) {
      final v = row[k];
      if (v != null && '$v'.trim().isNotEmpty) return '$v'.trim();
    }
    return '';
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('لوحة الأسعار'),
        actions: [
          IconButton(onPressed: _load, icon: const Icon(Icons.refresh)),
        ],
      ),
      body: SafeArea(
        child: _loading
            ? const Center(child: CompanyLoader(label: 'جارٍ تحميل البيانات'))
            : _error != null
                ? Center(
                    child: Text(_error!, style: const TextStyle(color: AppColors.danger)),
                  )
                : _rows.isEmpty
                    ? const Center(
                        child: Text('لا توجد أسعار لهذه المحافظة',
                            style: TextStyle(color: AppColors.muted)),
                      )
                    : RefreshIndicator(
                        onRefresh: _load,
                        child: ListView.separated(
                          padding: const EdgeInsets.all(AppSpacing.md),
                          itemCount: _rows.length,
                          separatorBuilder: (_, __) => const SizedBox(height: 8),
                          itemBuilder: (context, i) {
                            final row = _rows[i];
                            final name = _str(row, ['productName', 'ProductName']);
                            final city = _str(
                                row, ['targetCityName', 'TargetCityName', 'targetCityValue']);
                            return Card(
                              child: ListTile(
                                title: Text(
                                  name.isEmpty ? '—' : name,
                                  style: const TextStyle(fontWeight: FontWeight.w700),
                                ),
                                subtitle: Text(
                                  [
                                    'سعر البيع: ${_money(row['salePrice'] ?? row['SalePrice'])}',
                                    'القسط اليومي: ${_money(row['dailyInstallment'] ?? row['DailyInstallment'])}',
                                    'المقدمة: ${_money(row['downPayment'] ?? row['DownPayment'])}',
                                    if (city.isNotEmpty) 'المحافظة: $city',
                                  ].join('\n'),
                                ),
                                isThreeLine: true,
                              ),
                            );
                          },
                        ),
                      ),
      ),
    );
  }
}
