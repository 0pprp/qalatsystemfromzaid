import 'package:branch_manager_application/core/api.dart';
import 'package:branch_manager_application/core/session.dart';
import 'package:branch_manager_application/theme/bm_theme.dart';
import 'package:branch_manager_application/ui/assigned_task_attachment_ops.dart';
import 'package:branch_manager_application/ui/assigned_tasks_actions.dart';
import 'package:flutter/material.dart';
import 'package:open_filex/open_filex.dart';

class AssignedTasksPage extends StatefulWidget {
  const AssignedTasksPage({super.key});

  @override
  State<AssignedTasksPage> createState() => _AssignedTasksPageState();
}

class _AssignedTasksPageState extends State<AssignedTasksPage> {
  bool _loading = true;
  bool _acting = false;
  String? _error;
  List<BmAssignedTask> _items = [];

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
      final rows = await BmApi.instance.assignedTasksInbox();
      final parsed = rows.map(BmAssignedTask.fromJson).toList();
      if (!mounted) return;
      setState(() {
        _items = BmAssignedTaskActions.sortInbox(parsed);
        _loading = false;
      });
    } catch (e) {
      if (!mounted) return;
      if (e is BmApiException && e.statusCode == 401) {
        await BmSession.instance.clear();
      }
      setState(() {
        _loading = false;
        _error = '$e';
      });
    }
  }

  Future<void> _openTaskDetail(BmAssignedTask task) async {
    final desc = task.description.trim();
    await showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      showDragHandle: true,
      builder: (ctx) => Directionality(
        textDirection: TextDirection.rtl,
        child: SafeArea(
          child: Padding(
            padding: EdgeInsets.fromLTRB(
              16,
              0,
              16,
              MediaQuery.paddingOf(ctx).bottom + 16,
            ),
            child: SingleChildScrollView(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Text(
                    task.title.isEmpty ? 'مهمة' : task.title,
                    style: const TextStyle(
                      fontFamily: 'Cairo',
                      fontWeight: FontWeight.w800,
                      fontSize: 17,
                      color: BmTheme.ink,
                    ),
                  ),
                  const SizedBox(height: 10),
                  Wrap(
                    spacing: 8,
                    runSpacing: 6,
                    children: [
                      _Chip(
                        label: task.priorityLabel,
                        color: _priorityColorStatic(task.priority),
                      ),
                      _Chip(label: task.statusLabel, color: BmTheme.primary),
                      _Chip(
                        label: 'الاستحقاق ${task.dueDisplay}',
                        color: BmTheme.muted,
                      ),
                    ],
                  ),
                  if (task.transferredAtUtc != null) ...[
                    const SizedBox(height: 8),
                    Text(
                      'وقت النقل: ${BmAssignedTask.stampDisplay(task.transferredAtUtc)}',
                      style: const TextStyle(
                        fontFamily: 'Cairo',
                        fontSize: 13,
                        color: BmTheme.muted,
                      ),
                    ),
                  ],
                  if (task.branchCompletedAtUtc != null) ...[
                    const SizedBox(height: 4),
                    Text(
                      'إنجاز الفرع: ${BmAssignedTask.stampDisplay(task.branchCompletedAtUtc)}',
                      style: const TextStyle(
                        fontFamily: 'Cairo',
                        fontSize: 13,
                        color: BmTheme.muted,
                      ),
                    ),
                  ],
                  const SizedBox(height: 14),
                  const Text(
                    'وصف المهمة',
                    style: TextStyle(
                      fontFamily: 'Cairo',
                      fontWeight: FontWeight.w700,
                      fontSize: 14,
                      color: BmTheme.primary,
                    ),
                  ),
                  const SizedBox(height: 6),
                  Text(
                    desc.isEmpty ? 'لا يوجد وصف لهذه المهمة.' : desc,
                    style: TextStyle(
                      fontFamily: 'Cairo',
                      fontSize: 14,
                      height: 1.55,
                      color: desc.isEmpty ? BmTheme.muted : BmTheme.ink,
                    ),
                  ),
                  if (task.attachments.isNotEmpty) ...[
                    const SizedBox(height: 14),
                    const Text(
                      'المرفقات',
                      style: TextStyle(
                        fontFamily: 'Cairo',
                        fontWeight: FontWeight.w700,
                        fontSize: 14,
                        color: BmTheme.primary,
                      ),
                    ),
                    for (final file in task.attachments)
                      ListTile(
                        contentPadding: EdgeInsets.zero,
                        dense: true,
                        leading: const Icon(Icons.attach_file),
                        title: Text(
                          file.fileName,
                          style: const TextStyle(fontFamily: 'Cairo'),
                        ),
                        trailing: Row(
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            IconButton(
                              tooltip: 'تنزيل وفتح',
                              icon: const Icon(Icons.file_open_outlined),
                              onPressed: () =>
                                  _downloadAndOpenAttachment(task, file),
                            ),
                            IconButton(
                              tooltip: 'مشاركة',
                              icon: const Icon(Icons.share_outlined),
                              onPressed: () =>
                                  _shareAttachment(task, file),
                            ),
                          ],
                        ),
                      ),
                  ],
                  const SizedBox(height: 16),
                  FilledButton(
                    onPressed: () => Navigator.pop(ctx),
                    child: const Text('إغلاق',
                        style: TextStyle(fontFamily: 'Cairo')),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }

  Future<void> _downloadAndOpenAttachment(
    BmAssignedTask task,
    BmAssignedTaskAttachment file,
  ) async {
    final messenger = ScaffoldMessenger.of(context);
    messenger.showSnackBar(
      const SnackBar(
        content: Text('جارٍ تنزيل المرفق...',
            style: TextStyle(fontFamily: 'Cairo')),
        duration: Duration(seconds: 20),
      ),
    );
    try {
      final result = await BmAssignedTaskAttachmentOps.downloadAndOpen(
        taskId: task.id,
        file: file,
      );
      if (!mounted) return;
      messenger.hideCurrentSnackBar();
      if (result.type != ResultType.done) {
        messenger.showSnackBar(
          SnackBar(
            content: Text(
              result.message.isNotEmpty
                  ? result.message
                  : 'تم التنزيل لكن تعذر فتح الملف',
              style: const TextStyle(fontFamily: 'Cairo'),
            ),
          ),
        );
      }
    } catch (e) {
      if (!mounted) return;
      messenger.hideCurrentSnackBar();
      messenger.showSnackBar(
        SnackBar(
          content: Text(
            e is BmApiException ? e.message : 'تعذر تنزيل المرفق',
            style: const TextStyle(fontFamily: 'Cairo'),
          ),
        ),
      );
    }
  }

  Future<void> _shareAttachment(
    BmAssignedTask task,
    BmAssignedTaskAttachment file,
  ) async {
    final messenger = ScaffoldMessenger.of(context);
    messenger.showSnackBar(
      const SnackBar(
        content: Text('جارٍ تجهيز المرفق للمشاركة...',
            style: TextStyle(fontFamily: 'Cairo')),
        duration: Duration(seconds: 20),
      ),
    );
    try {
      await BmAssignedTaskAttachmentOps.downloadAndShare(
        taskId: task.id,
        file: file,
      );
      if (!mounted) return;
      messenger.hideCurrentSnackBar();
    } catch (e) {
      if (!mounted) return;
      messenger.hideCurrentSnackBar();
      messenger.showSnackBar(
        SnackBar(
          content: Text(
            e is BmApiException ? e.message : 'تعذر مشاركة المرفق',
            style: const TextStyle(fontFamily: 'Cairo'),
          ),
        ),
      );
    }
  }

  static Color _priorityColorStatic(int priority) {
    switch (priority) {
      case 1:
        return BmTheme.danger;
      case 2:
        return BmTheme.warn;
      default:
        return BmTheme.muted;
    }
  }

  Future<void> _accept(BmAssignedTask task) async {
    await _runAction(() => BmApi.instance.acceptAssignedTask(task.id));
  }

  Future<void> _complete(BmAssignedTask task) async {
    final ok = await showDialog<bool>(
      context: context,
      builder: (ctx) => Directionality(
        textDirection: TextDirection.rtl,
        child: AlertDialog(
          title: const Text('تأكيد الإنجاز',
              style: TextStyle(fontFamily: 'Cairo')),
          content: Text(
            'هل أنجزت المهمة «${task.title}»؟',
            style: const TextStyle(fontFamily: 'Cairo'),
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(ctx, false),
              child:
                  const Text('إلغاء', style: TextStyle(fontFamily: 'Cairo')),
            ),
            FilledButton(
              onPressed: () => Navigator.pop(ctx, true),
              child: const Text('تم الإنجاز',
                  style: TextStyle(fontFamily: 'Cairo')),
            ),
          ],
        ),
      ),
    );
    if (ok != true) return;
    await _runAction(() => BmApi.instance.completeAssignedTask(task.id));
  }

  Future<void> _reject(BmAssignedTask task) async {
    final noteCtrl = TextEditingController();
    final ok = await showDialog<bool>(
      context: context,
      builder: (ctx) => Directionality(
        textDirection: TextDirection.rtl,
        child: AlertDialog(
          title: const Text('رفض المهمة',
              style: TextStyle(fontFamily: 'Cairo')),
          content: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const Text(
                'ملاحظة الرفض مطلوبة وستُعاد المهمة للمدير.',
                style: TextStyle(fontFamily: 'Cairo', fontSize: 13),
              ),
              const SizedBox(height: 12),
              TextField(
                controller: noteCtrl,
                maxLines: 3,
                decoration: const InputDecoration(
                  labelText: 'ملاحظة الرفض',
                  alignLabelWithHint: true,
                ),
              ),
            ],
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(ctx, false),
              child:
                  const Text('إلغاء', style: TextStyle(fontFamily: 'Cairo')),
            ),
            FilledButton(
              style: FilledButton.styleFrom(backgroundColor: BmTheme.danger),
              onPressed: () => Navigator.pop(ctx, true),
              child:
                  const Text('رفض', style: TextStyle(fontFamily: 'Cairo')),
            ),
          ],
        ),
      ),
    );
    final note = noteCtrl.text.trim();
    noteCtrl.dispose();
    if (ok != true) return;
    if (note.isEmpty) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('أدخل ملاحظة الرفض')),
      );
      return;
    }
    await _runAction(
      () => BmApi.instance.rejectAssignedTask(task.id, note: note),
    );
  }

  Future<void> _runAction(Future<void> Function() action) async {
    if (_acting) return;
    setState(() => _acting = true);
    try {
      await action();
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('تم بنجاح')),
      );
      await _load();
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('$e')),
      );
    } finally {
      if (mounted) setState(() => _acting = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    if (_loading) {
      return const Center(child: CircularProgressIndicator());
    }
    if (_error != null) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(
                _error!,
                textAlign: TextAlign.center,
                style: const TextStyle(fontFamily: 'Cairo', color: BmTheme.danger),
              ),
              const SizedBox(height: 12),
              FilledButton(
                onPressed: _load,
                child: const Text('إعادة المحاولة',
                    style: TextStyle(fontFamily: 'Cairo')),
              ),
            ],
          ),
        ),
      );
    }
    if (_items.isEmpty) {
      return RefreshIndicator(
        onRefresh: _load,
        child: ListView(
          physics: const AlwaysScrollableScrollPhysics(),
          children: const [
            SizedBox(height: 120),
            Center(
              child: Text(
                'لا توجد مهام معيّنة حالياً',
                style: TextStyle(fontFamily: 'Cairo', color: BmTheme.muted),
              ),
            ),
          ],
        ),
      );
    }

    return RefreshIndicator(
      onRefresh: _load,
      child: ListView.separated(
        padding: const EdgeInsets.fromLTRB(12, 12, 12, 24),
        itemCount: _items.length,
        separatorBuilder: (_, _) => const SizedBox(height: 10),
        itemBuilder: (context, i) => _TaskCard(
          task: _items[i],
          busy: _acting,
          onOpenDetail: () => _openTaskDetail(_items[i]),
          onAccept: () => _accept(_items[i]),
          onReject: () => _reject(_items[i]),
          onComplete: () => _complete(_items[i]),
        ),
      ),
    );
  }
}

