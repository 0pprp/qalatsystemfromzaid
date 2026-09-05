import 'package:flutter/material.dart';
import 'package:sales_employee_application/data/sales_models.dart';
import 'package:sales_employee_application/data/sales_repository_factory.dart';
import 'package:sales_employee_application/services/api_client.dart';
import 'package:sales_employee_application/utils/app_theme.dart';
import 'package:sales_employee_application/utils/sales_format.dart';

class PendingSalesScreen extends StatefulWidget {
  const PendingSalesScreen({super.key});

  @override
  State<PendingSalesScreen> createState() => _PendingSalesScreenState();
}

class _PendingSalesScreenState extends State<PendingSalesScreen>
    with SingleTickerProviderStateMixin {
  late final TabController _tabs;
  List<SalesDraft> _rows = [];
  List<SalesDraft> _today = [];
  List<SalesWorkRequest> _requests = [];
  bool _loading = true;
  String? _error;
  bool _tabsReady = false;

  @override
  void initState() {
    super.initState();
    _tabs = TabController(length: 3, vsync: this);
    _load();
  }

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    if (_tabsReady) return;
    _tabsReady = true;
    final arg = ModalRoute.of(context)?.settings.arguments;
    if (arg == 'today' || arg == 1) {
      _tabs.index = 1;
    }
  }

  @override
  void dispose() {
    _tabs.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    if (!mounted) return;
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final rows = await SalesRepositoryFactory.instance.pending();
      final today = await SalesRepositoryFactory.instance.todayCompleted();
      final requests = await SalesRepositoryFactory.instance.salesRequests();
      if (!mounted) return;
      setState(() {
        _rows = rows;
        _today = today;
        _requests = requests;
        _loading = false;
      });
    } on ApiException catch (e) {
      if (!mounted) return;
      setState(() {
        _loading = false;
        _error = e.message;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _loading = false;
        _error = 'تعذر تحميل المبيعات';
      });
    }
  }

  int get _newCount => _requests.where((r) => r.isNew).length;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('المبيعات'),
        bottom: TabBar(
          controller: _tabs,
          isScrollable: true,
          labelColor: AppColors.darkGreen,
          tabs: [
            const Tab(text: 'المبيعات المعلقة'),
            const Tab(text: 'مبيعات اليوم'),
            Tab(
              child: Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  const Text('طلبات المبيعات'),
                  if (_newCount > 0) ...[
                    const SizedBox(width: 6),
                    CircleAvatar(
                      radius: 10,
                      backgroundColor: AppColors.danger,
                      child: Text('$_newCount',
                          style: const TextStyle(color: Colors.white, fontSize: 11)),
                    ),
                  ],
                ],
              ),
            ),
          ],
        ),
      ),
      body: TabBarView(
        controller: _tabs,
        children: [
          _listTab(
            rows: _rows,
            empty: 'لا توجد عمليات معلقة',
          ),
          _listTab(
            rows: _today,
            empty: 'لا توجد مبيعات مكتملة اليوم',
          ),
          _requestsTab(),
        ],
      ),
    );
  }

  Widget _listTab({required List<SalesDraft> rows, required String empty}) {
    if (_loading) return const Center(child: CircularProgressIndicator());
    if (_error != null) {
      return Center(child: Text(_error!, style: const TextStyle(color: AppColors.danger)));
    }
    if (rows.isEmpty) {
      return Center(child: Text(empty, style: const TextStyle(color: AppColors.muted)));
    }
    return RefreshIndicator(
      onRefresh: _load,
      child: ListView.builder(
        padding: const EdgeInsets.all(AppSpacing.md),
        itemCount: rows.length,
        itemBuilder: (context, i) {
          final d = rows[i];
          return Card(
            child: ListTile(
              title: Text(d.fullName, style: const TextStyle(fontWeight: FontWeight.w700)),
              subtitle: Text(
                'رقم ${d.saleId} · ${EvaluationLabels.of(d.evaluationLevel)}\n${MoneyFormat.iqd(d.finalSalePrice)}',
              ),
              trailing: _StatusBadge(status: d.status, rejected: d.isRejected),
              onTap: () async {
                await Navigator.pushNamed(context, '/sale-details', arguments: d.saleId);
                if (mounted) await _load();
              },
            ),
          );
        },
      ),
    );
  }

  Widget _requestsTab() {
    if (_loading) return const Center(child: CircularProgressIndicator());
    if (_error != null) {
      return Center(child: Text(_error!, style: const TextStyle(color: AppColors.danger)));
    }
    if (_requests.isEmpty) {
      return const Center(child: Text('لا توجد طلبات مبيعات', style: TextStyle(color: AppColors.muted)));
    }
    return RefreshIndicator(
      onRefresh: _load,
      child: ListView.builder(
        padding: const EdgeInsets.all(AppSpacing.md),
        itemCount: _requests.length,
        itemBuilder: (context, i) => _requestCard(_requests[i]),
      ),
    );
  }

  Widget _requestCard(SalesWorkRequest r) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.md),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            InkWell(
              onTap: () async {
                await Navigator.push(
                  context,
                  MaterialPageRoute(builder: (_) => SalesRequestDetailsScreen(requestId: r.id)),
                );
                if (mounted) await _load();
              },
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Text(r.customerName, style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 16)),
                  Text(
                    '${r.customerPhone ?? ''} · ${r.customerProvince ?? ''}',
                    style: const TextStyle(color: AppColors.muted),
                  ),
                  if ((r.notes ?? '').trim().isNotEmpty)
                    Text(r.notes!, style: const TextStyle(color: AppColors.muted)),
                  const SizedBox(height: 6),
                  Align(
                    alignment: Alignment.centerRight,
                    child: Text(
                      SalesRequestStatusLabels.of(r.status),
                      style: const TextStyle(fontSize: 12, color: AppColors.darkGreen, fontWeight: FontWeight.w700),
                    ),
                  ),
                ],
              ),
            ),
            if (r.isReturned && (r.returnNote ?? '').trim().isNotEmpty) ...[
              const SizedBox(height: AppSpacing.sm),
              Container(
                padding: const EdgeInsets.all(AppSpacing.sm),
                decoration: BoxDecoration(
                  color: AppColors.warningSoft,
                  borderRadius: BorderRadius.circular(AppRadius.sm),
                ),
                child: Text('ملاحظة الإعادة: ${r.returnNote}',
                    style: const TextStyle(color: AppColors.danger, fontWeight: FontWeight.w700)),
              ),
            ],
            if (r.isPendingHold && (r.pendingNote ?? '').trim().isNotEmpty)
              Padding(
                padding: const EdgeInsets.only(top: AppSpacing.sm),
                child: Text('ملاحظة التعليق: ${r.pendingNote}'),
              ),
            if (r.canAct) ...[
              const SizedBox(height: AppSpacing.md),
              ElevatedButton(
                onPressed: () => _prepareAndOpenSale(r),
                child: const Text('تجهيز المبيع'),
              ),
              const SizedBox(height: AppSpacing.sm),
              OutlinedButton(
                onPressed: () => _pendRequest(r),
                child: const Text('معلقة'),
              ),
              const SizedBox(height: AppSpacing.sm),
              TextButton(
                onPressed: () => _rejectRequest(r),
                child: const Text('رفض'),
              ),
            ],
          ],
        ),
      ),
    );
  }

  Future<void> _prepareAndOpenSale(SalesWorkRequest request) async {
    try {
      final prepared = await SalesRepositoryFactory.instance.prepareSalesRequest(request.id);
      if (!mounted) return;
      await Navigator.pushNamed(context, '/sale', arguments: prepared);
      if (mounted) await _load();
    } on ApiException catch (e) {
      if (mounted) _toast(e.message);
    } catch (_) {
      if (mounted) _toast('تعذر تجهيز المبيع');
    }
  }

  Future<void> _pendRequest(SalesWorkRequest request) async {
    final ok = await _RequestNoteDialog.open(
      context,
      title: 'تعليق الطلب',
      label: 'الملاحظة *',
      confirm: 'إرسال',
      onConfirm: (note) =>
          SalesRepositoryFactory.instance.pendSalesRequest(request.id, note),
    );
    if (!ok || !mounted) return;
    await WidgetsBinding.instance.endOfFrame;
    if (!mounted) return;
    await _load();
  }

  Future<void> _rejectRequest(SalesWorkRequest request) async {
    final ok = await _RequestNoteDialog.open(
      context,
      title: 'رفض الطلب',
      label: 'سبب الرفض *',
      confirm: 'رفض',
      onConfirm: (reason) =>
          SalesRepositoryFactory.instance.rejectSalesRequest(request.id, reason),
    );
    if (!ok || !mounted) return;
    await WidgetsBinding.instance.endOfFrame;
    if (!mounted) return;
    await _load();
  }

  void _toast(String m) {
    if (!mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(m)));
  }
}

