import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/app_state.dart';
import '../../core/theme/app_theme.dart';
import '../../core/utils/formatters.dart';
import '../../core/utils/launcher.dart';
import '../../core/widgets/common.dart';
import '../../core/widgets/state_views.dart';
import '../../data/models.dart';
import '../../data/repository.dart';

final _orderProvider = FutureProvider.autoDispose.family<OrderDetail, int>((ref, id) {
  ref.watch(dataVersionProvider);
  return ref.watch(repositoryProvider).order(id);
});

class OrderDetailScreen extends ConsumerWidget {
  const OrderDetailScreen({super.key, required this.orderId});

  final int orderId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final value = ref.watch(_orderProvider(orderId));
    return Scaffold(
      appBar: AppBar(title: Text(value.value?.summary.orderNumber ?? 'الطلب')),
      body: AsyncView<OrderDetail>(
        value: value,
        onRetry: () => ref.invalidate(_orderProvider(orderId)),
        data: (o) {
          final s = o.summary;
          final color = OrderStatuses.color(s.status);
          return RefreshIndicator(
            onRefresh: () => ref.refresh(_orderProvider(orderId).future),
            child: ListView(
              padding: const EdgeInsets.all(16),
              children: [
                AppCard(
                  color: color.withValues(alpha: 0.06),
                  child: Row(
                    children: [
                      Icon(OrderStatuses.icon(s.status), color: color, size: 36),
                      const SizedBox(width: 12),
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(s.statusLabel, style: TextStyle(fontWeight: FontWeight.w800, fontSize: 18, color: color)),
                            const SizedBox(height: 2),
                            Text(
                              switch (s.status) {
                                'Sold' => 'أضيفت ${Fmt.money(s.myCommission)} إلى محفظتك',
                                'NotSold' || 'Cancelled' => 'لا توجد عمولة لهذا الطلب',
                                _ => 'عمولتك ${Fmt.money(s.myCommission)} تضاف عند إتمام البيع',
                              },
                              style: const TextStyle(color: AppColors.textMuted),
                            ),
                          ],
                        ),
                      ),
                    ],
                  ),
                ),
                if (o.publicReason != null) ...[
                  const SizedBox(height: 12),
                  NoticeBanner(title: 'السبب', message: o.publicReason!, color: AppColors.danger),
                ],
                const SizedBox(height: 12),
                AppCard(
                  child: Column(
                    children: [
                      InfoRow(label: 'المنتج', value: s.quantity > 1 ? '${s.productName} ×${s.quantity}' : s.productName),
                      InfoRow(label: 'السعر', value: Fmt.money(o.totalPrice)),
                      InfoRow(label: 'عمولتي', value: Fmt.money(s.myCommission), valueStyle: const TextStyle(fontWeight: FontWeight.w800, color: AppColors.success)),
                      const Divider(height: 20),
                      InfoRow(label: 'الزبون', value: s.customerName),
                      Row(
                        children: [
                          Expanded(child: InfoRow(label: 'الهاتف', value: o.customerPhone)),
                          IconButton(onPressed: () => Launch.call(context, o.customerPhone), icon: const Icon(Icons.call_rounded, color: AppColors.primary)),
                        ],
                      ),
                      InfoRow(label: 'العنوان', value: '${s.cityName} — ${o.address}'),
                      if (o.notes != null) InfoRow(label: 'ملاحظات', value: o.notes!),
                      InfoRow(label: 'تاريخ الطلب', value: Fmt.dateTime(s.createdAt)),
                    ],
                  ),
                ),
                const SizedBox(height: 16),
                const Text('مراحل الطلب', style: TextStyle(fontWeight: FontWeight.w800, fontSize: 16)),
                const SizedBox(height: 8),
                for (final (i, t) in o.timeline.indexed) _TimelineRow(item: t, last: i == o.timeline.length - 1),
                if (o.canCancel) ...[
                  const SizedBox(height: 16),
                  OutlinedButton.icon(
                    style: OutlinedButton.styleFrom(foregroundColor: AppColors.danger, side: const BorderSide(color: AppColors.danger)),
                    onPressed: () async {
                      if (!await confirmDialog(context, title: 'إلغاء الطلب', message: 'هل تريد إلغاء هذا الطلب؟', destructive: true, confirm: 'إلغاء الطلب')) return;
                      try {
                        await ref.read(repositoryProvider).cancelOrder(orderId);
                        ref.read(dataVersionProvider.notifier).bump();
                      } catch (e) {
                        if (context.mounted) context.showError(e);
                      }
                    },
                    icon: const Icon(Icons.close_rounded),
                    label: const Text('إلغاء الطلب'),
                  ),
                ],
              ],
            ),
          );
        },
      ),
    );
  }
}

class _TimelineRow extends StatelessWidget {
  const _TimelineRow({required this.item, required this.last});

  final TimelineItem item;
  final bool last;

  @override
  Widget build(BuildContext context) {
    final color = OrderStatuses.color(item.status);
    return IntrinsicHeight(
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          SizedBox(
            width: 24,
            child: Column(
              children: [
                Container(width: 14, height: 14, margin: const EdgeInsets.only(top: 4), decoration: BoxDecoration(color: color, shape: BoxShape.circle)),
                if (!last) Expanded(child: Container(width: 2, color: AppColors.border)),
              ],
            ),
          ),
          const SizedBox(width: 10),
          Expanded(
            child: Padding(
              padding: const EdgeInsets.only(bottom: 16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(item.label, style: const TextStyle(fontWeight: FontWeight.w700)),
                  Text(Fmt.dateTime(item.at), style: const TextStyle(color: AppColors.textMuted, fontSize: 12)),
                  if (item.note != null) Text(item.note!, style: const TextStyle(fontSize: 13)),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }
}
