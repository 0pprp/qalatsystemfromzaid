import 'package:sales_employee_application/widgets/company_loader.dart';
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
import 'package:sales_employee_application/widgets/home_price_board_banner.dart';
import 'package:sales_employee_application/widgets/tappable_phone.dart';

class PendingSalesScreen extends StatefulWidget {
  const PendingSalesScreen({super.key, this.binIndex});

  /// `null` = لوحة الحالات. غير ذلك = قائمة هذه الحالة فقط.
  final int? binIndex;

  @override
  State<PendingSalesScreen> createState() => _PendingSalesScreenState();
}

class _PendingSalesScreenState extends State<PendingSalesScreen> {
  static const incomingBin = 0;
  static const readyBin = 1;
  static const inspectedBin = 2;
  static const pendingBin = 3;
  static const rejectedBin = 4;
  static const soldBin = 5;

  static const _bins = [
    (title: 'طلبات البيع', empty: 'لا توجد طلبات بيع', icon: Icons.assignment_outlined),
    (title: 'جاهز للبيع', empty: 'لا توجد طلبات جاهزة للبيع', icon: Icons.storefront_outlined),
    (title: 'تم الكشف', empty: 'لا توجد طلبات تم الكشف عنها', icon: Icons.travel_explore_outlined),
    (title: 'معلقة', empty: 'لا توجد طلبات معلقة', icon: Icons.pause_circle_outline),
    (title: 'مرفوض', empty: 'لا توجد طلبات مرفوضة', icon: Icons.cancel_outlined),
    (title: 'تم البيع', empty: 'لا توجد مبيعات مكتملة', icon: Icons.check_circle_outline),
  ];

  List<SalesWorkRequest> _requests = [];
  WorkShift? _shift;
  bool _loading = true;
  bool _shiftBusy = false;
  String? _error;
  String? _shiftError;
  bool _openedToday = false;
  final _homeSearch = TextEditingController();
  String _homeQuery = '';

  bool get _isDashboard => widget.binIndex == null;

  @override
  void initState() {
    super.initState();
    _homeSearch.addListener(() {
      final q = _homeSearch.text.trim();
      if (q == _homeQuery) return;
      setState(() => _homeQuery = q);
    });
    _load();
  }

  @override
  void dispose() {
    _homeSearch.dispose();
    super.dispose();
  }

