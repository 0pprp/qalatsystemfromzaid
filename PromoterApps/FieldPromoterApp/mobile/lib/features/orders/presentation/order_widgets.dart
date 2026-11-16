import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../../core/theme/app_theme.dart';
import '../../../core/utils/formatters.dart';
import '../../../core/widgets/common.dart';
import '../data/order_models.dart';

class OrderStatusChip extends StatelessWidget {
  const OrderStatusChip({super.key, required this.status, this.label});

  final OrderStatus status;
  final String? label;

  @override
  Widget build(BuildContext context) =>
      StatusChip(label: label ?? status.label, color: status.color, icon: status.icon);
}

/// A sale order in a list: what was sold, the customer, status and (once sold) payment progress.
class OrderCard extends StatelessWidget {
  const OrderCard({super.key, required this.order});

  final OrderSummary order;

  @override
  Widget build(BuildContext context) {
    return AppCard(
      onTap: () => context.push('/orders/${order.id}'),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(
                child: Text(order.itemsSummary,
                    maxLines: 2, overflow: TextOverflow.ellipsis, style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 15)),
              ),
              const SizedBox(width: 8),
              OrderStatusChip(status: order.status, label: order.statusLabel),
            ],
          ),
          const SizedBox(height: 8),
          Wrap(
            spacing: 14,
            runSpacing: 4,
            children: [
              _Meta(icon: Icons.person_outline_rounded, text: order.customerName),
              _Meta(icon: order.purchaseType.icon, text: order.purchaseType.label),
              _Meta(icon: Icons.schedule_rounded, text: Fmt.relative(order.createdAt)),
            ],
          ),
          if (order.sale != null) ...[
            const SizedBox(height: 12),
            SaleProgressBar(sale: order.sale!, finished: order.status == OrderStatus.finished),
          ],
        ],
      ),
    );
  }
}

/// Paid / remaining of a sold order, until it is fully paid.
class SaleProgressBar extends StatelessWidget {
  const SaleProgressBar({super.key, required this.sale, required this.finished});

  final SaleProgress sale;
  final bool finished;

  @override
  Widget build(BuildContext context) {
    final color = finished ? OrderStatus.finished.color : AppColors.success;
    if (!finished && sale.total <= 0) {
      return const Row(
        children: [
          Icon(Icons.hourglass_top_rounded, size: 16, color: AppColors.textMuted),
          SizedBox(width: 6),
          Text('بانتظار بيانات التسديد من المنظومة', style: TextStyle(fontSize: 12.5, color: AppColors.textMuted)),
        ],
      );
    }
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Row(
          children: [
            Text(finished ? 'مسددة بالكامل' : 'نسبة التسديد', style: const TextStyle(fontSize: 12.5, color: AppColors.textMuted)),
            const Spacer(),
            Text('${sale.progressPercent}%', style: TextStyle(fontWeight: FontWeight.w800, color: color)),
          ],
        ),
        const SizedBox(height: 4),
        ClipRRect(
          borderRadius: BorderRadius.circular(6),
          child: LinearProgressIndicator(
            value: sale.progressPercent / 100,
            minHeight: 8,
            backgroundColor: AppColors.border,
            valueColor: AlwaysStoppedAnimation(color),
          ),
        ),
        const SizedBox(height: 4),
        Row(
          children: [
            Flexible(
              child: Text('المدفوع ${Fmt.money(sale.paid)}',
                  overflow: TextOverflow.ellipsis, style: const TextStyle(fontSize: 12, color: AppColors.textMuted)),
            ),
            const SizedBox(width: 8),
            const Spacer(),
            if (!finished)
              Flexible(
                child: Text('المتبقي ${Fmt.money(sale.remaining)}',
                    overflow: TextOverflow.ellipsis, style: const TextStyle(fontSize: 12, color: AppColors.textMuted)),
              ),
          ],
        ),
      ],
    );
  }
}

/// Detail-page panel for a sold order: progress, totals, payments and dates.
class SalePanel extends StatelessWidget {
  const SalePanel({super.key, required this.sale, required this.finished});

  final SaleProgress sale;
  final bool finished;

