import 'package:follower_application/services/committee_task_service.dart';
import 'package:follower_application/services/follower_assigned_tasks_service.dart';
import 'package:follower_application/ui/app_safe_scaffold.dart';
import 'package:follower_application/utils/AppTheme.dart';
import 'package:flutter/material.dart';

class CommitteeTasksPage extends StatefulWidget {
  const CommitteeTasksPage({super.key});

  @override
  State<CommitteeTasksPage> createState() => _CommitteeTasksPageState();
}

class _CommitteeTasksPageState extends State<CommitteeTasksPage> {
  bool _loading = true;
  List<CommitteeTask> _bulletin = const [];
  List<FollowerAssignedTask> _assigned = const [];
  String? _actionError;

  @override
  void initState() {
    super.initState();
    _load(markSeen: true);
  }

  Future<void> _load({bool markSeen = false}) async {
    setState(() {
      _loading = true;
      _actionError = null;
    });
    final bulletin = await CommitteeTaskService.fetch();
    final inbox = await FollowerAssignedTasksService.fetchInbox();
    if (markSeen) {
      await FollowerAssignedTasksService.markSeen();
    }
    if (!mounted) return;
    setState(() {
      _bulletin = bulletin;
      _assigned = inbox.tasks;
      _loading = false;
    });
  }

  Future<void> _accept(FollowerAssignedTask task) async {
    final err = await FollowerAssignedTasksService.accept(task);
    if (!mounted) return;
    if (err != null) {
      setState(() => _actionError = err);
      return;
    }
    await _load();
  }

  Future<void> _complete(FollowerAssignedTask task) async {
    final err = await FollowerAssignedTasksService.complete(task);
    if (!mounted) return;
    if (err != null) {
      setState(() => _actionError = err);
      return;
    }
    await _load();
  }

