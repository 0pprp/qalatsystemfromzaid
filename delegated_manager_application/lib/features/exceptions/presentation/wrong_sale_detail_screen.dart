import 'package:delegated_manager_application/core/network/api_client.dart';
import 'package:delegated_manager_application/core/theme/app_theme.dart';
import 'package:delegated_manager_application/core/utils/date_format.dart';
import 'package:delegated_manager_application/core/utils/dm_format.dart';
import 'package:delegated_manager_application/core/widgets/state_views.dart';
import 'package:delegated_manager_application/features/exceptions/data/exceptions_repository.dart';
import 'package:delegated_manager_application/features/exceptions/domain/sales_exception.dart';
import 'package:flutter/material.dart';

/// WrongSale = notice like a complaint (read/unread), not approve/reject.
class WrongSaleDetailScreen extends StatefulWidget {
  const WrongSaleDetailScreen({super.key, required this.requestId});

  final String requestId;

  @override
  State<WrongSaleDetailScreen> createState() => _WrongSaleDetailScreenState();
}

class _WrongSaleDetailScreenState extends State<WrongSaleDetailScreen> {
  ExceptionDetail? _detail;
  bool _loading = true;
  String? _error;

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
      final detail = await ExceptionsRepository.detail(widget.requestId);
      if (!mounted) return;
      setState(() {
        _detail = detail;
        _loading = false;
      });
      if (detail.request.isUnread || detail.request.summary.readAtUtc == null) {
        try {
          final updated = await ExceptionsRepository.markRead(widget.requestId);
          if (!mounted) return;
          setState(() {
            _detail = ExceptionDetail(
              request: updated,
              audit: detail.audit,
              review: detail.review,
              customerProfile: detail.customerProfile,
              reviewError: detail.reviewError,
            );
          });
        } catch (_) {}
      }
    } on ApiException catch (e) {
      if (!mounted) return;
      setState(() {
        _error = e.message;
        _loading = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final detail = _detail;
    return Scaffold(
      appBar: AppBar(
        title: const Text('مبيع خاطئ'),
        leading: IconButton(
          icon: const Icon(Icons.arrow_forward),
          onPressed: () => Navigator.of(context).pop(true),
        ),
      ),
      body: SafeArea(
        child: _loading
            ? const LoadingView()
            : detail == null
                ? ErrorView(
                    message: _error ?? 'تعذر تحميل الإشعار', onRetry: _load)
                : RefreshIndicator(
                    onRefresh: _load,
                    child: ListView(
                      padding: const EdgeInsets.all(AppSpacing.md),
                      children: _sections(detail),
                    ),
                  ),
      ),
    );
  }

  List<Widget> _sections(ExceptionDetail detail) {
    final request = detail.request;
    final p = detail.customerProfile;
    final review = detail.review;
    final pay = _asMap(p?['paymentSummary'] ?? p?['PaymentSummary']);
    final collectionDelegateEval =
        _asMap(p?['delegateShopEval'] ?? p?['DelegateShopEval']);
    final followerEval =
        _asMap(p?['followerShopEval'] ?? p?['FollowerShopEval']);
    final sales = (p?['sales'] ?? p?['Sales'] ?? []) as List? ?? [];
    final payments = (p?['payments'] ?? p?['Payments'] ?? []) as List? ?? [];
    final selfId = request.summary.customerId;
    final matches = (review?.matchingCustomers ??
            const <ExceptionReviewMatchCustomer>[])
        .where((m) {
      if (m.isCurrentCustomer) return false;
      if (selfId != null && m.customerId != null && m.customerId == selfId) {
        return false;
      }
      return true;
    }).toList();

    // One eval container: follower OR collection-list delegate (not sales employee).
    final fromFollower = _isWrongSaleFromFollower(
      request: request,
      followerEval: followerEval,
      collectionDelegateEval: collectionDelegateEval,
    );
    final senderEval = fromFollower ? followerEval : collectionDelegateEval;
    final evalTitle =
        fromFollower ? 'تقييم المتابع' : 'تقييم مندوب الجباية';
    final evaluatorLabel =
        fromFollower ? 'اسم المتابع' : 'اسم مندوب الجباية';
    final evaluatorName = _firstNonEmpty([
      '${senderEval?['evaluatorName'] ?? senderEval?['EvaluatorName'] ?? ''}',
      request.summary.requestingManagerDisplayName ?? '',
    ]);
    final listName = _firstNonEmpty([
      '${p?['customerListName'] ?? p?['CustomerListName'] ?? ''}',
      '${p?['delegateName'] ?? p?['DelegateName'] ?? ''}',
    ]);

    final widgets = <Widget>[
      Card(
        color: Colors.orange.shade50,
        child: Padding(
          padding: const EdgeInsets.all(AppSpacing.md),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Row(
                children: [
                  Icon(Icons.report_gmailerrorred,
                      color: Colors.orange.shade800),
                  const SizedBox(width: 8),
                  const Expanded(
                    child: Text(
                      'مبيع خاطئ — إشعار اطلاع',
                      style: TextStyle(
                        fontSize: 17,
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                  ),
                  StatusChip(
                    label: request.isUnread ? 'غير مقروء' : 'مقروء',
                    color: request.isUnread
                        ? AppColors.primary
                        : AppColors.muted,
                  ),
                ],
              ),
              const SizedBox(height: 8),
              Text(
                'هذا إشعار فقط (مثل الرسالة) — لا موافقة ولا رفض. يبقى مخزّناً ويُعلَّم مقروءاً عند الفتح.',
                style: TextStyle(
                  color: Colors.orange.shade900,
                  height: 1.5,
                  fontSize: 13,
                ),
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
            label: 'الاسم',
            value: _firstNonEmpty([
              '${p?['customerName'] ?? p?['CustomerName'] ?? ''}',
              request.summary.customerName,
            ]),
          ),
          InfoRow(
            label: 'الهاتف',
            value: _firstNonEmpty([
              '${p?['phone'] ?? p?['Phone'] ?? ''}',
              request.customerPhone ?? '',
            ]),
          ),
          InfoRow(
            label: 'المحافظة',
            value: DmFormat.friendlyCity(
              _firstNonEmpty([
                '${p?['province'] ?? p?['Province'] ?? ''}',
                '${p?['cityName'] ?? p?['CityName'] ?? ''}',
                request.summary.cityName ?? '',
              ]),
              request.summary.cityValue,
            ),
          ),
          InfoRow(
            label: 'العنوان',
            value: '${p?['address'] ?? p?['Address'] ?? '-'}',
          ),
          InfoRow(
            label: 'القائمة',
            value:
                '${p?['customerListName'] ?? p?['CustomerListName'] ?? p?['delegateName'] ?? '-'}',
          ),
        ],
      ),
      const SizedBox(height: AppSpacing.md),
      SectionCard(
        title: 'البيع والقسط',
        children: [
          InfoRow(
            label: 'سعر البيع',
            value: _num(pay?['amountTotalSales'] ?? pay?['AmountTotalSales']),
          ),
          InfoRow(
            label: 'القسط اليومي',
            value: _num(
              pay?['amountDaySales'] ??
                  pay?['AmountDaySales'] ??
                  _firstSaleDaily(sales),
            ),
          ),
          InfoRow(
            label: 'المدفوع',
            value: _num(pay?['receiptsTotal'] ?? pay?['ReceiptsTotal']),
          ),
          InfoRow(
            label: 'المتبقي',
            value: _num(pay?['amountRemaining'] ?? pay?['AmountRemaining']),
          ),
        ],
      ),
      const SizedBox(height: AppSpacing.md),
      // Single container: whoever sent the WrongSale (follower or collection delegate).
      SectionCard(
        title: evalTitle,
        children: [
          InfoRow(label: evaluatorLabel, value: evaluatorName),
          InfoRow(label: 'القائمة', value: listName),
          ..._shopEvalRows(senderEval, emptyHint: 'لم يُقيَّم بعد'),
        ],
      ),
    ];

    if ((request.reason ?? '').trim().isNotEmpty) {
      widgets.add(const SizedBox(height: AppSpacing.md));
      widgets.add(SectionCard(
        title: 'نص الإشعار',
        children: [
          Text(
            request.reason!,
            style: const TextStyle(height: 1.6, fontSize: 14),
          ),
        ],
      ));
    }

    if (payments.isNotEmpty) {
      widgets.add(const SizedBox(height: AppSpacing.md));
      widgets.add(SectionCard(
        title: 'التسديدات (${payments.length})',
        children: payments.take(20).map((raw) {
          final m = _asMap(raw) ?? {};
          return InfoRow(
            label: _shortDate(m['paymentDate'] ?? m['PaymentDate']),
            value:
                '${_num(m['amount'] ?? m['Amount'])} · سند ${m['boundNumber'] ?? m['BoundNumber'] ?? '-'}',
          );
        }).toList(),
      ));
    }

    widgets.add(const SizedBox(height: AppSpacing.md));
    widgets.add(const Text(
      'أسماء سابقة / مطابقة (هاتف أو اسم) في المحافظة',
      style: TextStyle(
        fontWeight: FontWeight.bold,
        fontSize: 15,
        color: AppColors.text,
      ),
    ));
    widgets.add(const SizedBox(height: AppSpacing.sm));
    if (matches.isEmpty) {
      widgets.add(const SectionCard(
        title: 'المطابقات',
        children: [
          Text(
            'لا توجد أسماء سابقة مطابقة غير هذا الزبون في المحافظة.',
            style: TextStyle(color: AppColors.muted, height: 1.5),
          ),
        ],
      ));
    } else {
      for (final m in matches) {
        final fin = m.financialSummary;
        final saleDate = fin.lastSaleDate ??
            (m.previousSales.isNotEmpty
                ? m.previousSales.first.saleDate
                : null);
        widgets.add(Card(
          child: Padding(
            padding: const EdgeInsets.all(AppSpacing.md),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Text(
                  m.fullName,
                  style: const TextStyle(
                      fontWeight: FontWeight.w700, fontSize: 15),
                ),
                if (m.matchReasons.isNotEmpty) ...[
                  const SizedBox(height: 4),
                  Text(
                    m.matchReasons.join(' · '),
                    style:
                        const TextStyle(color: AppColors.muted, fontSize: 12),
                  ),
                ],
                InfoRow(label: 'الهاتف', value: m.phone ?? '-'),
                InfoRow(
                  label: 'تاريخ البيع',
                  value: DmFormat.dateOnly(saleDate),
                ),
                InfoRow(
                  label: 'سعر البيع',
                  value: DmFormat.money(fin.totalSales),
                ),
                InfoRow(
                  label: 'المدفوع',
                  value: DmFormat.money(fin.totalPaid),
                ),
                InfoRow(
                  label: 'المتبقي',
                  value: DmFormat.money(fin.remaining),
                ),
                InfoRow(label: 'التقييم', value: m.ratingLabel ?? '-'),
                if (m.previousSales.isNotEmpty) ...[
                  const SizedBox(height: 8),
                  const Text(
                    'مبيعات سابقة',
                    style: TextStyle(fontWeight: FontWeight.w700),
                  ),
                  ...m.previousSales.take(5).map(
                        (s) => InfoRow(
                          label: DmFormat.dateOnly(s.saleDate),
                          value: DmFormat.money(s.saleAmount),
                        ),
                      ),
                ],
              ],
            ),
          ),
        ));
        widgets.add(const SizedBox(height: AppSpacing.sm));
      }
    }

    if ((detail.reviewError ?? '').isNotEmpty && p == null) {
      widgets.add(const SizedBox(height: AppSpacing.md));
      widgets.add(SectionCard(
        title: 'ملاحظة',
        children: [
          Text(detail.reviewError!,
              style: const TextStyle(color: AppColors.muted)),
        ],
      ));
    }

    return widgets;
  }

  /// True when WrongSale was sent by the follower (not collection-list delegate).
  bool _isWrongSaleFromFollower({
    required ExceptionRequest request,
    required Map<String, dynamic>? followerEval,
    required Map<String, dynamic>? collectionDelegateEval,
  }) {
    final reason = request.reason ?? '';
    if (reason.contains('المصدر: متابع')) return true;
    if (reason.contains('المصدر: مندوب')) return false;

    final requester =
        (request.summary.requestingManagerDisplayName ?? '').trim();
    final fName =
        '${followerEval?['evaluatorName'] ?? followerEval?['EvaluatorName'] ?? ''}'
            .trim();
    final dName =
        '${collectionDelegateEval?['evaluatorName'] ?? collectionDelegateEval?['EvaluatorName'] ?? ''}'
            .trim();

    if (requester.isNotEmpty && fName.isNotEmpty && requester == fName) {
      return true;
    }
    if (requester.isNotEmpty && dName.isNotEmpty && requester == dName) {
      return false;
    }
    if (requester == 'متابع') return true;
    if (requester == 'مندوب' || requester.contains('مندوب الجباية')) {
      return false;
    }

    final fSrc =
        '${followerEval?['source'] ?? followerEval?['Source'] ?? ''}'.trim();
    final dSrc =
        '${collectionDelegateEval?['source'] ?? collectionDelegateEval?['Source'] ?? ''}'
            .trim();
    if (fSrc.toLowerCase() == 'follower' && dSrc.toLowerCase() != 'delegate') {
      return true;
    }
    if (dSrc.toLowerCase() == 'delegate' && followerEval == null) {
      return false;
    }

    if (followerEval != null && collectionDelegateEval == null) return true;
    if (collectionDelegateEval != null && followerEval == null) return false;

    // New-sale WrongSale from collection delegate carries SalesRequestId.
    if (request.summary.salesRequestId != null) return false;

    // Default: follower authentication path.
    return followerEval != null;
  }

  List<Widget> _shopEvalRows(Map<String, dynamic>? e, {String? emptyHint}) {
    if (e == null) {
      return [
        InfoRow(label: 'الحالة', value: emptyHint ?? 'غير متوفر'),
        const InfoRow(label: 'التقييم', value: '-'),
        const InfoRow(label: 'الملاحظة', value: '-'),
        const InfoRow(label: 'قيمة بضاعة المحل', value: '-'),
        const InfoRow(label: 'واردات المحل اليومية', value: '-'),
      ];
    }
    final rating = e['rating'] ?? e['Rating'];
    return [
      InfoRow(
        label: 'التقييم',
        value: rating == null ? '-' : '$rating / 10',
      ),
      InfoRow(
        label: 'الملاحظة',
        value: '${e['note'] ?? e['Note'] ?? '-'}',
      ),
      InfoRow(
        label: 'قيمة بضاعة المحل',
        value: _num(
            e['shopStockEstimatedValue'] ?? e['ShopStockEstimatedValue']),
      ),
      InfoRow(
        label: 'واردات المحل اليومية',
        value: _num(
            e['estimatedDailyRevenue'] ?? e['EstimatedDailyRevenue']),
      ),
      InfoRow(
        label: 'سعر البيع (عند التقييم)',
        value: _num(e['saleAmount'] ?? e['SaleAmount']),
      ),
    ];
  }

  dynamic _firstSaleDaily(List sales) {
    for (final s in sales) {
      final m = _asMap(s);
      final v = m?['dailyInstallment'] ?? m?['DailyInstallment'];
      if (v != null) return v;
    }
    return null;
  }

  Map<String, dynamic>? _asMap(dynamic v) {
    if (v is Map<String, dynamic>) return v;
    if (v is Map) return Map<String, dynamic>.from(v);
    return null;
  }

  String _num(dynamic v) {
    if (v == null) return '-';
    final n = v is num ? v.toDouble() : double.tryParse('$v');
    if (n == null) return '$v';
    return DmFormat.money(n);
  }

  String _shortDate(dynamic v) {
    final d = AppDate.parseUtc(v);
    return d == null ? '-' : DmFormat.dateOnly(d);
  }

  String _firstNonEmpty(List<String> values) {
    for (final v in values) {
      final t = v.trim();
      if (t.isNotEmpty && t != '-') return t;
    }
    return '-';
  }
}
