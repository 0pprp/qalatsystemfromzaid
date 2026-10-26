import 'package:flutter/material.dart';
import 'company_loader.dart';
import 'package:sales_employee_application/services/api_client.dart';
import 'package:sales_employee_application/utils/app_theme.dart';
import 'package:sales_employee_application/utils/sales_format.dart';

/// Compact price-board container for the sales-employee home (above search).
class HomePriceBoardBanner extends StatefulWidget {
  const HomePriceBoardBanner({super.key});

  @override
  State<HomePriceBoardBanner> createState() => _HomePriceBoardBannerState();
}

class _HomePriceBoardBannerState extends State<HomePriceBoardBanner> {
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
    return Container(
      width: double.infinity,
      decoration: BoxDecoration(
        color: AppColors.darkGreen.withValues(alpha: 0.06),
        borderRadius: BorderRadius.circular(AppRadius.md),
        border: Border.all(color: AppColors.darkGreen.withValues(alpha: 0.22)),
      ),
      padding: const EdgeInsets.fromLTRB(12, 10, 12, 10),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            children: [
              const Icon(Icons.sell_outlined, size: 18, color: AppColors.darkGreen),
              const SizedBox(width: 6),
              const Expanded(
                child: Text(
                  'لوحة الأسعار',
                  style: TextStyle(
                    fontWeight: FontWeight.w800,
                    color: AppColors.darkGreen,
                    fontSize: 14,
                  ),
                ),
              ),
              IconButton(
                tooltip: 'تحديث',
                visualDensity: VisualDensity.compact,
                onPressed: _loading ? null : _load,
                icon: const Icon(Icons.refresh, size: 18),
              ),
              TextButton(
                onPressed: () => Navigator.pushNamed(context, '/price-board'),
                child: const Text('عرض الكل'),
              ),
            ],
          ),
          if (_loading)
            const Padding(
              padding: EdgeInsets.symmetric(vertical: 12),
              child: Center(
                child: SizedBox(
                  width: 22,
                  height: 22,
                  child: CompanyLoader(size: 22),
                ),
              ),
            )
          else if (_error != null)
            Padding(
              padding: const EdgeInsets.only(bottom: 4),
              child: Text(
                _error!,
                style: const TextStyle(color: AppColors.danger, fontSize: 12),
              ),
            )
          else if (_rows.isEmpty)
            const Padding(
              padding: EdgeInsets.only(bottom: 4),
              child: Text(
                'لا توجد أسعار لهذه المحافظة حالياً',
                style: TextStyle(color: AppColors.muted, fontSize: 12),
              ),
            )
          else
            SizedBox(
              height: 96,
              child: ListView.separated(
                scrollDirection: Axis.horizontal,
                itemCount: _rows.length,
                separatorBuilder: (_, __) => const SizedBox(width: 8),
                itemBuilder: (context, i) {
                  final row = _rows[i];
                  final name = _str(row, ['productName', 'ProductName']);
                  return InkWell(
                    onTap: () => Navigator.pushNamed(context, '/price-board'),
                    borderRadius: BorderRadius.circular(AppRadius.sm),
                    child: Container(
                      width: 168,
                      padding: const EdgeInsets.all(10),
                      decoration: BoxDecoration(
                        color: Colors.white,
                        borderRadius: BorderRadius.circular(AppRadius.sm),
                        border: Border.all(
                          color: AppColors.darkGreen.withValues(alpha: 0.15),
                        ),
                      ),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            name.isEmpty ? '—' : name,
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                            style: const TextStyle(
                              fontWeight: FontWeight.w700,
                              fontSize: 13,
                            ),
                          ),
                          const Spacer(),
                          Text(
                            'البيع: ${_money(row['salePrice'] ?? row['SalePrice'])}',
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                            style: const TextStyle(fontSize: 11),
                          ),
                          Text(
                            'القسط: ${_money(row['dailyInstallment'] ?? row['DailyInstallment'])}',
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                            style: const TextStyle(
                              fontSize: 11,
                              color: AppColors.muted,
                            ),
                          ),
                        ],
                      ),
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
