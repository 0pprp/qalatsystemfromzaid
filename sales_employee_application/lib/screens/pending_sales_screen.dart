import 'package:flutter/material.dart';
import 'package:sales_employee_application/data/sales_models.dart';
import 'package:sales_employee_application/data/sales_repository_factory.dart';
import 'package:sales_employee_application/screens/shift_screen.dart';
import 'package:sales_employee_application/services/api_client.dart';
import 'package:sales_employee_application/services/device_handover.dart';
import 'package:sales_employee_application/services/session.dart';
import 'package:sales_employee_application/tracking/shift_start_debug.dart';
import 'package:sales_employee_application/tracking/shift_tracking_controller.dart';
import 'package:sales_employee_application/tracking/work_shift.dart';
import 'package:sales_employee_application/utils/app_theme.dart';
import 'package:sales_employee_application/utils/sales_format.dart';
import 'package:sales_employee_application/widgets/tappable_phone.dart';

class PendingSalesScreen extends StatefulWidget {
  const PendingSalesScreen({super.key, this.binIndex});

  /// `null` = لوحة الحالات. غير ذلك = قائمة هذه الحالة فقط.
  final int? binIndex;

  @override
  State<PendingSalesScreen> createState() => _PendingSalesScreenState();
}

class _PendingSalesScreenState extends State<PendingSalesScreen> {
  static const _bins = [
    (title: 'طلبات البيع', empty: 'لا توجد طلبات بيع', icon: Icons.assignment_outlined),
    (title: 'جاهز للبيع', empty: 'لا توجد طلبات جاهزة للبيع', icon: Icons.storefront_outlined),
    (title: 'تم البيع', empty: 'لا توجد مبيعات مكتملة', icon: Icons.check_circle_outline),
    (title: 'معلقة', empty: 'لا توجد طلبات معلقة', icon: Icons.pause_circle_outline),
    (title: 'مرفوض', empty: 'لا توجد طلبات مرفوضة', icon: Icons.cancel_outlined),
  ];

  List<SalesWorkRequest> _requests = [];
  WorkShift? _shift;
  bool _loading = true;
  bool _shiftBusy = false;
  String? _error;
  String? _shiftError;
  bool _openedToday = false;