  Future<void> _reject(FollowerAssignedTask task) async {
    final noteCtrl = TextEditingController();
    final ok = await showDialog<bool>(
      context: context,
      builder: (ctx) => Directionality(
        textDirection: TextDirection.rtl,
        child: AlertDialog(
          title: const Text('رفض المهمة',
              style: TextStyle(fontFamily: 'Cairo', fontWeight: FontWeight.bold)),
          content: TextField(
            controller: noteCtrl,
            maxLines: 4,
            decoration: const InputDecoration(
              hintText: 'سبب الرفض (مطلوب)',
              hintStyle: TextStyle(fontFamily: 'Cairo'),
            ),
            style: const TextStyle(fontFamily: 'Cairo'),
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(ctx, false),
              child: const Text('إلغاء', style: TextStyle(fontFamily: 'Cairo')),
            ),
            FilledButton(
              onPressed: () => Navigator.pop(ctx, true),
              child: const Text('رفض', style: TextStyle(fontFamily: 'Cairo')),
            ),
          ],
        ),
      ),
    );
    if (ok != true) return;
    final note = noteCtrl.text.trim();
    if (note.isEmpty) {
      setState(() => _actionError = 'ملاحظة الرفض مطلوبة');
      return;
    }
    final err = await FollowerAssignedTasksService.reject(task, note);
    if (!mounted) return;
    if (err != null) {
      setState(() => _actionError = err);
      return;
    }
    await _load();
  }

  void _openBulletin(CommitteeTask task) {
    showDialog<void>(
      context: context,
      builder: (ctx) => Directionality(
        textDirection: TextDirection.rtl,
        child: AlertDialog(
          title: const Text(
            'نشرة مسؤول اللجنة',
            style: TextStyle(fontFamily: 'Cairo', fontWeight: FontWeight.bold),
          ),
          content: SizedBox(
            width: double.maxFinite,
            child: SingleChildScrollView(
              child: Text(
                task.body,
                style: const TextStyle(
                  fontFamily: 'Cairo',
                  fontSize: 15,
                  height: 1.55,
                ),
              ),
            ),
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(ctx),
              child: const Text('إغلاق', style: TextStyle(fontFamily: 'Cairo')),
            ),
          ],
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Directionality(
      textDirection: TextDirection.rtl,
      child: AppSafeScaffold(
        backgroundColor: AppTheme.backgroundColor,
        appBar: AppBar(
          backgroundColor: AppTheme.primaryColor,
          foregroundColor: Colors.white,
          title: const Text(
            'مهام مسؤول اللجنة',
            style: TextStyle(fontFamily: 'Cairo', fontWeight: FontWeight.bold),
          ),
        ),
        body: RefreshIndicator(
          onRefresh: () => _load(markSeen: true),
          color: AppTheme.primaryColor,
          child: _loading
              ? ListView(
                  children: const [
                    SizedBox(height: 120),
                    Center(child: CircularProgressIndicator()),
                  ],
                )
              : ListView(
                  padding: const EdgeInsets.fromLTRB(16, 16, 16, 24),
                  children: [
                    if (_actionError != null) ...[
                      Text(
                        _actionError!,
                        style: const TextStyle(
                          fontFamily: 'Cairo',
                          color: Colors.red,
                          fontSize: 13,
                        ),
                      ),
                      const SizedBox(height: 10),
                    ],
                    const Text(
                      'مهام شخصية من المدير',
                      style: TextStyle(
                        fontFamily: 'Cairo',
                        fontWeight: FontWeight.bold,
                        fontSize: 16,
                      ),
                    ),
                    const SizedBox(height: 10),
                    if (_assigned.isEmpty)
                      const Padding(
                        padding: EdgeInsets.only(bottom: 8),
                        child: Text(
                          'لا توجد مهام شخصية حالياً',
                          style: TextStyle(
                            fontFamily: 'Cairo',
                            color: Colors.grey,
                            fontSize: 14,
                          ),
                        ),
                      )
                    else
                      ..._assigned.map(_assignedCard),
                    const SizedBox(height: 20),
                    const Text(
                      'نشرة عامة',
                      style: TextStyle(
                        fontFamily: 'Cairo',
                        fontWeight: FontWeight.bold,
                        fontSize: 16,
                      ),
                    ),
                    const SizedBox(height: 10),
                    if (_bulletin.isEmpty)
                      const Text(
                        'لا توجد نشرات حالياً',
                        style: TextStyle(
                          fontFamily: 'Cairo',
                          color: Colors.grey,
                          fontSize: 14,
                        ),
                      )
                    else
                      ..._bulletin.map(_bulletinCard),
                  ],
                ),
        ),
      ),
    );
  }

  Widget _assignedCard(FollowerAssignedTask t) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 10),
      child: Material(
        color: Theme.of(context).cardColor,
        borderRadius: BorderRadius.circular(14),
        elevation: 0.5,
        child: Padding(
          padding: const EdgeInsets.fromLTRB(14, 12, 14, 12),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Row(
                children: [
                  Expanded(
                    child: Text(
                      t.title.isEmpty ? 'مهمة' : t.title,
                      style: const TextStyle(
                        fontFamily: 'Cairo',
                        fontWeight: FontWeight.bold,
                        fontSize: 15,
                      ),
                    ),
                  ),
                  Container(
                    padding:
                        const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
                    decoration: BoxDecoration(
                      color: t.isSent
                          ? const Color(0xFFFFF3E0)
                          : AppTheme.primaryColor.withValues(alpha: 0.1),
                      borderRadius: BorderRadius.circular(8),
                    ),
                    child: Text(
                      t.status.isEmpty ? '—' : t.status,
                      style: TextStyle(
                        fontFamily: 'Cairo',
                        fontSize: 12,
                        fontWeight: FontWeight.w600,
                        color: t.isSent
                            ? const Color(0xFFE65100)
                            : AppTheme.primaryColor,
                      ),
                    ),
                  ),
                ],
              ),
              if (t.description.isNotEmpty) ...[
                const SizedBox(height: 8),
                Text(
                  t.description,
                  style: const TextStyle(
                    fontFamily: 'Cairo',
                    fontSize: 13,
                    height: 1.45,
                    color: Colors.black87,
                  ),
                ),
              ],
              if (t.dueDate != null && t.dueDate!.isNotEmpty) ...[
                const SizedBox(height: 6),
                Text(
                  'الاستحقاق: ${t.dueDate}',
                  style: const TextStyle(
                    fontFamily: 'Cairo',
                    fontSize: 12,
                    color: Colors.grey,
                  ),
                ),
              ],
              if (t.canAccept || t.canComplete) ...[
                const SizedBox(height: 10),
                Wrap(
                  spacing: 8,
                  runSpacing: 8,
                  children: [
                    if (t.canAccept)
                      FilledButton(
                        onPressed: () => _accept(t),
                        child: const Text('قبول',
                            style: TextStyle(fontFamily: 'Cairo')),
                      ),
                    if (t.canReject)
                      OutlinedButton(
                        onPressed: () => _reject(t),
                        child: const Text('رفض',
                            style: TextStyle(fontFamily: 'Cairo')),
                      ),
                    if (t.canComplete)
                      FilledButton(
                        onPressed: () => _complete(t),
                        child: const Text('إنجاز',
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

  Widget _bulletinCard(CommitteeTask t) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 10),
      child: Material(
        color: Theme.of(context).cardColor,
        borderRadius: BorderRadius.circular(14),
        elevation: 0.5,
        child: InkWell(
          borderRadius: BorderRadius.circular(14),
          onTap: () => _openBulletin(t),
          child: Padding(
            padding: const EdgeInsets.fromLTRB(14, 12, 14, 12),
            child: Row(
              children: [
                Container(
                  width: 40,
                  height: 40,
                  decoration: BoxDecoration(
                    color: AppTheme.primaryColor.withValues(alpha: 0.12),
                    borderRadius: BorderRadius.circular(12),
                  ),
                  child: const Icon(
                    Icons.campaign_outlined,
                    color: AppTheme.primaryColor,
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: Text(
                    t.preview,
                    maxLines: 3,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(
                      fontFamily: 'Cairo',
                      fontSize: 14,
                      height: 1.4,
                      fontWeight: FontWeight.w600,
                    ),
                  ),
                ),
                const Icon(Icons.chevron_left, color: Colors.grey),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