  List<SalesWorkRequest> _searchMatches(String query) {
    final q = query.trim().toLowerCase();
    if (q.isEmpty) return const [];
    final digits = q.replaceAll(RegExp(r'\D'), '');
    return _requests.where((r) {
      final name = r.customerName.toLowerCase();
      final province = (r.customerProvince ?? '').toLowerCase();
      final notes = (r.notes ?? '').toLowerCase();
      final phone = (r.customerPhone ?? '').replaceAll(RegExp(r'\D'), '');
      if (name.contains(q) || province.contains(q) || notes.contains(q)) {
        return true;
      }
      if (digits.isNotEmpty && phone.contains(digits)) {
        return true;
      }
      return false;
    }).toList();
  }

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    if (!_isDashboard || _openedToday) return;
    final arg = ModalRoute.of(context)?.settings.arguments;
    if (arg == 'today' || arg == 'sold' || arg == soldBin) {
      _openedToday = true;
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (mounted) _openBin(soldBin);
      });
    } else if (arg == 'inspected' || arg == inspectedBin) {
      _openedToday = true;
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (mounted) _openBin(inspectedBin);
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
      case incomingBin:
        return _requests.where((r) => r.isIncoming).toList();
      case readyBin:
        return _requests.where((r) => r.isPreparedForSale).toList();
      case inspectedBin:
        return _requests.where((r) => r.isInspected).toList();
      case pendingBin:
        return _requests.where((r) => r.isPendingHold).toList();
      case rejectedBin:
        return _requests.where((r) => r.status == 'Rejected').toList();
      default:
        return _requests.where((r) => r.isSold).toList();
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
              tooltip: 'لوحة الأسعار',
              onPressed: () => Navigator.pushNamed(context, '/price-board'),
              icon: const Icon(Icons.sell_outlined),
            ),
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
        body: _dashboardBody(),
      );
    }

    final bin = _bins[widget.binIndex!];
    return Scaffold(
      appBar: AppBar(title: Text(bin.title)),
      body: _requestsList(widget.binIndex!, bin.empty),
    );
  }

  Widget _dashboardBody() {
    final searching = _homeQuery.isNotEmpty;
    final matches = searching ? _searchMatches(_homeQuery) : const <SalesWorkRequest>[];
    return RefreshIndicator(
      onRefresh: _load,
      child: ListView(
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.fromLTRB(AppSpacing.md, AppSpacing.md, AppSpacing.md, AppSpacing.lg),
        children: [
          Row(children: [
            Image.asset('assets/icons/LogoCompany.png', width: 48, height: 52, fit: BoxFit.contain),
            const SizedBox(width: 12),
            const Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Text('مساحة المبيعات', style: TextStyle(fontSize: 24, fontWeight: FontWeight.w700)),
              Text('تابع دوامك وطلبات زبائنك من مكان واحد', style: TextStyle(color: AppColors.muted, fontSize: 13)),
            ])),
          ]),
          const SizedBox(height: 20),
          _shiftCard(),
          const SizedBox(height: 16),
          ElevatedButton.icon(
            onPressed: () => Navigator.pushNamed(context, '/submit-request'),
            icon: const Icon(Icons.add_rounded),
            label: const Text('إرسال طلب بيع'),
          ),
          const SizedBox(height: 24),
          const HomePriceBoardBanner(),
          const SizedBox(height: AppSpacing.md),
          TextField(
            controller: _homeSearch,
            textInputAction: TextInputAction.search,
            decoration: InputDecoration(
              hintText: 'ابحث باسم الزبون أو رقم الهاتف',
              prefixIcon: const Icon(Icons.search),
              suffixIcon: _homeQuery.isEmpty
                  ? null
                  : IconButton(
                      tooltip: 'مسح',
                      onPressed: () {
                        _homeSearch.clear();
                        setState(() => _homeQuery = '');
                      },
                      icon: const Icon(Icons.clear),
                    ),
              border: OutlineInputBorder(borderRadius: BorderRadius.circular(AppRadius.md)),
              isDense: true,
            ),
          ),
          const SizedBox(height: AppSpacing.md),
          if (_loading)
            const Padding(padding: EdgeInsets.all(24), child: CompanyLoader(label: 'جارٍ تحميل الطلبات'))
          else if (_error != null) ...[
            Text(_error!, style: const TextStyle(color: AppColors.danger)),
            TextButton.icon(onPressed: _load, icon: const Icon(Icons.refresh), label: const Text('إعادة المحاولة')),
          ] else if (searching) ...[
            Text(
              matches.isEmpty ? 'لا نتائج' : 'نتائج البحث (${matches.length})',
              style: const TextStyle(fontWeight: FontWeight.w700, color: AppColors.darkGreen),
            ),
            const SizedBox(height: AppSpacing.sm),
            if (matches.isEmpty)
              const Padding(
                padding: EdgeInsets.symmetric(vertical: AppSpacing.lg),
                child: Text('لا توجد نتائج مطابقة، جرّب اسمًا أو رقمًا آخر', style: TextStyle(color: AppColors.muted)),
              )
            else
              for (final r in matches) ...[
                _requestCard(r),
                const SizedBox(height: AppSpacing.sm),
              ],
          ] else ...[
            const Text('متابعة الطلبات', style: TextStyle(fontSize: 18, fontWeight: FontWeight.w700)),
            const SizedBox(height: 4),
            const Text('اختر الحالة لعرض الطلبات وتفاصيلها', style: TextStyle(color: AppColors.muted, fontSize: 13)),
            const SizedBox(height: 16),
            for (var i = 0; i < _bins.length; i++) _binCard(i),
          ],
        ],
      ),
    );
  }

  Widget _binCard(int index) {
    final bin = _bins[index];
    final count = _binCount(index);
    const colors = [AppColors.darkGreen, Color(0xFF006879), Color(0xFF6750A4),
      Color(0xFF946200), AppColors.danger, Color(0xFF1D6B50)];
    final color = colors[index];
    return Card(
      child: InkWell(
        onTap: () => _openBin(index),
        borderRadius: BorderRadius.circular(AppRadius.md),
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Row(children: [
            Container(
              padding: const EdgeInsets.all(12),
              decoration: BoxDecoration(color: color.withValues(alpha: 0.08), borderRadius: BorderRadius.circular(12)),
              child: Icon(bin.icon, color: color, size: 24),
            ),
            const SizedBox(width: 14),
            Expanded(child: Text(bin.title, style: const TextStyle(fontSize: 16, fontWeight: FontWeight.w600))),
            const SizedBox(width: 8),
            Text('$count', style: TextStyle(fontSize: 26, fontWeight: FontWeight.w700, color: color)),
            const SizedBox(width: 12),
            const Icon(Icons.chevron_left_rounded, color: AppColors.muted, size: 22),
          ]),
        ),
      ),
    );
  }

  Widget _shiftCard() {
    final shift = _shift;
    var stopped = false;
    try { stopped = Session.gpsStoppedByUser; } catch (_) {}
    final active = shift != null && shift.isActive && !stopped;
    return Container(
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        color: const Color(0xFFF4FBF9),
        border: Border.all(color: const Color(0xFFCCE1E0)),
        borderRadius: BorderRadius.circular(16),
      ),
      child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
        Row(children: [
          const Icon(Icons.schedule_rounded, color: AppColors.darkGreen, size: 24),
          const SizedBox(width: 10),
          const Expanded(child: Text('دوام اليوم', style: TextStyle(fontSize: 16, fontWeight: FontWeight.w700))),
          Flexible(child: Text(active ? 'أثناء الدوام' : 'غير نشط',
              style: const TextStyle(color: AppColors.darkGreen, fontWeight: FontWeight.w600))),
        ]),
        const SizedBox(height: 10),
        Text(active ? 'بدأ الدوام الساعة ${_iraqClock(shift.startedAtUtc)}' : 'ابدأ دوامك لمتابعة مهام المبيعات',
            style: const TextStyle(color: AppColors.muted, fontSize: 13)),
        const SizedBox(height: 14),
        OutlinedButton(
          onPressed: _shiftBusy ? null : (active ? _endShift : _startShift),
          style: OutlinedButton.styleFrom(
            foregroundColor: AppColors.darkGreen,
            side: const BorderSide(color: Color(0xFFCCE1E0)),
            padding: const EdgeInsets.symmetric(vertical: 12),
            shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
          ),
          child: _shiftBusy ? const CompanyLoader(size: 24) : Text(active ? 'إنهاء الدوام' : 'بدء الدوام'),
        ),
        if (_shiftError != null) ...[
          const SizedBox(height: 12),
          Text(_shiftError!, style: const TextStyle(color: AppColors.danger)),
        ],
      ]),
    );
  }

  Widget _requestsList(int index, String empty) {
    if (_loading) return const Center(child: CompanyLoader(label: 'جارٍ تحميل البيانات'));
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
            if (r.isPreparedForSale && (r.preparedForSaleNote ?? '').trim().isNotEmpty)
              Padding(
                padding: const EdgeInsets.only(top: AppSpacing.sm),
                child: Text(
                  'ملاحظة البيع: ${r.preparedForSaleNote}',
                  style: const TextStyle(fontWeight: FontWeight.w700),
                ),
              ),
            if (r.status == 'Rejected' && (r.rejectionReason ?? '').trim().isNotEmpty)
              Padding(
                padding: const EdgeInsets.only(top: AppSpacing.sm),
                child: Text('سبب الرفض: ${r.rejectionReason}',
                    style: const TextStyle(color: AppColors.danger)),
              ),
            if (r.canAct) ...[
              const SizedBox(height: AppSpacing.md),
              _SalesRequestActions(
                request: r,
                onContinue: () => _openCheckout(r),
                onPrepare: () => _prepareRequest(r),
                onPend: () => _pendRequest(r),
                onReject: () => _rejectRequest(r),
                onTransfer: () => _transferRequest(r),
              ),
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
    final ok = await _RequestNoteDialog.open(
      context,
      title: 'جاهز للبيع',
      label: 'ملاحظة جاهز للبيع *',
      confirm: 'تأكيد',
      onConfirm: (note) =>
          SalesRepositoryFactory.instance.prepareSalesRequest(request.id, note),
    );
    if (!ok || !mounted) return;
    await WidgetsBinding.instance.endOfFrame;
    if (!mounted) return;
    await _load();
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

  Future<void> _transferRequest(SalesWorkRequest request) async {
    try {
      final peers = await SalesRepositoryFactory.instance.transferPeers();
      if (!mounted) return;
      if (peers.isEmpty) {
        _toast('لا يوجد زملاء مبيعات متاحون للنقل');
        return;
      }
      final result = await showDialog<_TransferResult>(
        context: context,
        builder: (ctx) => _TransferNameDialog(peers: peers),
      );
      if (result == null || !mounted) return;
      await SalesRepositoryFactory.instance.transferSalesRequestName(
        request.id,
        result.toEmployeeId,
        result.reason,
      );
      if (!mounted) return;
      _toast('تم نقل الاسم بنجاح');
      await _load();
    } catch (e) {
      final msg = e is ApiException ? e.message : e.toString();
      _toast(msg);
    }
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
    SalesWorkRequest? updated;
    final ok = await _RequestNoteDialog.open(
      context,
      title: 'جاهز للبيع',
      label: 'ملاحظة جاهز للبيع *',
      confirm: 'تأكيد',
      onConfirm: (note) async {
        updated = await SalesRepositoryFactory.instance.prepareSalesRequest(widget.requestId, note);
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

  Future<void> _transferName() async {
    if (_busy) return;
    setState(() => _busy = true);
    try {
      final peers = await SalesRepositoryFactory.instance.transferPeers();
      if (!mounted) return;
      if (peers.isEmpty) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('لا يوجد زملاء مبيعات متاحون للنقل')),
        );
        return;
      }

      final result = await showDialog<_TransferResult>(
        context: context,
        builder: (ctx) => _TransferNameDialog(peers: peers),
      );
      if (result == null || !mounted) return;

      await SalesRepositoryFactory.instance.transferSalesRequestName(
        widget.requestId,
        result.toEmployeeId,
        result.reason,
      );
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('تم نقل الاسم بنجاح')),
      );
      Navigator.of(context).pop(true);
    } catch (e) {
      if (!mounted) return;
      final msg = e is ApiException ? e.message : e.toString();
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(msg)));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final row = _row;
    return Scaffold(
      appBar: AppBar(title: const Text('طلب مبيع')),
      body: row == null
          ? Center(child: _error == null ? const CompanyLoader(label: 'جارٍ تحميل البيانات') : Text(_error!))
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
                if (row.isPreparedForSale && (row.preparedForSaleNote ?? '').trim().isNotEmpty)
                  Text(
                    'ملاحظة البيع: ${row.preparedForSaleNote}',
                    style: const TextStyle(fontWeight: FontWeight.w700),
                  ),
                if (row.status == 'Rejected' && (row.rejectionReason ?? '').trim().isNotEmpty)
                  Text('سبب الرفض: ${row.rejectionReason}',
                      style: const TextStyle(color: AppColors.danger)),
                if (row.isTransferred && row.latestNameTransfer != null) ...[
                  const SizedBox(height: AppSpacing.md),
                  Card(
                    color: AppColors.card,
                    child: Padding(
                      padding: const EdgeInsets.all(AppSpacing.md),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          const Text('طلب منقول', style: TextStyle(fontWeight: FontWeight.w700)),
                          const SizedBox(height: AppSpacing.xs),
                          Text('من: ${row.latestNameTransfer!.fromEmployeeName}'),
                          Text('السبب: ${row.latestNameTransfer!.transferReason}'),
                          Text(
                            'التاريخ: ${SalesFormat.iraqDateTime(row.latestNameTransfer!.transferredAtUtc)}',
                            style: const TextStyle(color: AppColors.muted),
                          ),
                        ],
                      ),
                    ),
                  ),
                ],
                const SizedBox(height: AppSpacing.lg),
                if (row.canAct)
                  _SalesRequestActions(
                    request: row,
                    enabled: !_busy,
                    onContinue: _openCheckout,
                    onPrepare: _prepare,
                    onPend: _pend,
                    onReject: _reject,
                    onTransfer: _transferName,
                  ),
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
class _SalesRequestActions extends StatelessWidget {
  const _SalesRequestActions({
    required this.request,
    required this.onContinue,
    required this.onPrepare,
    required this.onPend,
    required this.onReject,
    required this.onTransfer,
    this.enabled = true,
  });

