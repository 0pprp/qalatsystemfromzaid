import 'package:delegated_manager_application/core/network/api_client.dart';
import 'package:delegated_manager_application/core/theme/app_theme.dart';
import 'package:delegated_manager_application/core/utils/date_format.dart';
import 'package:delegated_manager_application/core/widgets/state_views.dart';
import 'package:delegated_manager_application/features/assigned_tasks/data/assigned_tasks_repository.dart';
import 'package:delegated_manager_application/features/assigned_tasks/domain/assigned_task.dart';
import 'package:delegated_manager_application/features/assigned_tasks/presentation/widgets/assigned_task_actions_bar.dart';
import 'package:flutter/material.dart';

class AssignedTasksInboxScreen extends StatefulWidget {
  const AssignedTasksInboxScreen({super.key});

  @override
  State<AssignedTasksInboxScreen> createState() =>
      AssignedTasksInboxScreenState();
}

class AssignedTasksInboxScreenState extends State<AssignedTasksInboxScreen> {
  bool _loading = true;
  bool _acting = false;
  String? _error;
  List<AssignedTask> _items = [];

  @override
  void initState() {
    super.initState();
    reload();
  }

  Future<void> reload() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final rows = await AssignedTasksRepository.inbox();
      if (!mounted) return;
      setState(() {
        _items = rows;
        _loading = false;
      });
    } on ApiException catch (e) {
      if (!mounted) return;
      setState(() {
        _error = e.message;
        _loading = false;
      });
    }
  }

  Future<void> _runAction(Future<AssignedTask> Function() action) async {
    if (_acting) return;
    setState(() => _acting = true);
    try {
      await action();
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('تم تحديث المهمة')),
      );
      await reload();
    } on ApiException catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(e.message)),
      );
    } finally {
      if (mounted) setState(() => _acting = false);
    }
  }

  Future<void> _accept(AssignedTask task) async {
    final ok = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('قبول المهمة'),
        content: Text('هل تريد قبول «${task.title}»؟'),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx, false),
            child: const Text('إلغاء'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(ctx, true),
            child: const Text('قبول'),
          ),
        ],
      ),
    );
    if (ok != true) return;
    await _runAction(() => AssignedTasksRepository.accept(task.id));
  }

  Future<void> _reject(AssignedTask task) async {
    final note = await showDialog<String>(
      context: context,
      builder: (ctx) {
        final controller = TextEditingController();
        return AlertDialog(
          title: const Text('رفض المهمة'),
          content: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const Text('ملاحظة الرفض مطلوبة.'),
              const SizedBox(height: AppSpacing.sm),
              TextField(
                controller: controller,
                maxLines: 3,
                decoration: const InputDecoration(
                  labelText: 'الملاحظة',
                  border: OutlineInputBorder(),
                ),
                autofocus: true,
              ),
            ],
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(ctx),
              child: const Text('إلغاء'),
            ),
            FilledButton(
              style: FilledButton.styleFrom(backgroundColor: AppColors.rejected),
              onPressed: () {
                final text = controller.text.trim();
                if (text.isEmpty) return;
                Navigator.pop(ctx, text);
              },
              child: const Text('رفض'),
            ),
          ],
        );
      },
    );
    if (note == null || note.trim().isEmpty) return;
    await _runAction(
      () => AssignedTasksRepository.reject(task.id, note: note),
    );
  }

  Future<void> _transfer(AssignedTask task) async {
    List<BranchManagerPickOption> users;
    try {
      users = await AssignedTasksRepository.listBranchManagers();
    } on ApiException catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(e.message)),
      );
      return;
    }
    if (!mounted) return;
    if (users.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('لا يوجد مستخدمو إدارة فرع نشطون')),
      );
      return;
    }

    final picked = await showDialog<BranchManagerPickOption>(
      context: context,
      builder: (ctx) => SimpleDialog(
        title: const Text('نقل إلى إدارة الفرع'),
        children: [
          for (final user in users)
            SimpleDialogOption(
              onPressed: () => Navigator.pop(ctx, user),
              child: ListTile(
                contentPadding: EdgeInsets.zero,
                title: Text(user.displayName),
                subtitle: user.userName == null || user.userName!.isEmpty
                    ? null
                    : Text(user.userName!),
              ),
            ),
        ],
      ),
    );
    if (picked == null) return;

    await _runAction(
      () => AssignedTasksRepository.transfer(
        task.id,
        targetUserId: picked.id,
        targetKey: picked.id,
        targetDisplayName: picked.displayName,
      ),
    );
  }

  Future<void> _complete(AssignedTask task) async {
    final ok = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('تم الإنجاز'),
        content: Text('تأكيد إنجاز «${task.title}»؟'),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx, false),
            child: const Text('إلغاء'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(ctx, true),
            child: const Text('تم الإنجاز'),
          ),
        ],
      ),
    );
    if (ok != true) return;
    await _runAction(() => AssignedTasksRepository.complete(task.id));
  }

  Future<void> _confirm(AssignedTask task) async {
    final ok = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('تصديق المهمة'),
        content: Text(
          'تأكيد تصديق إنجاز «${task.title}» بعد إنجاز إدارة الفرع؟',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx, false),
            child: const Text('إلغاء'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(ctx, true),
            child: const Text('تصديق'),
          ),
        ],
      ),
    );
    if (ok != true) return;
    await _runAction(() => AssignedTasksRepository.confirm(task.id));
  }

  Future<void> _openAttachment(
    AssignedTask task,
    AssignedTaskAttachment attachment,
  ) async {
    try {
      await AssignedTasksRepository.openAttachment(
        taskId: task.id,
        attachment: attachment,
      );
    } on ApiException catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(e.message)),
      );
    }
  }

  Future<void> _openTaskDetail(AssignedTask task) async {
    final desc = task.description.trim();
    await showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      showDragHandle: true,
      builder: (ctx) {
        return SafeArea(
          child: Padding(
            padding: EdgeInsets.fromLTRB(
              AppSpacing.md,
              0,
              AppSpacing.md,
              MediaQuery.paddingOf(ctx).bottom + AppSpacing.md,
            ),
            child: SingleChildScrollView(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Text(
                    task.title,
                    style: const TextStyle(
                      fontSize: 18,
                      fontWeight: FontWeight.w800,
                      color: AppColors.text,
                    ),
                  ),
                  const SizedBox(height: AppSpacing.sm),
                  Wrap(
                    spacing: AppSpacing.sm,
                    runSpacing: AppSpacing.xs,
                    children: [
                      StatusChip(
                        label: task.priorityLabel,
                        color: _priorityColor(task.priority),
                      ),
                      StatusChip(
                        label: task.statusLabel,
                        color: _statusColor(task.status),
                      ),
                    ],
                  ),
                  const SizedBox(height: AppSpacing.sm),
                  InfoRow(
                    label: 'تاريخ الاستحقاق',
                    value: task.dueDisplay,
                  ),
                  if (task.transferredAtUtc != null)
                    InfoRow(
                      label: 'وقت النقل',
                      value: AppDate.iraqDateTime12h(task.transferredAtUtc),
                    ),
                  if (task.branchCompletedAtUtc != null)
                    InfoRow(
                      label: 'إنجاز الفرع',
                      value: AppDate.iraqDateTime12h(task.branchCompletedAtUtc),
                    ),
                  if (task.delegatedCompletedAtUtc != null)
                    InfoRow(
                      label: 'إنجاز المفوض',
                      value:
                          AppDate.iraqDateTime12h(task.delegatedCompletedAtUtc),
                    ),
                  if (task.delegatedConfirmedAtUtc != null)
                    InfoRow(
                      label: 'تصديق المفوض',
                      value:
                          AppDate.iraqDateTime12h(task.delegatedConfirmedAtUtc),
                    ),
                  const SizedBox(height: AppSpacing.md),
                  const Text(
                    'وصف المهمة',
                    style: TextStyle(
                      fontWeight: FontWeight.w700,
                      fontSize: 15,
                      color: AppColors.primary,
                    ),
                  ),
                  const SizedBox(height: AppSpacing.xs),
                  Text(
                    desc.isEmpty ? 'لا يوجد وصف لهذه المهمة.' : desc,
                    style: TextStyle(
                      fontSize: 15,
                      height: 1.55,
                      color: desc.isEmpty ? AppColors.muted : AppColors.text,
                    ),
                  ),
                  if (task.attachments.isNotEmpty) ...[
                    const SizedBox(height: AppSpacing.md),
                    const Text(
                      'المرفقات',
                      style: TextStyle(
                        fontWeight: FontWeight.w700,
                        color: AppColors.primary,
                      ),
                    ),
                    for (final file in task.attachments)
                      ListTile(
                        contentPadding: EdgeInsets.zero,
                        dense: true,
                        leading: const Icon(Icons.attach_file),
                        title: Text(file.fileName),
                        trailing: IconButton(
                          onPressed: () {
                            Navigator.pop(ctx);
                            _openAttachment(task, file);
                          },
                          icon: const Icon(Icons.download_outlined),
                        ),
                      ),
                  ],
                  const SizedBox(height: AppSpacing.md),
                  FilledButton(
                    onPressed: () => Navigator.pop(ctx),
                    child: const Text('إغلاق'),
                  ),
                ],
              ),
            ),
          ),
        );
      },
    );
  }

  Color _priorityColor(int priority) {
    switch (priority) {
      case AssignedTaskPriority.high:
        return AppColors.rejected;
      case AssignedTaskPriority.medium:
        return AppColors.warning;
      default:
        return AppColors.muted;
    }
  }

  Color _statusColor(String status) {
    switch (status) {
      case AssignedTaskStatus.sent:
        return AppColors.pending;
      case AssignedTaskStatus.transferred:
        return AppColors.muted;
      case AssignedTaskStatus.accepted:
        return AppColors.accent;
      case AssignedTaskStatus.awaitingDelegatedConfirm:
        return AppColors.warning;
      case AssignedTaskStatus.returned:
        return AppColors.rejected;
      case AssignedTaskStatus.completed:
        return AppColors.approved;
      default:
        return AppColors.muted;
    }
  }

  @override
  Widget build(BuildContext context) {
    if (_loading) return const LoadingView();
    if (_error != null) {
      return ErrorView(message: _error!, onRetry: reload);
    }
    if (_items.isEmpty) {
      return RefreshIndicator(
        onRefresh: reload,
        child: ListView(
          physics: const AlwaysScrollableScrollPhysics(),
          children: const [
            SizedBox(height: 120),
            EmptyView(message: 'لا توجد مهام معيّنة حالياً'),
          ],
        ),
      );
    }

    return RefreshIndicator(
      onRefresh: reload,
      child: ListView.separated(
        padding: const EdgeInsets.all(AppSpacing.md),
        itemCount: _items.length,
        separatorBuilder: (_, __) => const SizedBox(height: AppSpacing.sm),
        itemBuilder: (context, index) {
          final task = _items[index];
          final overdue = task.isOverdue();
          final desc = task.description.trim();
          return Card(
            clipBehavior: Clip.antiAlias,
            child: InkWell(
              onTap: () => _openTaskDetail(task),
              child: Padding(
                padding: const EdgeInsets.all(AppSpacing.md),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    Row(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Expanded(
                          child: Text(
                            task.title,
                            style: const TextStyle(
                              fontSize: 16,
                              fontWeight: FontWeight.w700,
                              color: AppColors.text,
                            ),
                          ),
                        ),
                        StatusChip(
                          label: task.priorityLabel,
                          color: _priorityColor(task.priority),
                        ),
                      ],
                    ),
                    const SizedBox(height: AppSpacing.sm),
                    Wrap(
                      spacing: AppSpacing.sm,
                      runSpacing: AppSpacing.xs,
                      children: [
                        StatusChip(
                          label: task.statusLabel,
                          color: _statusColor(task.status),
                        ),
                        if (overdue)
                          const StatusChip(
                            label: 'تجاوزت المدة',
                            color: AppColors.rejected,
                          ),
                      ],
                    ),
                    const SizedBox(height: AppSpacing.sm),
                    InfoRow(
                      label: 'تاريخ الاستحقاق',
                      value: task.dueDisplay,
                    ),
                    if (task.transferredAtUtc != null)
                      InfoRow(
                        label: 'وقت النقل',
                        value: AppDate.iraqDateTime12h(task.transferredAtUtc),
                      ),
                    if (task.branchCompletedAtUtc != null)
                      InfoRow(
                        label: 'إنجاز الفرع',
                        value:
                            AppDate.iraqDateTime12h(task.branchCompletedAtUtc),
                      ),
                    if (task.delegatedConfirmedAtUtc != null)
                      InfoRow(
                        label: 'تصديق',
                        value: AppDate.iraqDateTime12h(
                            task.delegatedConfirmedAtUtc),
                      ),
                    if (desc.isNotEmpty) ...[
                      const SizedBox(height: AppSpacing.sm),
                      Text(
                        desc.length > 90 ? '${desc.substring(0, 90)}…' : desc,
                        maxLines: 2,
                        overflow: TextOverflow.ellipsis,
                        style: const TextStyle(
                          fontSize: 13,
                          height: 1.4,
                          color: AppColors.muted,
                        ),
                      ),
                      const SizedBox(height: 4),
                      const Text(
                        'اضغط لعرض وصف المهمة بالكامل',
                        style: TextStyle(
                          fontSize: 12,
                          fontWeight: FontWeight.w600,
                          color: AppColors.primary,
                        ),
                      ),
                    ] else ...[
                      const SizedBox(height: AppSpacing.xs),
                      const Text(
                        'اضغط لعرض تفاصيل المهمة',
                        style: TextStyle(
                          fontSize: 12,
                          color: AppColors.muted,
                        ),
                      ),
                    ],
                    if ((task.rejectNote ?? '').isNotEmpty)
                      InfoRow(label: 'ملاحظة الرفض', value: task.rejectNote!),
                    if (task.attachments.isNotEmpty) ...[
                      const SizedBox(height: AppSpacing.sm),
                      const Text(
                        'المرفقات',
                        style: TextStyle(
                          fontWeight: FontWeight.w700,
                          color: AppColors.primary,
                        ),
                      ),
                      const SizedBox(height: AppSpacing.xs),
                      for (final file in task.attachments)
                        ListTile(
                          contentPadding: EdgeInsets.zero,
                          dense: true,
                          leading: const Icon(Icons.attach_file),
                          title: Text(file.fileName),
                          trailing: IconButton(
                            tooltip: 'فتح المرفق',
                            onPressed: _acting
                                ? null
                                : () => _openAttachment(task, file),
                            icon: const Icon(Icons.download_outlined),
                          ),
                        ),
                    ],
                    if (task.actions.hasAny) ...[
                      const SizedBox(height: AppSpacing.md),
                      AssignedTaskActionsBar(
                        actions: task.actions,
                        busy: _acting,
                        onAccept: () => _accept(task),
                        onReject: () => _reject(task),
                        onTransfer: () => _transfer(task),
                        onComplete: () => _complete(task),
                        onConfirm: () => _confirm(task),
                      ),
                    ],
                  ],
                ),
              ),
            ),
          );
        },
      ),
    );
  }
}
