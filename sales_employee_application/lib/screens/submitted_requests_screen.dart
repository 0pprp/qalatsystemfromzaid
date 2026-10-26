import 'package:sales_employee_application/widgets/company_loader.dart';
import 'package:flutter/material.dart';
import 'package:sales_employee_application/data/sales_models.dart';
import 'package:sales_employee_application/data/sales_repository_factory.dart';
import 'package:sales_employee_application/services/api_client.dart';
import 'package:sales_employee_application/utils/app_theme.dart';
import 'package:sales_employee_application/utils/sales_format.dart';
import 'package:sales_employee_application/widgets/tappable_phone.dart';

/// History of sales requests this employee submitted (not inbox assignments).
class SubmittedRequestsScreen extends StatefulWidget {
  const SubmittedRequestsScreen({super.key});

  @override
  State<SubmittedRequestsScreen> createState() => _SubmittedRequestsScreenState();
}

enum _SubmittedFilter { all, sold, rejected, inProgress }

class _SubmittedRequestsScreenState extends State<SubmittedRequestsScreen> {
  List<SalesWorkRequest> _rows = [];
  bool _loading = true;
  String? _error;
  _SubmittedFilter _filter = _SubmittedFilter.all;

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
      final rows = await SalesRepositoryFactory.instance.submittedSalesRequests();
      if (!mounted) return;
      setState(() {
        _rows = rows;
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
        _error = 'تعذر تحميل سجل طلبات المبيع';
      });
    }
  }

  bool _isSold(SalesWorkRequest r) =>
      r.status == 'Completed' || r.isSold;

  bool _isRejected(SalesWorkRequest r) => r.status == 'Rejected';

  bool _isInProgress(SalesWorkRequest r) => !_isSold(r) && !_isRejected(r);

  List<SalesWorkRequest> get _filtered {
    switch (_filter) {
      case _SubmittedFilter.all:
        return _rows;
      case _SubmittedFilter.sold:
        return _rows.where(_isSold).toList();
      case _SubmittedFilter.rejected:
        return _rows.where(_isRejected).toList();
      case _SubmittedFilter.inProgress:
        return _rows.where(_isInProgress).toList();
    }
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        SingleChildScrollView(
          scrollDirection: Axis.horizontal,
          padding: const EdgeInsets.fromLTRB(AppSpacing.md, AppSpacing.md, AppSpacing.md, AppSpacing.sm),
          child: Row(
            children: [
              _chip('الكل', _SubmittedFilter.all),
              _chip('تم البيع', _SubmittedFilter.sold),
              _chip('مرفوض', _SubmittedFilter.rejected),
              _chip('قيد العمل', _SubmittedFilter.inProgress),
            ],
          ),
        ),
        Expanded(child: _body()),
      ],
    );
  }

  Widget _chip(String label, _SubmittedFilter value) {
    final selected = _filter == value;
    return Padding(
      padding: const EdgeInsetsDirectional.only(end: 8),
      child: FilterChip(
        label: Text(label),
        selected: selected,
        onSelected: (_) => setState(() => _filter = value),
        selectedColor: AppColors.darkGreen.withValues(alpha: 0.18),
        checkmarkColor: AppColors.darkGreen,
        labelStyle: TextStyle(
          fontWeight: selected ? FontWeight.w700 : FontWeight.w500,
          color: selected ? AppColors.darkGreen : AppColors.text,
        ),
      ),
    );
  }

  Widget _body() {
    if (_loading) return const Center(child: CompanyLoader(label: 'جارٍ تحميل البيانات'));
    if (_error != null) {
      return Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(_error!, style: const TextStyle(color: AppColors.danger)),
            const SizedBox(height: 12),
            TextButton(onPressed: _load, child: const Text('إعادة المحاولة')),
          ],
        ),
      );
    }
    final rows = _filtered;
    if (rows.isEmpty) {
      return RefreshIndicator(
        onRefresh: _load,
        child: ListView(
          children: const [
            SizedBox(height: 120),
            Center(child: Text('لا توجد طلبات في هذا التبويب', style: TextStyle(color: AppColors.muted))),
          ],
        ),
      );
    }
    return RefreshIndicator(
      onRefresh: _load,
      child: ListView.separated(
        padding: const EdgeInsets.all(AppSpacing.md),
        itemCount: rows.length,
        separatorBuilder: (_, _) => const SizedBox(height: AppSpacing.sm),
        itemBuilder: (context, i) => _card(rows[i]),
      ),
    );
  }

  Widget _card(SalesWorkRequest r) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.md),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text(r.customerName, style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 16)),
            if ((r.customerProvince ?? '').trim().isNotEmpty)
              Text(r.customerProvince!, style: const TextStyle(color: AppColors.muted)),
            if ((r.customerPhone ?? '').trim().isNotEmpty) ...[
              const SizedBox(height: 4),
              TappablePhone(r.customerPhone),
            ],
            const SizedBox(height: 6),
            Align(
              alignment: Alignment.centerRight,
              child: Text(
                SalesRequestStatusLabels.of(r.status),
                style: const TextStyle(
                  fontSize: 12,
                  color: AppColors.darkGreen,
                  fontWeight: FontWeight.w700,
                ),
              ),
            ),
            if (r.status == 'Rejected' && (r.rejectionReason ?? '').trim().isNotEmpty) ...[
              const SizedBox(height: 6),
              Text(
                'سبب الرفض: ${r.rejectionReason}',
                style: const TextStyle(color: AppColors.danger, fontSize: 13),
              ),
            ],
          ],
        ),
      ),
    );
  }
}
