import 'package:flutter/material.dart';
import 'package:main_follower_application/core/ui/safe_dialogs.dart';
import 'package:main_follower_application/domain/sale_review_rules.dart';
import 'package:main_follower_application/domain/staff_member.dart';

Future<bool> showStaffReviewDialog(
  BuildContext context, {
  required StaffMember member,
  required Future<void> Function(int score, String? note) onSubmit,
}) async {
  final noteCtrl = TextEditingController();
  var score = 7;
  String? error;
  final ok = await showSafeDialog<bool>(
    context: context,
    builder: (ctx) => StatefulBuilder(
      builder: (ctx, setLocal) => AlertDialog(
        title: Text('تقييم ${member.name}'),
        content: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Text(_typeLabel(member.targetType)),
              const SizedBox(height: 12),
              DropdownButtonFormField<int>(
                value: score,
                decoration: const InputDecoration(
                  labelText: 'التقييم من 10',
                  border: OutlineInputBorder(),
                ),
                items: [
                  for (var i = 1; i <= 10; i++)
                    DropdownMenuItem(value: i, child: Text('$i')),
                ],
                onChanged: (v) => setLocal(() => score = v ?? score),
              ),
              const SizedBox(height: 12),
              TextField(
                controller: noteCtrl,
                maxLines: 3,
                decoration: InputDecoration(
                  labelText: score >= 7 ? 'ملاحظة (اختيارية)' : 'ملاحظة (إجبارية)',
                  border: const OutlineInputBorder(),
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
              final err = SaleReviewRules.validate(score: score, note: noteCtrl.text);
              if (err != null) {
                setLocal(() => error = err);
                return;
              }
              try {
                await onSubmit(
                  score,
                  noteCtrl.text.trim().isEmpty ? null : noteCtrl.text.trim(),
                );
                if (ctx.mounted) Navigator.pop(ctx, true);
              } catch (e) {
                setLocal(() => error = e.toString());
              }
            },
            child: const Text('حفظ'),
          ),
        ],
      ),
    ),
  );
  noteCtrl.dispose();
  return ok == true;
}

String _typeLabel(String type) => switch (type) {
      'Delegate' => 'مندوب قائمة',
      'SubFollower' => 'متابع فرعي',
      'SalesEmployee' => 'موظف مبيعات',
      _ => type,
    };