  final SalesWorkRequest request;
  final VoidCallback onContinue;
  final VoidCallback onPrepare;
  final VoidCallback onPend;
  final VoidCallback onReject;
  final VoidCallback onTransfer;
  final bool enabled;

  @override
  Widget build(BuildContext context) {
    final actions = request.availableActions;
    final children = <Widget>[];
    for (final action in actions) {
      if (children.isNotEmpty) {
        children.add(const SizedBox(height: AppSpacing.sm));
      }
      children.add(_button(action));
    }
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: children,
    );
  }

  Widget _button(String action) {
    switch (action) {
      case 'continue':
        return ElevatedButton(
          onPressed: enabled ? onContinue : null,
          child: const Text('متابعة البيع'),
        );
      case 'create':
        return ElevatedButton(
          onPressed: enabled ? onContinue : null,
          child: const Text('إنشاء بيع'),
        );
      case 'prepared':
        return ElevatedButton(
          onPressed: enabled ? onPrepare : null,
          child: const Text('جاهز للبيع'),
        );
      case 'pending':
        return OutlinedButton(
          onPressed: enabled ? onPend : null,
          child: const Text('معلّق'),
        );
      case 'rejected':
        return TextButton(
          onPressed: enabled ? onReject : null,
          child: const Text('مرفوض'),
        );
      case 'transfer':
        return OutlinedButton(
          onPressed: enabled ? onTransfer : null,
          child: const Text('نقل الاسم'),
        );
      default:
        return const SizedBox.shrink();
    }
  }
}

