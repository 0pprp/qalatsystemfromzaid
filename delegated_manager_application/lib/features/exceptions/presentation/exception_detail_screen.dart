import 'package:delegated_manager_application/core/network/api_client.dart';
import 'package:delegated_manager_application/core/theme/app_theme.dart';
import 'package:delegated_manager_application/core/utils/dm_format.dart';
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
          review: _detail?.review,
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
    final review = detail.review;
    final pending = request.status == ExceptionStatuses.pending;
    final classification = review?.customerClassification;

    return ListView(
      padding: const EdgeInsets.all(AppSpacing.md),
      children: [
        // 1. Classification
        if (classification != null) ...[
          _ClassificationBanner(classification: classification),
          const SizedBox(height: AppSpacing.md),
        ],

        // 2. Current request details
        SectionCard(
          title: 'تفاصيل الطلب الحالي',
          children: [
            InfoRow(
              label: 'اسم الزبون',
              value: review?.currentRequest?.customerName ??
                  request.summary.customerName,
            ),
            InfoRow(
              label: 'الهاتف',
              value: review?.currentRequest?.customerPhone ??
                  request.customerPhone ??
                  '-',
            ),
            InfoRow(
              label: 'المحافظة',
              value: DmFormat.friendlyCity(
                review?.currentRequest?.cityName ??
                    review?.currentRequest?.customerProvince ??
                    request.summary.cityName,
                request.summary.cityValue,
              ),
            ),
            InfoRow(
              label: 'العنوان',
              value: review?.currentRequest?.customerAddress ?? '-',
            ),
            InfoRow(
              label: 'نوع المبيع / المنتج',
              value: review?.currentRequest?.saleTypeOrProduct ?? '-',
            ),
            InfoRow(
              label: 'ملاحظات الطلب',
              value: review?.currentRequest?.notes ?? '-',
            ),
            InfoRow(
              label: 'تاريخ إنشاء الطلب',
              value: DmFormat.dateOnly(
                  review?.currentRequest?.createdAtUtc ??
                      request.summary.requestedAtUtc),
            ),
            InfoRow(
              label: 'حالة الطلب',
              value: review?.currentRequest?.status ?? request.status,
            ),
          ],
        ),
        const SizedBox(height: AppSpacing.md),

        // 3. Source
        SectionCard(
          title: 'مصدر الطلب',
          children: [
            InfoRow(
              label: 'المصدر',
              value: review?.source.displayLabel ?? 'غير متوفر',
            ),
            InfoRow(
              label: 'الاسم',
              value: review?.source.personName ?? 'غير متوفر',
            ),
            if ((review?.source.listName ?? '').isNotEmpty)
              InfoRow(label: 'القائمة', value: review!.source.listName!),
            InfoRow(
              label: 'المحافظة',
              value: DmFormat.friendlyCity(
                review?.source.branchName ?? request.summary.cityName,
                request.summary.cityValue,
              ),
            ),
            InfoRow(
              label: 'مقدّم طلب الاستثناء',
              value: request.summary.requestingManagerDisplayName ?? '-',
            ),
          ],
        ),
        const SizedBox(height: AppSpacing.md),

        // 4. Exception reason
        SectionCard(
          title: 'سبب الاستثناء',
          children: [
            Text(
              request.reason ?? '-',
              style: const TextStyle(
                  fontSize: 14, height: 1.6, color: AppColors.text),
            ),
            const SizedBox(height: AppSpacing.sm),
            InfoRow(
              label: 'تاريخ الطلب',
              value: DmFormat.dateOnly(request.summary.requestedAtUtc),
            ),
            StatusChip(
              label: ExceptionStatuses.arabic(request.status),
              color: ExceptionStatuses.color(request.status),
            ),
          ],
        ),
        const SizedBox(height: AppSpacing.md),

        // 5–7. Matches / financial / rating
        if (classification?.isExisting == true &&
            (review?.matchingCustomers.isNotEmpty ?? false)) ...[
          Text(
            classification!.explanationArabic,
            style: const TextStyle(
              fontSize: 14,
              fontWeight: FontWeight.w600,
              color: AppColors.text,
            ),
          ),
          const SizedBox(height: AppSpacing.sm),
          ...review!.matchingCustomers.map(_MatchCard.new),
          const SizedBox(height: AppSpacing.md),
        ] else if (classification?.isNew == true) ...[
          SectionCard(
            title: 'الحالات المطابقة',
            children: [
              Text(
                classification!.explanationArabic,
                style: const TextStyle(
                    fontSize: 14, height: 1.5, color: AppColors.muted),
              ),
            ],
          ),
          const SizedBox(height: AppSpacing.md),
        ],

        if (!pending) ...[
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
            ],
          ),
          const SizedBox(height: AppSpacing.md),
        ],

        // 8. Approve / reject
        if (pending) ...[
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
          const SizedBox(height: AppSpacing.md),
          Row(
            children: [
              Expanded(
                child: FilledButton(
                  style: FilledButton.styleFrom(
                    backgroundColor: AppColors.approved,
                    minimumSize: const Size.fromHeight(48),
                  ),
                  onPressed: _submitting
                      ? null
                      : () => _decide(ExceptionDecision.approved),
                  child: const Text('موافقة'),
                ),
              ),
              const SizedBox(width: AppSpacing.sm),
              Expanded(
                child: FilledButton(
                  style: FilledButton.styleFrom(
                    backgroundColor: AppColors.rejected,
                    minimumSize: const Size.fromHeight(48),
                  ),
                  onPressed: _submitting
                      ? null
                      : () => _decide(ExceptionDecision.rejected),
                  child: const Text('رفض'),
                ),
              ),
            ],
          ),
        ],
      ],
    );
  }
}

