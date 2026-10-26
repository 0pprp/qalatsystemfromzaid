import 'package:delegated_manager_application/core/theme/app_theme.dart';
import 'package:delegated_manager_application/features/assigned_tasks/domain/assigned_task.dart';
import 'package:flutter/material.dart';

/// Action buttons gated by [AssignedTaskActions] for the current status.
class AssignedTaskActionsBar extends StatelessWidget {
  const AssignedTaskActionsBar({
    super.key,
    required this.actions,
    this.busy = false,
    this.onAccept,
    this.onReject,
    this.onTransfer,
    this.onComplete,
    this.onConfirm,
  });

  final AssignedTaskActions actions;
  final bool busy;
  final VoidCallback? onAccept;
  final VoidCallback? onReject;
  final VoidCallback? onTransfer;
  final VoidCallback? onComplete;
  final VoidCallback? onConfirm;

  @override
  Widget build(BuildContext context) {
    if (!actions.hasAny) return const SizedBox.shrink();

    final buttons = <Widget>[];
    if (actions.canAccept) {
      buttons.add(
        FilledButton(
          onPressed: busy ? null : onAccept,
          child: const Text('قبول'),
        ),
      );
    }
    if (actions.canReject) {
      buttons.add(
        OutlinedButton(
          onPressed: busy ? null : onReject,
          style: OutlinedButton.styleFrom(foregroundColor: AppColors.rejected),
          child: const Text('رفض'),
        ),
      );
    }
    if (actions.canTransfer) {
      buttons.add(
        OutlinedButton(
          onPressed: busy ? null : onTransfer,
          child: const Text('نقل'),
        ),
      );
    }
    if (actions.canComplete) {
      buttons.add(
        FilledButton(
          onPressed: busy ? null : onComplete,
          child: const Text('تم الإنجاز'),
        ),
      );
    }
    if (actions.canConfirm) {
      buttons.add(
        FilledButton(
          onPressed: busy ? null : onConfirm,
          child: const Text('تصديق'),
        ),
      );
    }

    return Wrap(
      spacing: AppSpacing.sm,
      runSpacing: AppSpacing.sm,
      children: buttons,
    );
  }
}