class SalesRequestDetailsScreen extends StatefulWidget {
  const SalesRequestDetailsScreen({super.key, required this.requestId});
  final int requestId;

  @override
  State<SalesRequestDetailsScreen> createState() => _SalesRequestDetailsScreenState();
}

class _SalesRequestDetailsScreenState extends State<SalesRequestDetailsScreen> {
  SalesWorkRequest? _row;
  String? _error;
  bool _busy = false;

  @override
  void initState() {
    super.initState();
    _open();
  }

  Future<void> _open() async {
    try {
      final viewed = await SalesRepositoryFactory.instance.viewSalesRequest(widget.requestId);
      if (!mounted) return;
      setState(() => _row = viewed);
    } catch (_) {
      if (!mounted) return;
      setState(() => _error = 'تعذر فتح الطلب');
    }
  }

  Future<void> _prepare() async {
    setState(() => _busy = true);
    try {
      final row = await SalesRepositoryFactory.instance.prepareSalesRequest(widget.requestId);
      if (!mounted) return;
      setState(() {
        _row = row;
        _busy = false;
      });
      await Navigator.pushNamed(context, '/sale', arguments: row);
    } on ApiException catch (e) {
      if (!mounted) return;
      setState(() => _busy = false);
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(e.message)));
    } catch (_) {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _pend() async {
    SalesWorkRequest? updated;
    final ok = await _RequestNoteDialog.open(
      context,
      title: 'تعليق الطلب',
      label: 'الملاحظة *',
      confirm: 'إرسال',
      onConfirm: (note) async {
        updated = await SalesRepositoryFactory.instance.pendSalesRequest(widget.requestId, note);
      },
    );
    if (!ok || !mounted) return;
    await WidgetsBinding.instance.endOfFrame;
    if (!mounted) return;
    setState(() {
      if (updated != null) _row = updated;
      _busy = false;
    });
  }

  Future<void> _reject() async {
    SalesWorkRequest? updated;
    final ok = await _RequestNoteDialog.open(
      context,
      title: 'رفض الطلب',
      label: 'سبب الرفض *',
      confirm: 'رفض',
      onConfirm: (reason) async {
        updated = await SalesRepositoryFactory.instance.rejectSalesRequest(widget.requestId, reason);
      },
    );
    if (!ok || !mounted) return;
    await WidgetsBinding.instance.endOfFrame;
    if (!mounted) return;
    setState(() {
      if (updated != null) _row = updated;
      _busy = false;
    });
  }

  @override
  Widget build(BuildContext context) {
    final row = _row;
    return Scaffold(
      appBar: AppBar(title: const Text('طلب مبيع')),
      body: row == null
          ? Center(child: _error == null ? const CircularProgressIndicator() : Text(_error!))
          : ListView(
              padding: const EdgeInsets.all(AppSpacing.md),
              children: [
                Text(row.customerName, style: const TextStyle(fontSize: 20, fontWeight: FontWeight.w700)),
                Text(row.customerPhone ?? ''),
                Text(row.customerProvince ?? ''),
                const SizedBox(height: AppSpacing.md),
                Text(row.notes ?? '', style: const TextStyle(color: AppColors.muted)),
                const SizedBox(height: AppSpacing.md),
                Text('الحالة: ${SalesRequestStatusLabels.of(row.status)}'),
                if (row.isReturned && (row.returnNote ?? '').trim().isNotEmpty) ...[
                  const SizedBox(height: AppSpacing.sm),
                  Text('ملاحظة الإعادة: ${row.returnNote}',
                      style: const TextStyle(color: AppColors.danger, fontWeight: FontWeight.w700)),
                ],
                if (row.isPendingHold && (row.pendingNote ?? '').trim().isNotEmpty)
                  Text('ملاحظة التعليق: ${row.pendingNote}'),
                const SizedBox(height: AppSpacing.lg),
                if (row.canAct) ...[
                  ElevatedButton(onPressed: _busy ? null : _prepare, child: const Text('تجهيز المبيع')),
                  const SizedBox(height: AppSpacing.sm),
                  OutlinedButton(onPressed: _busy ? null : _pend, child: const Text('معلقة')),
                  const SizedBox(height: AppSpacing.sm),
                  TextButton(onPressed: _busy ? null : _reject, child: const Text('رفض')),
                ],
              ],
            ),
    );
  }
}

