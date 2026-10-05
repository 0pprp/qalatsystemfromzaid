import 'package:delegate_application/utils/AppTheme.dart';
import 'package:delegate_application/utils/thousands_separator_input_formatter.dart';
import 'package:flutter/material.dart';

/// Result of the same shop-evaluation UI used on new-sale requests.
class ShopEvaluationResult {
  const ShopEvaluationResult({
    required this.rating,
    this.note,
    required this.shopStockEstimatedValue,
    required this.estimatedDailyRevenue,
  });

  final int rating;
  final String? note;
  final double shopStockEstimatedValue;
  final double estimatedDailyRevenue;
}

/// Same fields as [DelegateSalesRequestPage] new-sale evaluation container.
Future<ShopEvaluationResult?> showShopEvaluationDialog(
  BuildContext context, {
  String title = 'تقييم المحل — المبيعات الجديدة',
}) {
  return showDialog<ShopEvaluationResult>(
    context: context,
    barrierDismissible: false,
    builder: (ctx) => _ShopEvaluationDialog(title: title),
  );
}

class _ShopEvaluationDialog extends StatefulWidget {
  const _ShopEvaluationDialog({required this.title});

  final String title;

  @override
  State<_ShopEvaluationDialog> createState() => _ShopEvaluationDialogState();
}

class _ShopEvaluationDialogState extends State<_ShopEvaluationDialog> {
  final _note = TextEditingController();
  final _stock = TextEditingController();
  final _daily = TextEditingController();
  int _rating = 7;
  String? _error;

  @override
  void dispose() {
    _note.dispose();
    _stock.dispose();
    _daily.dispose();
    super.dispose();
  }

  double? _parseMoney(String raw) =>
      ThousandsSeparatorInputFormatter.parseMoney(raw);

  void _submit() {
    if (_rating < 7 && _note.text.trim().isEmpty) {
      setState(() => _error = 'الملاحظة إجبارية عندما يكون تقييم المحل أقل من 7');
      return;
    }
    final stock = _parseMoney(_stock.text);
    if (stock == null || stock <= 0) {
      setState(() => _error = 'قيمة بضاعة المحل مطلوبة');
      return;
    }
    final daily = _parseMoney(_daily.text);
    if (daily == null || daily <= 0) {
      setState(() => _error = 'واردات المحل اليومية مطلوبة');
      return;
    }
    Navigator.pop(
      context,
      ShopEvaluationResult(
        rating: _rating,
        note: _note.text.trim().isEmpty ? null : _note.text.trim(),
        shopStockEstimatedValue: stock,
        estimatedDailyRevenue: daily,
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Directionality(
      textDirection: TextDirection.rtl,
      child: AlertDialog(
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
        title: Text(
          widget.title,
          style: const TextStyle(fontFamily: 'Cairo', fontWeight: FontWeight.bold),
          textAlign: TextAlign.right,
        ),
        content: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Text(
                _rating < 7
                    ? 'التقييم إجباري مع ملاحظة إجبارية'
                    : 'التقييم إجباري مع ملاحظة اختيارية',
                style: TextStyle(
                  fontFamily: 'Cairo',
                  fontSize: 12,
                  color: _rating < 7 ? Colors.orange.shade800 : Colors.grey,
                ),
                textAlign: TextAlign.right,
              ),
              const SizedBox(height: 8),
              Text(
                'التقييم: $_rating',
                style: const TextStyle(
                  fontFamily: 'Cairo',
                  fontWeight: FontWeight.w700,
                ),
                textAlign: TextAlign.right,
              ),
              Slider(
                value: _rating.toDouble(),
                min: 1,
                max: 10,
                divisions: 9,
                label: '$_rating',
                activeColor: AppTheme.primaryColor,
                onChanged: (v) => setState(() {
                  _rating = v.round();
                  _error = null;
                }),
              ),
              TextField(
                controller: _note,
                maxLines: 3,
                style: const TextStyle(fontFamily: 'Cairo'),
                decoration: InputDecoration(
                  labelText: _rating < 7 ? 'ملاحظة *' : 'ملاحظة (اختياري)',
                  labelStyle: const TextStyle(fontFamily: 'Cairo'),
                ),
                onChanged: (_) => setState(() => _error = null),
              ),
              TextField(
                controller: _stock,
                keyboardType: TextInputType.number,
                inputFormatters: [ThousandsSeparatorInputFormatter()],
                style: const TextStyle(fontFamily: 'Cairo'),
                decoration: const InputDecoration(
                  labelText: 'قيمة بضاعة المحل *',
                  labelStyle: TextStyle(fontFamily: 'Cairo'),
                ),
                onChanged: (_) => setState(() => _error = null),
              ),
              TextField(
                controller: _daily,
                keyboardType: TextInputType.number,
                inputFormatters: [ThousandsSeparatorInputFormatter()],
                style: const TextStyle(fontFamily: 'Cairo'),
                decoration: const InputDecoration(
                  labelText: 'واردات المحل اليومية *',
                  labelStyle: TextStyle(fontFamily: 'Cairo'),
                ),
                onChanged: (_) => setState(() => _error = null),
              ),
              if (_error != null) ...[
                const SizedBox(height: 8),
                Text(
                  _error!,
                  style: TextStyle(
                    fontFamily: 'Cairo',
                    color: Theme.of(context).colorScheme.error,
                    fontSize: 13,
                  ),
                  textAlign: TextAlign.right,
                ),
              ],
            ],
          ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context),
            child: Text(
              'إلغاء',
              style: TextStyle(
                fontFamily: 'Cairo',
                color: Theme.of(context).colorScheme.error,
              ),
            ),
          ),
          ElevatedButton(
            onPressed: _submit,
            style: ElevatedButton.styleFrom(
              backgroundColor: AppTheme.primaryColor,
            ),
            child: const Text(
              'متابعة',
              style: TextStyle(fontFamily: 'Cairo', color: Colors.white),
            ),
          ),
        ],
      ),
    );
  }
}
