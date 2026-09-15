import 'package:delegated_manager_application/core/network/api_client.dart';
import 'package:delegated_manager_application/core/theme/app_theme.dart';
import 'package:delegated_manager_application/core/utils/date_format.dart';
import 'package:delegated_manager_application/core/widgets/state_views.dart';
import 'package:delegated_manager_application/features/exceptions/data/exceptions_repository.dart';
import 'package:delegated_manager_application/features/exceptions/domain/sales_exception.dart';
import 'package:flutter/material.dart';

/// Detail + decision screen. Pops `true` when a decision was recorded so the
/// list and dashboard can refresh.
class ExceptionDetailScreen extends StatefulWidget {
  const ExceptionDetailScreen({super.key, required this.requestId});

  final String requestId;

  @override
  State<ExceptionDetailScreen> createState() => _ExceptionDetailScreenState();
}

class _ExceptionDetailScreenState extends State<ExceptionDetailScreen> {
  final _noteController = TextEditingController();

  ExceptionDetail? _detail;
  bool _loading = true;
  bool _submitting = false;
  bool _decided = false;
  String? _error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    _noteController.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final detail = await ExceptionsRepository.detail(widget.requestId);
      if (!mounted) return;
      setState(() {
        _detail = detail;
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

  Future<void> _decide(String decision) async {
    // Double-submit guard: one in-flight decision at a time.
    if (_submitting) return;

    final approving = decision == ExceptionDecision.approved;
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: Text(approving ? 'الموافقة على الاستثناء' : 'رفض الاستثناء'),
        content: Text(approving
            ? 'سيتم تسجيل الموافقة وإشعار مدير المبيعات. هل تريد المتابعة؟'
            : 'سيتم تسجيل الرفض وإشعار مدير المبيعات. هل تريد المتابعة؟'),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(context).pop(false),
            child: const Text('إلغاء'),
          ),
          FilledButton(
            style: FilledButton.styleFrom(
              backgroundColor:
                  approving ? AppColors.approved : AppColors.rejected,
            ),
            onPressed: () => Navigator.of(context).pop(true),
            child: Text(approving ? 'موافقة' : 'رفض'),
          ),
        ],
      ),
    );
    if (confirmed != true) return;

    setState(() => _submitting = true);
    try {
      final updated = await ExceptionsRepository.decide(
        id: widget.requestId,
        decision: decision,
        note: _noteController.text,
      );
      if (!mounted) return;
      setState(() {
        _detail = ExceptionDetail(
          request: updated,
          audit: _detail?.audit ?? const [],
        );
        _decided = true;
      });
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(approving
              ? 'تمت الموافقة على طلب الاستثناء'
              : 'تم رفض طلب الاستثناء'),
        ),
      );
      await _load();
    } on ApiException catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context)
          .showSnackBar(SnackBar(content: Text(e.message)));
      if (e.isConflict) await _load();
    } finally {
      if (mounted) setState(() => _submitting = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final detail = _detail;
    return Scaffold(
      appBar: AppBar(
        title: const Text('تفاصيل طلب الاستثناء'),
        leading: IconButton(
          icon: const Icon(Icons.arrow_forward),
          onPressed: () => Navigator.of(context).pop(_decided),
        ),
      ),
      body: SafeArea(
        child: _loading
            ? const LoadingView()
            : detail == null
                ? ErrorView(
                    message: _error ?? 'تعذر تحميل الطلب', onRetry: _load)
                : _buildDetail(detail),
      ),
    );
  }

  Widget _buildDetail(ExceptionDetail detail) {
    final request = detail.request;
    final pending = request.status == ExceptionStatuses.pending;
    return ListView(
      padding: const EdgeInsets.all(AppSpacing.md),
      children: [
        Card(
          child: Padding(
            padding: const EdgeInsets.all(AppSpacing.md),
            child: Row(
              children: [
                Expanded(
                  child: Text(
                    request.summary.customerName,
                    style: const TextStyle(
                      fontSize: 17,
                      fontWeight: FontWeight.bold,
                      color: AppColors.text,
                    ),
                  ),
                ),
                StatusChip(
                  label: ExceptionStatuses.arabic(request.status),
                  color: ExceptionStatuses.color(request.status),
                ),
              ],
            ),
          ),
        ),
        const SizedBox(height: AppSpacing.md),
        SectionCard(
          title: 'بيانات الزبون',
          children: [
            InfoRow(
                label: 'الاسم', value: request.summary.customerName),
            InfoRow(
              label: 'رقم الزبون',
              value: request.summary.customerId?.toString() ?? '-',
            ),
            InfoRow(label: 'الهاتف', value: request.customerPhone ?? '-'),
            InfoRow(
              label: 'المحافظة',
              value: request.summary.cityName ?? 'غير محددة',
            ),
          ],
        ),
        const SizedBox(height: AppSpacing.md),
        SectionCard(
          title: 'بيانات الطلب',
          children: [
            InfoRow(
              label: 'رقم طلب البيع',
              value: request.summary.salesRequestId?.toString() ?? '-',
            ),
            InfoRow(
              label: 'مقدّم الطلب',
              value: request.summary.requestingManagerDisplayName ?? '-',
            ),
            InfoRow(
              label: 'جهة الموافقة',
              value: request.summary.targetApproverType == 'BranchManager'
                  ? 'مدير الفرع'
                  : 'المدير المفوض',
            ),
            InfoRow(
              label: 'تاريخ الطلب',
              value: AppDate.format(request.summary.requestedAtUtc),
            ),
            InfoRow(
              label: 'تاريخ القرار',
              value: AppDate.format(request.summary.decidedAtUtc),
            ),
            const SizedBox(height: AppSpacing.xs),
            const Text(
              'سبب الاستثناء',
              style: TextStyle(color: AppColors.muted, fontSize: 13),
            ),
            const SizedBox(height: 4),
            Text(
              request.reason ?? '-',
              style: const TextStyle(
                  fontSize: 14, height: 1.6, color: AppColors.text),
            ),
          ],
        ),
        if (!pending) ...[
          const SizedBox(height: AppSpacing.md),
          SectionCard(
            title: 'القرار',
            children: [
              InfoRow(
                label: 'صاحب القرار',
                value: request.decisionMakerDisplayName ?? '-',
              ),
              InfoRow(
                label: 'ملاحظة القرار',
                value: request.decisionNote ?? '-',
              ),
              InfoRow(
                label: 'تمت إضافة ملاحظة للزبون',
                value: request.branchCustomerNotePosted ? 'نعم' : 'لا',
              ),
            ],
          ),
        ],
        if (detail.audit.isNotEmpty) ...[
          const SizedBox(height: AppSpacing.md),
          SectionCard(
            title: 'سجل الإجراءات',
            children: detail.audit
                .map(
                  (entry) => Padding(
                    padding: const EdgeInsets.only(bottom: AppSpacing.xs),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          '${ExceptionStatuses.arabic(entry.previousStatus)}'
                          ' ← ${ExceptionStatuses.arabic(entry.newStatus)}',
                          style: const TextStyle(
                              fontSize: 13,
                              fontWeight: FontWeight.bold,
                              color: AppColors.text),
                        ),
                        Text(
                          '${entry.actorDisplayName ?? '-'} • '
                          '${AppDate.format(entry.createdAtUtc)}',
                          style: const TextStyle(
                              fontSize: 12, color: AppColors.muted),
                        ),
                        if ((entry.decisionNote ?? '').isNotEmpty)
                          Text(
                            entry.decisionNote!,
                            style: const TextStyle(
                                fontSize: 12, color: AppColors.muted),
                          ),
                      ],
                    ),
                  ),
                )
                .toList(),
          ),
        ],
        if (pending) ...[
          const SizedBox(height: AppSpacing.md),
          TextField(
            controller: _noteController,
            maxLines: 3,
            maxLength: 500,
            enabled: !_submitting,
            decoration: const InputDecoration(
              labelText: 'ملاحظة القرار (اختياري)',
              alignLabelWithHint: true,
            ),
          ),
          const SizedBox(height: AppSpacing.xs),
          Row(
            children: [
              Expanded(
                child: FilledButton.icon(
                  style: FilledButton.styleFrom(
                      backgroundColor: AppColors.approved),
                  onPressed: _submitting
                      ? null
                      : () => _decide(ExceptionDecision.approved),
                  icon: const Icon(Icons.check_circle_outline),
                  label: const Text('موافقة'),
                ),
              ),
              const SizedBox(width: AppSpacing.sm),
              Expanded(
                child: FilledButton.icon(
                  style: FilledButton.styleFrom(
                      backgroundColor: AppColors.rejected),
                  onPressed: _submitting
                      ? null
                      : () => _decide(ExceptionDecision.rejected),
                  icon: const Icon(Icons.cancel_outlined),
                  label: const Text('رفض'),
                ),
              ),
            ],
          ),
          if (_submitting)
            const Padding(
              padding: EdgeInsets.only(top: AppSpacing.sm),
              child: Center(child: CircularProgressIndicator()),
            ),
        ],
      ],
    );
  }
}
