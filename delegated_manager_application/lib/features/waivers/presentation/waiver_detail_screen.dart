import 'package:delegated_manager_application/core/network/api_client.dart';
import 'package:delegated_manager_application/core/theme/app_theme.dart';
import 'package:delegated_manager_application/core/utils/dm_format.dart';
import 'package:delegated_manager_application/core/widgets/state_views.dart';
import 'package:delegated_manager_application/features/exceptions/domain/sales_exception.dart';
import 'package:delegated_manager_application/features/waivers/data/waivers_repository.dart';
import 'package:delegated_manager_application/features/waivers/domain/customer_waiver.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

class WaiverDetailScreen extends StatefulWidget {
  const WaiverDetailScreen({super.key, required this.requestId});

  final String requestId;

  @override
  State<WaiverDetailScreen> createState() => _WaiverDetailScreenState();
}

class _WaiverDetailScreenState extends State<WaiverDetailScreen> {
  final _noteController = TextEditingController();
  final _amountController = TextEditingController();

  WaiverDetail? _detail;
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
    _amountController.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final detail = await WaiversRepository.detail(widget.requestId);
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

  double? _parseAmount() {
    final raw = _amountController.text.replaceAll(',', '').trim();
    if (raw.isEmpty) return null;
    return double.tryParse(raw);
  }