  bool get _isDashboard => widget.binIndex == null;

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    if (!_isDashboard || _openedToday) return;
    final arg = ModalRoute.of(context)?.settings.arguments;
    if (arg == 'today' || arg == 2) {
      _openedToday = true;
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (mounted) _openBin(2);
      });
    }
  }

  Future<void> _load() async {
    if (!mounted) return;
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final requests = await SalesRepositoryFactory.instance.salesRequests();
      final shift = _readLocalShift();
      if (!mounted) return;
      setState(() {
        _requests = requests;
        _shift = shift;
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

  int _binCount(int index) => _forBin(index).length;

  WorkShift? _readLocalShift() {
    try {
      if (Session.gpsStoppedByUser) return null;
    } catch (_) {}
    return TrackingRuntime.instance?.activeShift;
  }

  String _iraqClock(DateTime utc) {
    final iraq = utc.toUtc().add(const Duration(hours: 3));
    final h = iraq.hour.toString().padLeft(2, '0');
    final m = iraq.minute.toString().padLeft(2, '0');
    return '$h:$m';
  }

  Future<void> _startShift() async {
    if (_shiftBusy) return;
    setState(() {
      _shiftBusy = true;
      _shiftError = null;
    });
    final controller = TrackingRuntime.instance ??=
        ShiftTrackingController(repository: SalesRepositoryFactory.instance);
    try {
      final shift = await controller.startShiftFlow();
      if (!mounted) return;
      setState(() {
        _shiftBusy = false;
        _shift = shift;
        if (shift == null) {
          _shiftError = controller.lastError ?? ShiftStartDebug.generic;
        }
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _shiftBusy = false;
        _shiftError = ShiftStartDebug.apiFailure(e);
      });
    }
  }

  Future<void> _endShift() async {
    if (_shiftBusy) return;
    setState(() {
      _shiftBusy = true;
      _shiftError = null;
    });
    final controller = TrackingRuntime.instance ??=
        ShiftTrackingController(repository: SalesRepositoryFactory.instance);
    try {
      await controller.endShiftFlow();
      if (!mounted) return;
      Navigator.of(context).pushAndRemoveUntil(
        MaterialPageRoute(builder: (_) => const ShiftScreen()),
        (route) => false,
      );
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _shiftBusy = false;
        _shiftError = 'تعذر إنهاء الدوام';
      });
    }
  }

  List<SalesWorkRequest> _forBin(int index) {
    switch (index) {
      case 0:
        return _requests.where((r) => r.isIncoming).toList();
      case 1:
        return _requests.where((r) => r.isPreparedForSale).toList();
      case 2:
        return _requests.where((r) => r.isSold).toList();
      case 3:
        return _requests.where((r) => r.isPendingHold).toList();
      default:
        return _requests.where((r) => r.status == 'Rejected').toList();
    }
  }

  Future<void> _openBin(int index) async {
    await Navigator.push(
      context,
      MaterialPageRoute(builder: (_) => PendingSalesScreen(binIndex: index)),
    );
    if (mounted) await _load();
  }

  @override
  Widget build(BuildContext context) {
    if (_isDashboard) {
      return Scaffold(
        appBar: AppBar(
          title: Text(Session.userName.isEmpty ? 'موظف المبيعات' : Session.userName),
          actions: [
            IconButton(
              tooltip: 'مخزن الفرع',
              onPressed: () => Navigator.pushNamed(context, '/warehouse'),
              icon: const Icon(Icons.inventory_2_outlined),
            ),
            TextButton(
              onPressed: () async {
                await DeviceHandover.kickPreviousUser(endServerShift: false);
                if (context.mounted) {
                  Navigator.pushNamedAndRemoveUntil(context, '/login', (_) => false);
                }
              },
              child: const Text('خروج'),
            ),
          ],
        ),
        body: Column(
          children: [
            _shiftCard(),
            Padding(
              padding: const EdgeInsets.fromLTRB(AppSpacing.md, AppSpacing.sm, AppSpacing.md, 0),
              child: SizedBox(
                width: double.infinity,
                height: 48,
                child: ElevatedButton.icon(
                  onPressed: () => Navigator.pushNamed(context, '/submit-request'),
                  icon: const Icon(Icons.send_outlined),
                  label: const Text('إرسال طلب بيع'),
                ),
              ),
            ),
            Expanded(child: _dashboardBody()),
          ],
        ),
      );
    }

    final bin = _bins[widget.binIndex!];
    return Scaffold(
      appBar: AppBar(title: Text(bin.title)),
      body: _requestsList(widget.binIndex!, bin.empty),
    );
  }

  Widget _dashboardBody() {
    if (_loading) return const Center(child: CircularProgressIndicator());
    if (_error != null) {
      return Center(child: Text(_error!, style: const TextStyle(color: AppColors.danger)));
    }
    return RefreshIndicator(
      onRefresh: _load,
      child: ListView.separated(
        padding: const EdgeInsets.fromLTRB(AppSpacing.md, AppSpacing.md, AppSpacing.md, AppSpacing.lg),
        itemCount: _bins.length,
        separatorBuilder: (_, _) => const SizedBox(height: AppSpacing.md),
        itemBuilder: (context, i) => _binCard(i),
      ),
    );
  }

  Widget _binCard(int index) {
    final bin = _bins[index];
    final count = _binCount(index);
    return Card(
      child: InkWell(
        onTap: () => _openBin(index),
        borderRadius: BorderRadius.circular(AppRadius.md),
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: AppSpacing.md, vertical: AppSpacing.lg),
          child: Row(
            children: [
              Icon(bin.icon, size: 36, color: AppColors.darkGreen),
              const SizedBox(width: AppSpacing.md),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      bin.title,
                      style: const TextStyle(
                        fontWeight: FontWeight.w700,
                        fontSize: 18,
                        color: AppColors.darkGreen,
                      ),
                    ),
                    const SizedBox(height: 4),
                    Text(
                      '$count',
                      style: const TextStyle(fontSize: 22, fontWeight: FontWeight.w800),
                    ),
                  ],
                ),
              ),
              const Icon(Icons.chevron_left, color: AppColors.muted, size: 28),
            ],
          ),
        ),
      ),
    );
  }

  Widget _shiftCard() {
    final shift = _shift;
    var stopped = false;
    try {
      stopped = Session.gpsStoppedByUser;
    } catch (_) {}
    final active = shift != null && shift.isActive && !stopped;
    return Card(
      margin: const EdgeInsets.fromLTRB(AppSpacing.md, AppSpacing.sm, AppSpacing.md, 0),
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.md),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text(
              active ? 'حالة الدوام: أثناء الدوام' : 'حالة الدوام',
              style: const TextStyle(fontWeight: FontWeight.w700, color: AppColors.darkGreen),
            ),
            if (active) ...[
              const SizedBox(height: 4),
              Text('وقت البدء: ${_iraqClock(shift.startedAtUtc)}'),
              const SizedBox(height: AppSpacing.sm),
              ElevatedButton(
                onPressed: _shiftBusy ? null : _endShift,
                child: Text(_shiftBusy ? '...' : 'إنهاء الدوام'),
              ),
            ] else ...[
              const SizedBox(height: AppSpacing.sm),
              ElevatedButton(
                onPressed: _shiftBusy ? null : _startShift,
                child: Text(_shiftBusy ? '...' : 'بدء الدوام'),
              ),
            ],
            if (_shiftError != null) ...[
              const SizedBox(height: AppSpacing.sm),
              Text(_shiftError!, style: const TextStyle(color: AppColors.danger)),
            ],
          ],
        ),
      ),
    );
  }

  Widget _requestsList(int index, String empty) {
    if (_loading) return const Center(child: CircularProgressIndicator());
    if (_error != null) {
      return Center(child: Text(_error!, style: const TextStyle(color: AppColors.danger)));
    }
    final rows = _forBin(index);
    if (rows.isEmpty) {
      return Center(child: Text(empty, style: const TextStyle(color: AppColors.muted)));
    }
    return RefreshIndicator(
      onRefresh: _load,
      child: ListView.builder(
        padding: const EdgeInsets.all(AppSpacing.md),
        itemCount: rows.length,
        itemBuilder: (context, i) => _requestCard(rows[i]),
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
                  if ((r.customerProvince ?? '').trim().isNotEmpty)
                    Text(
                      r.customerProvince!,
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
            if ((r.customerPhone ?? '').trim().isNotEmpty) ...[
              const SizedBox(height: 4),
              Align(alignment: Alignment.centerRight, child: TappablePhone(r.customerPhone)),
            ],
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
            if (r.status == 'Rejected' && (r.rejectionReason ?? '').trim().isNotEmpty)
              Padding(
                padding: const EdgeInsets.only(top: AppSpacing.sm),
                child: Text('سبب الرفض: ${r.rejectionReason}',
                    style: const TextStyle(color: AppColors.danger)),
              ),
            if (r.canAct) ...[
              const SizedBox(height: AppSpacing.md),
              if (r.canPrepare)
                ElevatedButton(
                  onPressed: () => _prepareRequest(r),
                  child: const Text('جاهز للبيع'),
                ),
              if (r.canConvert || r.canContinueSale) ...[
                if (r.canPrepare) const SizedBox(height: AppSpacing.sm),
                ElevatedButton(
                  onPressed: () => _openCheckout(r),
                  child: const Text('إنشاء بيع'),
                ),
              ],
              if (r.canPend) ...[
                const SizedBox(height: AppSpacing.sm),
                OutlinedButton(
                  onPressed: () => _pendRequest(r),
                  child: const Text('معلقة'),
                ),
              ],
              if (r.canReject) ...[
                const SizedBox(height: AppSpacing.sm),
                TextButton(
                  onPressed: () => _rejectRequest(r),
                  child: const Text('مرفوض'),
                ),
              ],
            ],
          ],
        ),
      ),
    );
  }

  Future<void> _openCheckout(SalesWorkRequest request) async {
    try {
      await Navigator.pushNamed(context, '/sale', arguments: request);
      if (mounted) await _load();
    } catch (_) {
      if (mounted) _toast('تعذر فتح عملية البيع');
    }
  }

  Future<void> _prepareRequest(SalesWorkRequest request) async {
    try {
      await SalesRepositoryFactory.instance.prepareSalesRequest(request.id);
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

  Future<void> _openCheckout() async {
    final row = _row;
    if (row == null) return;
    setState(() => _busy = true);
    try {
      await Navigator.pushNamed(context, '/sale', arguments: row);
    } finally {
      if (mounted) setState(() => _busy = false);
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
                TappablePhone(row.customerPhone),
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
                if (row.status == 'Rejected' && (row.rejectionReason ?? '').trim().isNotEmpty)
                  Text('سبب الرفض: ${row.rejectionReason}',
                      style: const TextStyle(color: AppColors.danger)),
                const SizedBox(height: AppSpacing.lg),
                if (row.canAct) ...[
                  if (row.canPrepare) ...[
                    ElevatedButton(onPressed: _busy ? null : _prepare, child: const Text('جاهز للبيع')),
                    const SizedBox(height: AppSpacing.sm),
                  ],
                  if (row.canConvert || row.canContinueSale) ...[
                    ElevatedButton(onPressed: _busy ? null : _openCheckout, child: const Text('إنشاء بيع')),
                    const SizedBox(height: AppSpacing.sm),
                  ],
                  if (row.canPend) ...[
                    OutlinedButton(onPressed: _busy ? null : _pend, child: const Text('معلقة')),
                    const SizedBox(height: AppSpacing.sm),
                  ],
                  if (row.canReject)
                    TextButton(onPressed: _busy ? null : _reject, child: const Text('مرفوض')),
                ],
              ],
            ),
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
