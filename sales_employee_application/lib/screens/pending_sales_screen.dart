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
  List<SalesWorkRequest> _requests = [];
  bool _loading = true;
  String? _error;

  @override
  void initState() {
    super.initState();
    _tabs = TabController(length: 2, vsync: this);
    _load();
  }

  @override
  void dispose() {
    _tabs.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final rows = await SalesRepositoryFactory.instance.pending();
      final requests = await SalesRepositoryFactory.instance.salesRequests();
      if (!mounted) return;
      setState(() {
        _rows = rows;
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
          labelColor: AppColors.darkGreen,
          tabs: [
            const Tab(text: 'المبيعات المعلقة'),
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
          _pendingTab(),
          _requestsTab(),
        ],
      ),
    );
  }

  Widget _pendingTab() {
    if (_loading) return const Center(child: CircularProgressIndicator());
    if (_error != null) {
      return Center(child: Text(_error!, style: const TextStyle(color: AppColors.danger)));
    }
    if (_rows.isEmpty) {
      return const Center(child: Text('لا توجد عمليات', style: TextStyle(color: AppColors.muted)));
    }
    return RefreshIndicator(
      onRefresh: _load,
      child: ListView.builder(
        padding: const EdgeInsets.all(AppSpacing.md),
        itemCount: _rows.length,
        itemBuilder: (context, i) {
          final d = _rows[i];
          return Card(
            child: ListTile(
              title: Text(d.fullName, style: const TextStyle(fontWeight: FontWeight.w700)),
              subtitle: Text(
                'رقم ${d.saleId} · ${EvaluationLabels.of(d.evaluationLevel)}\n${MoneyFormat.iqd(d.finalSalePrice)}',
              ),
              trailing: _StatusBadge(rejected: d.isRejected),
              onTap: () => Navigator.pushNamed(context, '/sale-details', arguments: d.saleId),
            ),
          );
        },
      ),
    );
  }

  Widget _requestsTab() {
    if (_loading) return const Center(child: CircularProgressIndicator());
    if (_requests.isEmpty) {
      return const Center(child: Text('لا توجد طلبات مبيعات', style: TextStyle(color: AppColors.muted)));
    }
    return RefreshIndicator(
      onRefresh: _load,
      child: ListView.builder(
        padding: const EdgeInsets.all(AppSpacing.md),
        itemCount: _requests.length,
        itemBuilder: (context, i) {
          final r = _requests[i];
          return Card(
            child: ListTile(
              title: Text(r.customerName, style: const TextStyle(fontWeight: FontWeight.w700)),
              subtitle: Text(
                '${r.customerPhone ?? ''} · ${r.customerProvince ?? ''}\n${r.notes ?? ''}',
              ),
              trailing: Text(_requestStatusAr(r.status), style: const TextStyle(fontSize: 12, color: AppColors.darkGreen)),
              onTap: () async {
                await Navigator.push(context, MaterialPageRoute(builder: (_) => SalesRequestDetailsScreen(requestId: r.id)));
                await _load();
              },
            ),
          );
        },
      ),
    );
  }
}

String _requestStatusAr(String status) => switch (status) {
      'New' => 'جديد',
      'Viewed' => 'تمت المشاهدة',
      'InProgress' => 'قيد المعالجة',
      'ConvertedToSale' => 'تحول إلى بيع',
      'Completed' => 'مكتمل',
      'Rejected' => 'مرفوض',
      _ => status,
    };

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

  Future<void> _start() async {
    setState(() => _busy = true);
    try {
      final row = await SalesRepositoryFactory.instance.startSalesRequest(widget.requestId);
      if (!mounted) return;
      setState(() {
        _row = row;
        _busy = false;
      });
    } catch (_) {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _reject() async {
    final controller = TextEditingController();
    final reason = await showDialog<String>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('رفض الطلب'),
        content: TextField(controller: controller, decoration: const InputDecoration(labelText: 'سبب الرفض *')),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx), child: const Text('رجوع')),
          ElevatedButton(onPressed: () => Navigator.pop(ctx, controller.text), child: const Text('رفض')),
        ],
      ),
    );
    if (reason == null || !mounted) return;
    if (reason.trim().isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('سبب الرفض مطلوب')));
      return;
    }
    setState(() => _busy = true);
    try {
      final row = await SalesRepositoryFactory.instance.rejectSalesRequest(widget.requestId, reason.trim());
      if (!mounted) return;
      setState(() {
        _row = row;
        _busy = false;
      });
    } catch (_) {
      if (mounted) setState(() => _busy = false);
    }
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
                Text('الحالة: ${_requestStatusAr(row.status)}'),
                const SizedBox(height: AppSpacing.lg),
                if (row.status != 'Rejected' && row.status != 'Completed') ...[
                  ElevatedButton(onPressed: _busy ? null : _start, child: const Text('بدء المعالجة')),
                  const SizedBox(height: AppSpacing.sm),
                  if (row.canConvert)
                    ElevatedButton(
                      onPressed: _busy
                          ? null
                          : () => Navigator.pushNamed(context, '/sale', arguments: row),
                      child: const Text('إنشاء عملية بيع'),
                    ),
                  TextButton(onPressed: _busy ? null : _reject, child: const Text('رفض الطلب')),
                ],
              ],
            ),
    );
  }
}

class _StatusBadge extends StatelessWidget {
  const _StatusBadge({required this.rejected});
  final bool rejected;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
      decoration: BoxDecoration(
        color: rejected ? AppColors.warningSoft : AppColors.lightGreen.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(AppRadius.sm),
      ),
      child: Text(rejected ? 'مرفوض' : 'معلق',
          style: TextStyle(
            color: rejected ? AppColors.danger : AppColors.darkGreen,
            fontWeight: FontWeight.w700,
            fontSize: 12,
          )),
    );
  }
}