class _StatusBadge extends StatelessWidget {
  const _StatusBadge({required this.status, required this.rejected});
  final String status;
  final bool rejected;

  @override
  Widget build(BuildContext context) {
    final label = SalesStatusLabels.of(status);
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
      decoration: BoxDecoration(
        color: rejected ? AppColors.warningSoft : AppColors.lightGreen.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(AppRadius.sm),
      ),
      child: Text(label,
          style: TextStyle(
            color: rejected ? AppColors.danger : AppColors.darkGreen,
            fontWeight: FontWeight.w700,
            fontSize: 12,
          )),
    );
  }
}

/// Owns the text controller for the full dialog lifetime so it is not disposed
/// while the TextField is still in the tree (which trips InheritedElement
/// `_dependents.isEmpty` after Navigator.pop).
/// The route is popped once, only after [onConfirm] succeeds, so the parent
/// never setStates during overlay teardown.
class _RequestNoteDialog extends StatefulWidget {
  const _RequestNoteDialog({
    required this.title,
    required this.label,
    required this.confirm,
    required this.onConfirm,
  });

  final String title;
  final String label;
  final String confirm;
  final Future<void> Function(String note) onConfirm;

  static Future<bool> open(
    BuildContext context, {
    required String title,
    required String label,
    required String confirm,
    required Future<void> Function(String note) onConfirm,
  }) async {
    if (!context.mounted) return false;
    final result = await showDialog<bool>(
      context: context,
      barrierDismissible: false,
      builder: (_) => _RequestNoteDialog(
        title: title,
        label: label,
        confirm: confirm,
        onConfirm: onConfirm,
      ),
    );
    return result == true;
  }