  Future<void> _decide(String decision) async {
    if (_submitting) return;
    final approving = decision == WaiverStatuses.approved;
    final amount = approving ? _parseAmount() : null;
    if (approving && (amount == null || amount <= 0)) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('أدخل مبلغ التنازل بشكل صحيح')),
      );
      return;
    }

    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: Text(approving ? 'الموافقة على التنازل' : 'رفض طلب التنازل'),
        content: Text(approving
            ? 'سيتم تسجيل تسديد بمبلغ ${DmFormat.money(amount)} وإشعار المحاسب الرئيسي. هل تريد المتابعة؟'
            : 'سيتم رفض الطلب وإرجاعه لمدير الفرع. هل تريد المتابعة؟'),
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
      final updated = await WaiversRepository.decide(
        id: widget.requestId,
        decision: decision,
        note: _noteController.text,
        approvedAmount: amount,
      );
      if (!mounted) return;
      setState(() {
        _detail = WaiverDetail(
          request: updated,
          review: _detail?.review,
          reviewError: _detail?.reviewError,
        );
        _decided = true;
      });
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(approving
              ? 'تمت الموافقة وتسجيل التسديد'
              : 'تم رفض طلب التنازل'),
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
        title: const Text('تفاصيل طلب التنازل'),
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

  Widget _buildDetail(WaiverDetail detail) {
    final request = detail.request;
    final review = detail.review;
    final pending = request.status == WaiverStatuses.pending;

    return ListView(
      padding: const EdgeInsets.all(AppSpacing.md),
      children: [
        Container(
          width: double.infinity,
          padding: const EdgeInsets.all(AppSpacing.md),
          decoration: BoxDecoration(
            color: const Color(0xFFFFF3CD),
            borderRadius: BorderRadius.circular(12),
            border: Border.all(color: const Color(0xFFFFC107), width: 1.5),
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const Text(
                'ملاحظة مدير الفرع',
                style: TextStyle(
                  fontWeight: FontWeight.bold,
                  color: Color(0xFF856404),
                  fontSize: 13,
                ),
              ),
              const SizedBox(height: 6),
              Text(
                request.note?.trim().isNotEmpty == true
                    ? request.note!
                    : '-',
                style: const TextStyle(
                  fontSize: 15,
                  height: 1.55,
                  fontWeight: FontWeight.w600,
                  color: Color(0xFF664D03),
                ),
              ),
            ],
          ),
        ),
        const SizedBox(height: AppSpacing.md),
        SectionCard(
          title: 'بيانات الطلب',
          children: [
            InfoRow(label: 'الزبون', value: request.summary.customerName),
            InfoRow(label: 'الهاتف', value: request.customerPhone ?? '-'),
            InfoRow(
              label: 'المحافظة',
              value: DmFormat.friendlyCity(
                request.summary.cityName,
                request.summary.cityValue,
              ),
            ),
            InfoRow(
              label: 'مدير الفرع',
              value: request.summary.branchManagerDisplayName ?? '-',
            ),
            InfoRow(
              label: 'الحالة',
              value: WaiverStatuses.arabic(request.status),
            ),
            if (request.summary.approvedAmount != null)
              InfoRow(
                label: 'مبلغ التنازل',
                value: DmFormat.money(request.summary.approvedAmount),
              ),
          ],
        ),
        const SizedBox(height: AppSpacing.md),
        if (detail.reviewError != null && review == null)
          SectionCard(
            title: 'تفاصيل الزبون',
            children: [
              Text(detail.reviewError!,
                  style: const TextStyle(color: AppColors.muted)),
            ],
          )
        else if (review != null) ...[
          if (review.matchingCustomers.isNotEmpty) ...[
            const Text(
              'التسديدات والحسابات والأسماء المرتبطة',
              style: TextStyle(fontWeight: FontWeight.bold, fontSize: 15),
            ),
            const SizedBox(height: AppSpacing.sm),
            ...review.matchingCustomers.map(_MatchCard.new),
          ],
        ],
        if (pending) ...[
          const SizedBox(height: AppSpacing.md),
          TextField(
            controller: _amountController,
            keyboardType: TextInputType.number,
            inputFormatters: [_ThousandsSeparatorInputFormatter()],
            enabled: !_submitting,
            decoration: const InputDecoration(
              labelText: 'مبلغ التنازل (إجباري عند الموافقة)',
              hintText: 'مثال: 1,500,000',
            ),
          ),
          const SizedBox(height: AppSpacing.sm),
          TextField(
            controller: _noteController,
            maxLines: 2,
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
                      : () => _decide(WaiverStatuses.approved),
                  child: const Text('موافق'),
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
                      : () => _decide(WaiverStatuses.rejected),
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

class _MatchCard extends StatelessWidget {
  const _MatchCard(this.match);

  final ExceptionReviewMatchCustomer match;

  @override
  Widget build(BuildContext context) {
    final fin = match.financialSummary;
    return Padding(
      padding: const EdgeInsets.only(bottom: AppSpacing.sm),
      child: Card(
        child: ExpansionTile(
          title: Text(match.fullName,
              style: const TextStyle(fontWeight: FontWeight.bold)),
          subtitle: Text(match.matchReasons.join(' • ')),
          childrenPadding: const EdgeInsets.fromLTRB(
              AppSpacing.md, 0, AppSpacing.md, AppSpacing.md),
          children: [
            InfoRow(label: 'الهاتف', value: match.phone ?? '-'),
            InfoRow(label: 'التقييم', value: match.ratingLabel ?? '-'),
            InfoRow(label: 'إجمالي المبيعات', value: DmFormat.money(fin.totalSales)),
            InfoRow(label: 'المدفوع', value: DmFormat.money(fin.totalPaid)),
            InfoRow(label: 'المتبقي', value: DmFormat.money(fin.remaining)),
            const SizedBox(height: AppSpacing.sm),
            const Align(
              alignment: Alignment.centerRight,
              child: Text('التسديدات',
                  style: TextStyle(fontWeight: FontWeight.bold)),
            ),
            if (match.payments.isEmpty)
              const InfoRow(label: 'التسديدات', value: 'لا توجد')
            else
              ...match.payments.map(
                (p) => Padding(
                  padding: const EdgeInsets.only(bottom: 4),
                  child: Row(
                    children: [
                      Expanded(child: Text(DmFormat.dateOnly(p.paymentDate))),
                      Text(DmFormat.money(p.amount),
                          style: const TextStyle(fontWeight: FontWeight.w700)),
                    ],
                  ),
                ),
              ),
            if (match.previousSales.isNotEmpty) ...[
              const SizedBox(height: AppSpacing.sm),
              const Align(
                alignment: Alignment.centerRight,
                child: Text('الحسابات السابقة',
                    style: TextStyle(fontWeight: FontWeight.bold)),
              ),
              ...match.previousSales.map(
                (s) => Padding(
                  padding: const EdgeInsets.only(top: 6),
                  child: Text(
                    '${DmFormat.dateOnly(s.saleDate)} · ${DmFormat.money(s.saleAmount)} · ${s.accountStatusArabic ?? '-'}',
                    style: const TextStyle(fontSize: 13),
                  ),
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }
}

/// Formats digits with thousands separators while typing (1,234,567).
class _ThousandsSeparatorInputFormatter extends TextInputFormatter {
  @override
  TextEditingValue formatEditUpdate(
    TextEditingValue oldValue,
    TextEditingValue newValue,
  ) {
    final digits = newValue.text.replaceAll(RegExp(r'[^0-9]'), '');
    if (digits.isEmpty) {
      return const TextEditingValue(text: '');
    }
    final buf = StringBuffer();
    for (var i = 0; i < digits.length; i++) {
      final fromEnd = digits.length - i;
      buf.write(digits[i]);
      if (fromEnd > 1 && fromEnd % 3 == 1) buf.write(',');
    }
    final text = buf.toString();
    return TextEditingValue(
      text: text,
      selection: TextSelection.collapsed(offset: text.length),
    );
  }
}
