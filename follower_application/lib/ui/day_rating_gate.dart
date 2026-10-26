import 'package:follower_application/services/follower_day_rating_service.dart';
import 'package:follower_application/utils/AppTheme.dart';
import 'package:flutter/material.dart';

/// Copy used when rating from «إنهاء الجباية» on a collection list.
const String endCollectionRatingHint =
    'قيّم أداء المندوب اليوم من 0 إلى 10 قبل إنهاء الجباية.';

const String _endShiftRatingHint =
    'قيّم أداء المندوب اليوم من 0 إلى 10 قبل إنهاء الدوام.';

/// Ensures all collection-enabled lists have today's 0–10 rating before end-of-day.
/// Returns true if the caller may proceed to end the shift.
Future<bool> ensureDayRatingsBeforeEndShift(BuildContext context) async {
  final missing = await FollowerDayRatingService.fetchMissing();
  if (missing.isEmpty) return true;
  if (!context.mounted) return false;

  for (final item in missing) {
    if (!context.mounted) return false;
    final ok = await promptDayRatingForList(
      context,
      listId: item.listId,
      listName: item.listName,
      hint: _endShiftRatingHint,
    );
    if (!ok) return false;
  }
  return true;
}

/// Opens the 0–10 day-rating dialog for one list (إنهاء الجباية).
/// Returns true after a successful save.
Future<bool> promptDayRatingForList(
  BuildContext context, {
  required int listId,
  required String listName,
  String hint = endCollectionRatingHint,
}) async {
  if (listId <= 0) return false;
  return _promptOne(
    context,
    FollowerDayRatingMissing(
      listId: listId,
      listName: listName.trim().isEmpty ? 'مندوب' : listName.trim(),
    ),
    hint: hint,
  );
}

/// Rate the current collection list, then pop the collection shell on success.
Future<bool> endCollectionWithDayRating(
  BuildContext context, {
  required int listId,
  required String listName,
}) async {
  final ok = await promptDayRatingForList(
    context,
    listId: listId,
    listName: listName,
    hint: endCollectionRatingHint,
  );
  if (!ok || !context.mounted) return false;
  Navigator.of(context).pop(true);
  return true;
}

Future<bool> _promptOne(
  BuildContext context,
  FollowerDayRatingMissing item, {
  required String hint,
}) async {
  int score = 5;
  final noteCtrl = TextEditingController();
  final submitted = await showDialog<bool>(
    context: context,
    barrierDismissible: false,
    builder: (ctx) {
      return Directionality(
        textDirection: TextDirection.rtl,
        child: StatefulBuilder(
          builder: (ctx, setLocal) {
            return AlertDialog(
              title: Text(
                'تقييم يوم الجباية — ${item.listName}',
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
                    Text(
                      hint,
                      style: const TextStyle(fontFamily: 'Cairo', fontSize: 13),
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
                TextButton(
                  onPressed: () => Navigator.pop(ctx, false),
                  child: const Text('إلغاء',
                      style: TextStyle(fontFamily: 'Cairo')),
                ),
                FilledButton(
                  onPressed: () => Navigator.pop(ctx, true),
                  child: const Text('حفظ',
                      style: TextStyle(fontFamily: 'Cairo')),
                ),
              ],
            );
          },
        ),
      );
    },
  );
  if (submitted != true) return false;

  final err = await FollowerDayRatingService.submit(
    listId: item.listId,
    score: score,
    note: noteCtrl.text,
  );
  if (err != null) {
    if (context.mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(err, style: const TextStyle(fontFamily: 'Cairo')),
        ),
      );
    }
    return false;
  }
  if (context.mounted) {
    ScaffoldMessenger.of(context).showSnackBar(
      const SnackBar(
        content: Text('تم حفظ تقييم القائمة',
            style: TextStyle(fontFamily: 'Cairo')),
      ),
    );
  }
  return true;
}