  @override
  State<_RequestNoteDialog> createState() => _RequestNoteDialogState();
}

class _RequestNoteDialogState extends State<_RequestNoteDialog> {
  late final TextEditingController _controller;
  var _canSubmit = false;
  var _closing = false;
  var _busy = false;
  String? _error;

  @override
  void initState() {
    super.initState();
    _controller = TextEditingController();
    _controller.addListener(_onText);
  }

  void _onText() {
    if (!mounted || _closing || _busy) return;
    final can = _controller.text.trim().isNotEmpty;
    if (can != _canSubmit) setState(() => _canSubmit = can);
  }

  void _pop([bool value = false]) {
    if (_closing || !mounted) return;
    _closing = true;
    Navigator.of(context).pop(value);
  }

  Future<void> _submit() async {
    if (_closing || _busy || !_canSubmit || !mounted) return;
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await widget.onConfirm(_controller.text.trim());
      if (!mounted) return;
      _pop(true);
    } on ApiException catch (e) {
      if (!mounted) return;
      setState(() {
        _busy = false;
        _error = e.message;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _busy = false;
        _error = 'تعذر إرسال الطلب';
      });
    }
  }

  @override
  void dispose() {
    _controller.removeListener(_onText);
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: Text(widget.title),
      content: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          TextField(
            controller: _controller,
            maxLines: 3,
            autofocus: true,
            enabled: !_busy,
            decoration: InputDecoration(labelText: widget.label),
          ),
          if (_error != null) ...[
            const SizedBox(height: AppSpacing.sm),
            Text(_error!, style: const TextStyle(color: AppColors.danger)),
          ],
        ],
      ),
      actions: [
        TextButton(onPressed: _busy ? null : () => _pop(), child: const Text('رجوع')),
        ElevatedButton(
          onPressed: _canSubmit && !_busy ? _submit : null,
          child: Text(widget.confirm),
        ),
      ],
    );
  }
}