class _TransferResult {
  const _TransferResult({required this.toEmployeeId, required this.reason});
  final int toEmployeeId;
  final String reason;
}

class _TransferNameDialog extends StatefulWidget {
  const _TransferNameDialog({required this.peers});
  final List<SalesTransferPeer> peers;

  @override
  State<_TransferNameDialog> createState() => _TransferNameDialogState();
}

class _TransferNameDialogState extends State<_TransferNameDialog> {
  SalesTransferPeer? _selected;
  final _reason = TextEditingController();
  String? _error;
  bool _saving = false;

  @override
  void dispose() {
    _reason.dispose();
    super.dispose();
  }

  Future<void> _confirm() async {
    final peer = _selected;
    final reason = _reason.text.trim();
    if (peer == null) {
      setState(() => _error = 'اختر الموظف');
      return;
    }
    if (reason.isEmpty) {
      setState(() => _error = 'سبب نقل الاسم مطلوب');
      return;
    }
    setState(() {
      _saving = true;
      _error = null;
    });
    if (!mounted) return;
    Navigator.of(context).pop(_TransferResult(toEmployeeId: peer.employeeId, reason: reason));
  }

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: const Text('نقل الاسم'),
      content: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          DropdownButtonFormField<SalesTransferPeer>(
            // ignore: deprecated_member_use
            value: _selected,
            decoration: const InputDecoration(labelText: 'الموظف'),
            items: widget.peers
                .map((p) => DropdownMenuItem(value: p, child: Text(p.employeeName)))
                .toList(),
            onChanged: _saving ? null : (v) => setState(() => _selected = v),
          ),
          const SizedBox(height: 12),
          TextField(
            controller: _reason,
            enabled: !_saving,
            maxLines: 3,
            decoration: const InputDecoration(labelText: 'سبب نقل الاسم *'),
          ),
          if (_error != null) ...[
            const SizedBox(height: 8),
            Text(_error!, style: const TextStyle(color: AppColors.danger)),
          ],
        ],
      ),
      actions: [
        TextButton(onPressed: _saving ? null : () => Navigator.pop(context), child: const Text('إلغاء')),
        ElevatedButton(onPressed: _saving ? null : _confirm, child: const Text('تأكيد النقل')),
      ],
    );
  }
}

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
