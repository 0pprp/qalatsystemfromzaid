import 'package:branch_manager_application/core/api.dart';
import 'package:branch_manager_application/theme/bm_theme.dart';
import 'package:flutter/material.dart';
import 'package:intl/intl.dart' hide TextDirection;

class NewSaleDetailPage extends StatefulWidget {
  const NewSaleDetailPage({super.key, required this.saleId, this.seed});

  final int saleId;
  final Map<String, dynamic>? seed;

  @override
  State<NewSaleDetailPage> createState() => _NewSaleDetailPageState();
}

class _NewSaleDetailPageState extends State<NewSaleDetailPage> {
  final _money = NumberFormat('#,##0', 'en');
  bool _loading = true;
  String? _error;
  Map<String, dynamic>? _sale;

  @override
  void initState() {
    super.initState();
    _sale = widget.seed == null ? null : Map<String, dynamic>.from(widget.seed!);
    _load();
  }

  String _s(Map? m, List<String> keys) {
    if (m == null) return '';
    for (final k in keys) {
      final v = m[k];
      if (v != null && '$v'.trim().isNotEmpty) return '$v'.trim();
    }
    return '';
  }

  double _d(Map? m, List<String> keys) {
    if (m == null) return 0;
    for (final k in keys) {
      final v = m[k];
      if (v is num) return v.toDouble();
      final p = double.tryParse('$v');
      if (p != null) return p;
    }
    return 0;
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final detail = await BmApi.instance.newSaleDetail(widget.saleId);
      if (!mounted) return;
      setState(() {
        _sale = detail ?? _sale;
        _loading = false;
      });
      final readAt = _s(_sale, ['branchManagerReadAtUtc', 'BranchManagerReadAtUtc']);
      if (readAt.isEmpty) {
        await BmApi.instance.markNewSaleRead(widget.saleId);
      }
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _loading = false;
        _error = '$e';
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final name = _s(_sale, ['fullName', 'FullName']);
    final title = name.isEmpty ? 'تفاصيل المبيع' : name;
    final items = (_sale?['items'] ?? _sale?['Items']) as List? ?? const [];

    return Directionality(
      textDirection: TextDirection.rtl,
      child: Scaffold(
        backgroundColor: BmTheme.surface,
        appBar: AppBar(title: Text(title)),
        body: _loading && _sale == null
            ? const Center(child: CircularProgressIndicator())
            : _error != null && _sale == null
                ? Center(child: Text(_error!, style: const TextStyle(color: BmTheme.danger)))
                : ListView(
                    padding: const EdgeInsets.all(16),
                    children: [
                      if (_loading) const LinearProgressIndicator(minHeight: 2),
                      _tile('الموظف', _s(_sale, ['employeeName', 'EmployeeName'])),
                      _tile('الهاتف', _s(_sale, ['phone', 'Phone'])),
                      _tile('المحافظة', _s(_sale, ['province', 'Province'])),
                      _tile('العنوان', _s(_sale, ['address', 'Address'])),
                      _tile('أقرب نقطة', _s(_sale, ['nearestLandmark', 'NearestLandmark'])),
                      _tile(
                        'سعر البيع',
                        '${_money.format(_d(_sale, ['finalSalePrice', 'FinalSalePrice']))} دع',
                      ),
                      _tile(
                        'القسط اليومي',
                        '${_money.format(_d(_sale, ['dailyInstallment', 'DailyInstallment']))} دع',
                      ),
                      _tile(
                        'الدفعة الأولى',
                        '${_money.format(_d(_sale, ['downPayment', 'DownPayment']))} دع',
                      ),
                      _tile('التقييم', _s(_sale, ['evaluationLevel', 'EvaluationLevel'])),
                      _tile('ملاحظة التقييم', _s(_sale, ['evaluationNote', 'EvaluationNote'])),
                      const SizedBox(height: 16),
                      const Text(
                        'المواد',
                        style: TextStyle(
                          fontFamily: 'Cairo',
                          fontWeight: FontWeight.bold,
                          fontSize: 16,
                        ),
                      ),
                      const SizedBox(height: 8),
                      if (items.isEmpty)
                        const Text('لا توجد مواد',
                            style: TextStyle(fontFamily: 'Cairo', color: BmTheme.muted))
                      else
                        ...items.map((raw) {
                          final m = Map<String, dynamic>.from(raw as Map);
                          final pname = _s(m, ['productName', 'ProductName']);
                          final qty = _d(m, ['quantity', 'Quantity']);
                          final line = _d(m, ['lineSalePrice', 'LineSalePrice']);
                          return Container(
                            margin: const EdgeInsets.only(bottom: 8),
                            padding: const EdgeInsets.all(12),
                            decoration: BoxDecoration(
                              color: BmTheme.card,
                              borderRadius: BorderRadius.circular(12),
                              border: Border.all(
                                  color: BmTheme.primary.withValues(alpha: 0.08)),
                            ),
                            child: Row(
                              children: [
                                Expanded(
                                  child: Text(
                                    pname.isEmpty ? 'مادة' : pname,
                                    style: const TextStyle(
                                        fontFamily: 'Cairo',
                                        fontWeight: FontWeight.w600),
                                  ),
                                ),
                                Text(
                                  '×${qty.toStringAsFixed(0)} · ${_money.format(line)}',
                                  style: const TextStyle(
                                      fontFamily: 'Cairo', fontSize: 12),
                                ),
                              ],
                            ),
                          );
                        }),
                    ],
                  ),
      ),
    );
  }

  Widget _tile(String label, String value) {
    if (value.isEmpty) return const SizedBox.shrink();
    return Padding(
      padding: const EdgeInsets.only(bottom: 10),
      child: Container(
        width: double.infinity,
        padding: const EdgeInsets.all(12),
        decoration: BoxDecoration(
          color: BmTheme.card,
          borderRadius: BorderRadius.circular(12),
          border: Border.all(color: BmTheme.primary.withValues(alpha: 0.08)),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(label,
                style: const TextStyle(
                    fontFamily: 'Cairo', fontSize: 12, color: BmTheme.muted)),
            const SizedBox(height: 4),
            Text(value,
                style: const TextStyle(
                    fontFamily: 'Cairo', fontWeight: FontWeight.w600, height: 1.4)),
          ],
        ),
      ),
    );
  }
}