class _ClassificationBanner extends StatelessWidget {
  const _ClassificationBanner({required this.classification});

  final ExceptionReviewClassification classification;

  @override
  Widget build(BuildContext context) {
    final existing = classification.isExisting;
    final color = existing ? AppColors.accent : AppColors.approved;
    return Card(
          color: color.withValues(alpha: 0.12),
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.md),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text(
              classification.labelArabic,
              textAlign: TextAlign.center,
              style: TextStyle(
                fontSize: 22,
                fontWeight: FontWeight.bold,
                color: color,
              ),
            ),
            const SizedBox(height: 6),
            Text(
              classification.explanationArabic,
              textAlign: TextAlign.center,
              style: const TextStyle(
                fontSize: 13,
                height: 1.4,
                color: AppColors.text,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _MatchCard extends StatelessWidget {
  const _MatchCard(this.match);

  final ExceptionReviewMatchCustomer match;

  @override
  Widget build(BuildContext context) {
    final fin = match.financialSummary;
    final city = DmFormat.friendlyCity(match.cityName ?? match.province);
    return Padding(
      padding: const EdgeInsets.only(bottom: AppSpacing.sm),
      child: Card(
        child: ExpansionTile(
          tilePadding: const EdgeInsets.symmetric(horizontal: AppSpacing.md),
          childrenPadding: const EdgeInsets.fromLTRB(
              AppSpacing.md, 0, AppSpacing.md, AppSpacing.md),
          title: Text(
            match.fullName,
            style: const TextStyle(
                fontWeight: FontWeight.bold, color: AppColors.text),
          ),
          subtitle: Text(
            [
              match.matchReasons.join(' • '),
              if (city != 'غير متوفر') city,
            ].where((e) => e.isNotEmpty).join(' — '),
            style: const TextStyle(fontSize: 12, color: AppColors.muted),
          ),
          children: [
            const Align(
              alignment: Alignment.centerRight,
              child: Text('بيانات الزبون',
                  style: TextStyle(fontWeight: FontWeight.bold)),
            ),
            InfoRow(label: 'الهاتف', value: match.phone ?? '-'),
            InfoRow(label: 'المحافظة', value: city),
            InfoRow(label: 'العنوان', value: match.address ?? '-'),
            if ((match.occupation ?? '').isNotEmpty)
              InfoRow(label: 'المهنة', value: match.occupation!),
            const SizedBox(height: AppSpacing.sm),
            const Align(
              alignment: Alignment.centerRight,
              child: Text('التقييم',
                  style: TextStyle(fontWeight: FontWeight.bold)),
            ),
            InfoRow(label: 'التقييم', value: match.ratingLabel ?? '-'),
            if (match.isLegal)
              const InfoRow(label: 'الحالة القانونية', value: 'قانونية'),
            const SizedBox(height: AppSpacing.sm),
            const Align(
              alignment: Alignment.centerRight,
              child: Text('السجل المالي (إجمالي)',
                  style: TextStyle(fontWeight: FontWeight.bold)),
            ),
            InfoRow(label: 'إجمالي المبيعات', value: DmFormat.money(fin.totalSales)),
            InfoRow(label: 'المدفوع', value: DmFormat.money(fin.totalPaid)),
            InfoRow(label: 'المتبقي', value: DmFormat.money(fin.remaining)),
            InfoRow(
              label: 'حساب مسدد',
              value: fin.fullyPaid ? 'نعم' : 'لا',
            ),
            InfoRow(
              label: 'آخر بيع',
              value: DmFormat.dateOnly(fin.lastSaleDate),
            ),
            InfoRow(
              label: 'آخر دفعة',
              value: DmFormat.dateOnly(fin.lastPaymentDate),
            ),
            if (match.previousSales.isNotEmpty) ...[
              const SizedBox(height: AppSpacing.sm),
              const Align(
                alignment: Alignment.centerRight,
                child: Text('الحسابات السابقة',
                    style: TextStyle(fontWeight: FontWeight.bold)),
              ),
              ...match.previousSales.map(
                (s) => _SaleAccountCard(s, ratingLabel: match.ratingLabel),
              ),
            ],
          ],
        ),
      ),
    );
  }
}

class _SaleAccountCard extends StatelessWidget {
  const _SaleAccountCard(this.sale, {this.ratingLabel});

  final ExceptionReviewPreviousSale sale;
  final String? ratingLabel;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      margin: const EdgeInsets.only(top: AppSpacing.sm),
      padding: const EdgeInsets.all(AppSpacing.sm),
      decoration: BoxDecoration(
        border: Border.all(color: AppColors.muted.withValues(alpha: 0.35)),
        borderRadius: BorderRadius.circular(8),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text('الحساب السابق',
              style: TextStyle(fontWeight: FontWeight.w700)),
          const SizedBox(height: 4),
          InfoRow(label: 'تاريخ المبيع', value: DmFormat.dateOnly(sale.saleDate)),
          InfoRow(
            label: 'نوع المبيع',
            value: (sale.productOrType ?? '').isEmpty
                ? 'غير متوفر'
                : sale.productOrType!,
          ),
          InfoRow(label: 'المبلغ الكلي', value: DmFormat.money(sale.saleAmount)),
          InfoRow(label: 'المبلغ المسدد', value: DmFormat.money(sale.paidAmount)),
          InfoRow(
            label: 'المتبقي',
            value: DmFormat.money(sale.remainingAmount ?? 0),
          ),
          InfoRow(
            label: 'عدد التسديدات',
            value: sale.paymentCount == null
                ? 'غير متوفر'
                : '${sale.paymentCount}',
          ),
          InfoRow(
            label: 'عدد أيام التسديد',
            value: sale.repaymentDays == null
                ? 'غير متوفر'
                : '${sale.repaymentDays} يوم',
          ),
          InfoRow(
            label: 'آخر تسديد',
            value: DmFormat.dateOnly(sale.lastPaymentDate),
          ),
          InfoRow(
            label: 'حالة الحساب',
            value: sale.accountStatusArabic ??
                (sale.accountZero == true ? 'مصفر' : 'مفتوح'),
          ),
          InfoRow(
            label: 'التقييم',
            value: (ratingLabel == null || ratingLabel!.trim().isEmpty)
                ? 'غير متوفر'
                : ratingLabel!,
          ),
        ],
      ),
    );
  }
}