class _TaskCard extends StatelessWidget {
  const _TaskCard({
    required this.task,
    required this.busy,
    required this.onOpenDetail,
    required this.onAccept,
    required this.onReject,
    required this.onComplete,
  });

  final BmAssignedTask task;
  final bool busy;
  final VoidCallback onOpenDetail;
  final VoidCallback onAccept;
  final VoidCallback onReject;
  final VoidCallback onComplete;

  @override
  Widget build(BuildContext context) {
    final actions = task.actions;
    final desc = task.description.trim();
    return Card(
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: onOpenDetail,
        child: Padding(
          padding: const EdgeInsets.all(14),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Row(
                children: [
                  Expanded(
                    child: Text(
                      task.title.isEmpty ? 'مهمة' : task.title,
                      style: const TextStyle(
                        fontFamily: 'Cairo',
                        fontWeight: FontWeight.w700,
                        fontSize: 15,
                        color: BmTheme.ink,
                      ),
                    ),
                  ),
                  _Chip(
                    label: task.priorityLabel,
                    color: _priorityColor(task.priority),
                  ),
                ],
              ),
              const SizedBox(height: 8),
              Wrap(
                spacing: 8,
                runSpacing: 6,
                children: [
                  _Chip(label: task.statusLabel, color: BmTheme.primary),
                  _Chip(
                    label: 'الاستحقاق ${task.dueDisplay}',
                    color: BmTheme.muted,
                  ),
                  if (task.isOverdue)
                    const _Chip(label: 'تجاوزت المدة', color: BmTheme.danger),
                ],
              ),
              if (task.branchCompletedAtUtc != null) ...[
                const SizedBox(height: 6),
                Text(
                  'إنجاز الفرع: ${BmAssignedTask.stampDisplay(task.branchCompletedAtUtc)}',
                  style: const TextStyle(
                    fontFamily: 'Cairo',
                    fontSize: 12,
                    color: BmTheme.muted,
                  ),
                ),
              ],
              if (task.attachments.isNotEmpty) ...[
                const SizedBox(height: 6),
                Text(
                  'مرفقات: ${task.attachments.length}',
                  style: const TextStyle(
                    fontFamily: 'Cairo',
                    fontSize: 12,
                    color: BmTheme.muted,
                  ),
                ),
              ],
              if (desc.isNotEmpty) ...[
                const SizedBox(height: 10),
                Text(
                  desc.length > 90 ? '${desc.substring(0, 90)}…' : desc,
                  maxLines: 2,
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(
                    fontFamily: 'Cairo',
                    fontSize: 13,
                    height: 1.4,
                    color: BmTheme.muted,
                  ),
                ),
                const SizedBox(height: 4),
                const Text(
                  'اضغط لعرض وصف المهمة بالكامل',
                  style: TextStyle(
                    fontFamily: 'Cairo',
                    fontSize: 12,
                    fontWeight: FontWeight.w700,
                    color: BmTheme.primary,
                  ),
                ),
              ] else ...[
                const SizedBox(height: 8),
                const Text(
                  'اضغط لعرض تفاصيل المهمة',
                  style: TextStyle(
                    fontFamily: 'Cairo',
                    fontSize: 12,
                    color: BmTheme.muted,
                  ),
                ),
              ],
              if ((task.rejectNote ?? '').trim().isNotEmpty) ...[
                const SizedBox(height: 8),
                Text(
                  'ملاحظة الرفض: ${task.rejectNote}',
                  style: const TextStyle(
                    fontFamily: 'Cairo',
                    fontSize: 12,
                    color: BmTheme.warn,
                  ),
                ),
              ],
              if (actions.canAccept ||
                  actions.canReject ||
                  actions.canComplete) ...[
                const SizedBox(height: 12),
                Wrap(
                  spacing: 8,
                  runSpacing: 8,
                  children: [
                    if (actions.canAccept)
                      FilledButton(
                        onPressed: busy ? null : onAccept,
                        child: const Text('قبول',
                            style: TextStyle(fontFamily: 'Cairo')),
                      ),
                    if (actions.canReject)
                      OutlinedButton(
                        onPressed: busy ? null : onReject,
                        style: OutlinedButton.styleFrom(
                          foregroundColor: BmTheme.danger,
                        ),
                        child: const Text('رفض',
                            style: TextStyle(fontFamily: 'Cairo')),
                      ),
                    if (actions.canComplete)
                      FilledButton(
                        onPressed: busy ? null : onComplete,
                        style: FilledButton.styleFrom(
                          backgroundColor: BmTheme.ok,
                        ),
                        child: const Text('تم الإنجاز',
                            style: TextStyle(fontFamily: 'Cairo')),
                      ),
                  ],
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }

  Color _priorityColor(int p) {
    switch (p) {
      case 1:
        return BmTheme.danger;
      case 2:
        return BmTheme.warn;
      default:
        return BmTheme.muted;
    }
  }
}

class _Chip extends StatelessWidget {
  const _Chip({required this.label, required this.color});

  final String label;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(20),
        border: Border.all(color: color.withValues(alpha: 0.35)),
      ),
      child: Text(
        label,
        style: TextStyle(
          fontFamily: 'Cairo',
          fontSize: 11,
          fontWeight: FontWeight.w600,
          color: color,
        ),
      ),
    );
  }
}
