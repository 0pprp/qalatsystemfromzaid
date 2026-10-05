import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:main_follower_application/core/ui/safe_dialogs.dart';
import 'package:main_follower_application/core/ui/thousand_separator_formatter.dart';
import 'package:main_follower_application/domain/branch_sale.dart';
import 'package:main_follower_application/domain/sale_review_rules.dart';

Future<bool> showSaleAuthDialog(
  BuildContext context, {
  required BranchSale sale,
  required Future<void> Function(
    int score,
    String? note,
    double shopStockEstimatedValue,
    double estimatedDailyRevenue,
  ) onSubmit,
}) async {
  final noteCtrl = TextEditingController();
  final stockCtrl = TextEditingController();
  final dailyCtrl = TextEditingController();
  var score = 7;
  String? error;
  final ok = await showSafeDialog<bool>(
    context: context,
    builder: (ctx) => StatefulBuilder(
      builder: (ctx, setLocal) => AlertDialog(
        title: const Text('مصادقة المبيع'),
        content: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(
                sale.customerName,
                style: const TextStyle(fontWeight: FontWeight.bold),
              ),
              Text('مبيع #${sale.saleId}'),
              const SizedBox(height: 12),
              Text('التقييم: $score'),
              Slider(
                value: score.toDouble(),
                min: 1,
                max: 10,
                divisions: 9,
                label: '$score',
                onChanged: (v) => setLocal(() => score = v.round()),
              ),
              Text(
                score >= 7
                    ? 'الملاحظة اختيارية'
                    : 'الملاحظة إجبارية (تقييم أقل من 7)',
                style: TextStyle(
                  fontSize: 12,
                  color: score >= 7 ? Colors.grey : Colors.red,
                ),
              ),
              const SizedBox(height: 8),
              TextField(
                controller: noteCtrl,
                maxLines: 3,
                decoration: const InputDecoration(
                  labelText: 'ملاحظة',
                  border: OutlineInputBorder(),
                ),
              ),
              const SizedBox(height: 8),
              TextField(
                controller: stockCtrl,
                keyboardType: TextInputType.number,
                inputFormatters: [
                  FilteringTextInputFormatter.digitsOnly,
                  ThousandSeparatorInputFormatter(),
                ],
                decoration: const InputDecoration(
                  labelText: 'قيمة بضاعة المحل *',
                  border: OutlineInputBorder(),
                ),
              ),
              const SizedBox(height: 8),
              TextField(
                controller: dailyCtrl,
                keyboardType: TextInputType.number,
                inputFormatters: [
                  FilteringTextInputFormatter.digitsOnly,
                  ThousandSeparatorInputFormatter(),
                ],
                decoration: const InputDecoration(
                  labelText: 'واردات المحل اليومية *',
                  border: OutlineInputBorder(),
                ),
              ),
              if (error != null) ...[
                const SizedBox(height: 8),
                Text(error!, style: const TextStyle(color: Colors.red)),
              ],
            ],
          ),
        ),
        actionsPadding: const EdgeInsets.fromLTRB(16, 0, 16, 12),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx, false), child: const Text('إلغاء')),
          FilledButton(
            onPressed: () async {
              final stock = ThousandSeparatorInputFormatter.parse(stockCtrl.text);
              final daily = ThousandSeparatorInputFormatter.parse(dailyCtrl.text);
              final err = SaleReviewRules.validate(
                score: score,
                note: noteCtrl.text,
                shopStockEstimatedValue: stock,
                estimatedDailyRevenue: daily,
              );
              if (err != null) {
                setLocal(() => error = err);
                return;
              }
              try {
                await onSubmit(
                  score,
                  noteCtrl.text.trim().isEmpty ? null : noteCtrl.text.trim(),
                  stock!,
                  daily!,
                );
                if (ctx.mounted) Navigator.pop(ctx, true);
              } catch (e) {
                setLocal(() => error = e.toString());
              }
            },
            child: const Text('اعتماد'),
          ),
        ],
      ),
    ),
  );
  noteCtrl.dispose();
  stockCtrl.dispose();
  dailyCtrl.dispose();
  return ok == true;
}

/// Backward-compatible alias.
Future<bool> showSaleReviewDialog(
  BuildContext context, {
  required BranchSale sale,
  required Future<void> Function(
    int score,
    String? note,
    double shopStockEstimatedValue,
    double estimatedDailyRevenue,
  ) onSubmit,
}) =>
    showSaleAuthDialog(context, sale: sale, onSubmit: onSubmit);
