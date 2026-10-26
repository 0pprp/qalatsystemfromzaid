import 'package:flutter/material.dart';
import 'package:sales_employee_application/services/api_client.dart';
import 'package:sales_employee_application/utils/app_theme.dart';

/// After a successful sale complete: rate مندوب القائمة once per list per Iraq day (0–10).
/// Mandatory — no dismiss / «لاحقاً»; loops until save succeeds.
Future<void> promptListDelegateRatingAfterSale(
  BuildContext context, {
  required int? listId,
  required String listName,
}) async {
  final id = listId ?? 0;
  if (id <= 0 || !context.mounted) return;

  try {
    final raw = await ApiClient.get(
      'sales/list-delegate-ratings/status',
      query: {'listId': '$id'},
    );
    if (raw is! Map) return;
    final already = raw['alreadyRated'] == true || raw['AlreadyRated'] == true;
    if (already) return;
    final name = '${raw['listName'] ?? raw['ListName'] ?? listName}'.trim();
    if (!context.mounted) return;
    await _promptUntilSaved(
      context,
      listId: id,
      listName: name.isEmpty ? 'مندوب القائمة' : name,
    );
  } catch (_) {
    // Soft-fail only if status check itself fails (network).
  }
}

Future<void> _promptUntilSaved(
  BuildContext context, {
  required int listId,
  required String listName,
}) async {
  while (context.mounted) {
    final result = await _showMandatoryDialog(context, listName: listName);
    if (result == null || !context.mounted) continue;

    try {
      await ApiClient.post('sales/list-delegate-ratings', body: {
        'listId': listId,
        'score': result.score,
        if (result.note.trim().isNotEmpty) 'note': result.note.trim(),
      });
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('تم حفظ تقييم مندوب القائمة',
                style: TextStyle(fontFamily: 'Cairo')),
          ),
        );
      }
      return;
    } on ApiException catch (e) {
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
              content: Text(e.message, style: const TextStyle(fontFamily: 'Cairo'))),
        );
      }
    } catch (_) {
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('تعذر حفظ التقييم — أعد المحاولة',
                style: TextStyle(fontFamily: 'Cairo')),
          ),
        );
      }
    }
  }
}

class _RatingResult {
  const _RatingResult({required this.score, required this.note});
  final int score;
  final String note;
}

Future<_RatingResult?> _showMandatoryDialog(
  BuildContext context, {
  required String listName,
}) async {
  int score = 5;
  final noteCtrl = TextEditingController();
  try {
    return await showDialog<_RatingResult>(
      context: context,
      barrierDismissible: false,
      builder: (ctx) {
        return PopScope(
          canPop: false,
          child: Directionality(
            textDirection: TextDirection.rtl,
            child: StatefulBuilder(
              builder: (ctx, setLocal) {
                return AlertDialog(
                  title: Text(
                    'تقييم مندوب القائمة — $listName',
                    style: const TextStyle(
                      fontFamily: 'Cairo',
                      fontWeight: FontWeight.bold,
                      fontSize: 16,
                    ),
                  ),
                  content: SingleChildScrollView(
                    child: Column(
                      mainAxisSize: MainAxisSize.min,
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        const Text(
                          'التقييم إجباري — قيّم مندوب هذه القائمة من 0 إلى 10 (مرة واحدة لكل قائمة في اليوم).',
                          style: TextStyle(fontFamily: 'Cairo', fontSize: 13),
                        ),
                        const SizedBox(height: 14),
                        Text(
                          'التقييم: $score',
                          textAlign: TextAlign.center,
                          style: const TextStyle(
                            fontFamily: 'Cairo',
                            fontWeight: FontWeight.bold,
                            fontSize: 18,
                            color: AppTheme.primaryColor,
                          ),
                        ),
                        Slider(
                          value: score.toDouble(),
                          min: 0,
                          max: 10,
                          divisions: 10,
                          label: '$score',
                          activeColor: AppTheme.primaryColor,
                          onChanged: (v) => setLocal(() => score = v.round()),
                        ),
                        TextField(
                          controller: noteCtrl,
                          maxLines: 2,
                          decoration: const InputDecoration(
                            labelText: 'ملاحظة (اختياري)',
                            labelStyle: TextStyle(fontFamily: 'Cairo'),
                          ),
                          style: const TextStyle(fontFamily: 'Cairo'),
                        ),
                      ],
                    ),
                  ),
                  actions: [
                    FilledButton(
                      onPressed: () => Navigator.pop(
                        ctx,
                        _RatingResult(score: score, note: noteCtrl.text),
                      ),
                      child: const Text('حفظ',
                          style: TextStyle(fontFamily: 'Cairo')),
                    ),
                  ],
                );
              },
            ),
          ),
        );
      },
    );
  } finally {
    noteCtrl.dispose();
  }
}