  @override
  Widget build(BuildContext context) {
    return AppCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            children: [
              Icon(finished ? Icons.task_alt_rounded : Icons.payments_rounded, color: finished ? OrderStatus.finished.color : AppColors.success),
              const SizedBox(width: 8),
              Text(finished ? 'المبيعة مسددة بالكامل' : 'متابعة تسديد المبيعة', style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 16)),
            ],
          ),
          const SizedBox(height: 12),
          SaleProgressBar(sale: sale, finished: finished),
          const Divider(height: 24),
          if (sale.total > 0) ...[
            InfoRow(label: 'قيمة المبيعة', value: Fmt.money(sale.total)),
            InfoRow(label: 'المدفوع', value: Fmt.money(sale.paid)),
            if (!finished) InfoRow(label: 'المتبقي', value: Fmt.money(sale.remaining)),
          ],
          if (sale.paymentsCount > 0) InfoRow(label: 'عدد الدفعات', value: Fmt.number(sale.paymentsCount)),
          if (sale.lastPaymentAt != null) InfoRow(label: 'آخر دفعة', value: Fmt.date(sale.lastPaymentAt)),
          if (sale.soldAt != null) InfoRow(label: 'تاريخ البيع', value: Fmt.date(sale.soldAt)),
          if (sale.finishedAt != null) InfoRow(label: 'اكتمل التسديد', value: Fmt.date(sale.finishedAt)),
        ],
      ),
    );
  }
}

class _Meta extends StatelessWidget {
  const _Meta({required this.icon, required this.text});

  final IconData icon;
  final String text;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Icon(icon, size: 16, color: AppColors.textMuted),
        const SizedBox(width: 4),
        Text(text, style: const TextStyle(color: AppColors.textMuted, fontSize: 12.5)),
      ],
    );
  }
}

/// Vertical stepper: طلب جديد → في قسم المبيعات → قيد الكشف الميداني → تم البيع → مكتمل التسديد.
class OrderTimeline extends StatelessWidget {
  const OrderTimeline({super.key, required this.status, required this.timeline});

  final OrderStatus status;
  final List<TimelineEntry> timeline;

  @override
  Widget build(BuildContext context) {
    final steps = buildTimelineSteps(status, timeline);
    return Column(
      children: [
        for (var i = 0; i < steps.length; i++)
          IntrinsicHeight(
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                SizedBox(
                  width: 34,
                  child: Column(
                    children: [
                      Container(
                        width: 28,
                        height: 28,
                        decoration: BoxDecoration(
                          shape: BoxShape.circle,
                          color: steps[i].reached ? steps[i].status.color : Colors.white,
                          border: Border.all(color: steps[i].reached ? steps[i].status.color : AppColors.border, width: 2),
                        ),
                        child: Icon(
                          steps[i].reached ? (steps[i].current ? steps[i].status.icon : Icons.check_rounded) : Icons.circle,
                          size: steps[i].reached ? 16 : 6,
                          color: steps[i].reached ? Colors.white : AppColors.border,
                        ),
                      ),
                      if (i < steps.length - 1)
                        Expanded(
                          child: Container(
                            width: 2,
                            margin: const EdgeInsets.symmetric(vertical: 2),
                            color: steps[i + 1].reached ? AppColors.primary : AppColors.border,
                          ),
                        ),
                    ],
                  ),
                ),
                const SizedBox(width: 10),
                Expanded(
                  child: Padding(
                    padding: const EdgeInsets.only(bottom: 18, top: 3),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          steps[i].label,
                          style: TextStyle(
                            fontWeight: steps[i].current ? FontWeight.w800 : FontWeight.w600,
                            color: steps[i].reached ? AppColors.text : AppColors.textMuted,
                          ),
                        ),
                        if (steps[i].at != null)
                          Text(Fmt.dateTime(steps[i].at), style: const TextStyle(color: AppColors.textMuted, fontSize: 12)),
                        if (steps[i].note != null)
                          Text(steps[i].note!, style: const TextStyle(fontSize: 12.5, height: 1.5)),
                      ],
                    ),
                  ),
                ),
              ],
            ),
          ),
      ],
    );
  }
}
