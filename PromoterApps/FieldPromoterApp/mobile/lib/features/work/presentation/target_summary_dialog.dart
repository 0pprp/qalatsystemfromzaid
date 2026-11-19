import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/theme/app_theme.dart';
import '../../../core/utils/formatters.dart';
import '../../auth/data/auth_controller.dart';
import '../data/work_repository.dart';

bool _checking = false;

/// Shows «حققت هدفك اليومي» with that day's numbers when the agent reached the daily target and has not seen it yet.
/// Called when the app opens, comes back to the foreground, or the notification arrives while it is open. A summary
/// first seen on a later day keeps the date of the day it belongs to. Closing it marks it as seen.
Future<void> maybeShowTargetSummary(BuildContext context, WidgetRef ref) async {
  if (_checking || !ref.read(isLoggedInProvider)) return;
  _checking = true;
  try {
    final repo = ref.read(workRepositoryProvider);
    final summary = await repo.targetSummary();
    if (summary == null || !context.mounted) return;
    await showDialog<void>(
      context: context,
      useRootNavigator: true,
      builder: (_) => TargetSummaryDialog(summary: summary),
    );
    try {
      await repo.markTargetSummarySeen(summary.id);
    } catch (_) {
      // Offline: it shows again next time, which is fine.
    }
  } catch (_) {
    // No connection or an older server: nothing to show.
  } finally {
    _checking = false;
  }
}

class TargetSummaryDialog extends StatelessWidget {
  const TargetSummaryDialog({super.key, required this.summary});

  final TargetSummary summary;

  @override
  Widget build(BuildContext context) {
    final s = summary;
    final day = s.day;
    final dateText = day == null ? s.date : Fmt.date(day);
    final label = s.isToday
        ? 'اليوم'
        : s.isYesterday
            ? 'أمس'
            : null;
    return AlertDialog(
      contentPadding: const EdgeInsets.fromLTRB(20, 24, 20, 8),
      content: SingleChildScrollView(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Container(
              width: 72,
              height: 72,
              decoration: const BoxDecoration(color: AppColors.primaryLight, shape: BoxShape.circle),
              child: const Icon(Icons.emoji_events_rounded, size: 40, color: AppColors.accent),
            ),
            const SizedBox(height: 14),
            Text(
              s.isToday ? 'أحسنت! حققت هدفك اليومي' : 'حققت هدفك اليومي',
              textAlign: TextAlign.center,
              style: const TextStyle(fontSize: 19, fontWeight: FontWeight.w800, color: AppColors.primaryDark),
            ),
            const SizedBox(height: 6),
            Text(
              label == null ? dateText : '$label · $dateText',
              textAlign: TextAlign.center,
              style: const TextStyle(color: AppColors.textMuted, fontWeight: FontWeight.w600),
            ),
            const SizedBox(height: 18),
            const Align(
              alignment: AlignmentDirectional.centerStart,
              child: Text('ملخص اليوم', style: TextStyle(fontWeight: FontWeight.w700)),
            ),
            const SizedBox(height: 8),
            _Row(icon: Icons.storefront_rounded, color: AppColors.primary, label: 'المحلات التي زرتها', value: s.stores),
            _Row(icon: Icons.receipt_long_rounded, color: AppColors.info, label: 'طلبات البيع', value: s.orders),
            _Row(icon: Icons.verified_rounded, color: AppColors.success, label: 'المبيعات', value: s.sales),
          ],
        ),
      ),
      actionsAlignment: MainAxisAlignment.center,
      actions: [
        FilledButton(onPressed: () => Navigator.of(context).pop(), child: const Text('حسناً')),
      ],
    );
  }
}

class _Row extends StatelessWidget {
  const _Row({required this.icon, required this.color, required this.label, required this.value});

  final IconData icon;
  final Color color;
  final String label;
  final int value;

  @override
  Widget build(BuildContext context) {
    return Container(
      margin: const EdgeInsets.only(bottom: 8),
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
      decoration: BoxDecoration(
        color: AppColors.background,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: AppColors.border),
      ),
      child: Row(
        children: [
          Icon(icon, color: color, size: 22),
          const SizedBox(width: 10),
          Expanded(child: Text(label, style: const TextStyle(fontWeight: FontWeight.w600))),
          Text(Fmt.number(value), style: TextStyle(fontSize: 18, fontWeight: FontWeight.w800, color: color)),
        ],
      ),
    );
  }
}
