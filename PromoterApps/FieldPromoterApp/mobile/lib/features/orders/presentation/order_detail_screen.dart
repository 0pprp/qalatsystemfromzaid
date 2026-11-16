import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/theme/app_theme.dart';
import '../../../core/utils/formatters.dart';
import '../../../core/utils/launcher.dart';
import '../../../core/widgets/app_network_image.dart';
import '../../../core/widgets/common.dart';
import '../../../core/widgets/skeleton.dart';
import '../../../core/widgets/state_views.dart';
import '../data/order_models.dart';
import '../data/orders_repository.dart';
import 'order_widgets.dart';
import 'shop_capture_widgets.dart';

class OrderDetailScreen extends ConsumerStatefulWidget {
  const OrderDetailScreen({super.key, required this.orderId});

  final int orderId;

  @override
  ConsumerState<OrderDetailScreen> createState() => _OrderDetailScreenState();
}

/// A phone number that opens the dialer.
class _PhoneRow extends StatelessWidget {
  const _PhoneRow({required this.label, required this.phone});

  final String label;
  final String phone;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: () => Launch.call(context, phone),
      child: Row(
        children: [
          Expanded(child: InfoRow(label: label, value: phone)),
          const Padding(
            padding: EdgeInsetsDirectional.only(start: 6),
            child: Icon(Icons.call_rounded, size: 18, color: AppColors.primary),
          ),
        ],
      ),
    );
  }
}

class _OrderDetailScreenState extends ConsumerState<OrderDetailScreen> {
  bool _cancelling = false;

  Future<void> _cancel(OrderDetail order) async {
    final reason = TextEditingController();
    final ok = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('إلغاء الطلب'),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text('هل أنت متأكد من إلغاء الطلب ${order.summary.orderNumber}؟'),
            const SizedBox(height: 12),
            TextField(
              controller: reason,
              maxLength: 200,
              decoration: const InputDecoration(labelText: 'سبب الإلغاء (اختياري)'),
            ),
          ],
        ),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx, false), child: const Text('تراجع')),
          FilledButton(
            style: FilledButton.styleFrom(backgroundColor: AppColors.danger),
            onPressed: () => Navigator.pop(ctx, true),
            child: const Text('إلغاء الطلب'),
          ),
        ],
      ),
    );
    final text = reason.text.trim();
    reason.dispose();
    if (ok != true || !mounted) return;
    setState(() => _cancelling = true);
    try {
      await ref.read(ordersRepositoryProvider).cancel(order.id, reason: text.isEmpty ? null : text);
      ref.invalidate(orderDetailProvider(widget.orderId));
      ref.read(ordersVersionProvider.notifier).bump();
      if (mounted) context.showSnack('تم إلغاء الطلب');
    } catch (e) {
      if (mounted) context.showError(e);
    } finally {
      if (mounted) setState(() => _cancelling = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final async = ref.watch(orderDetailProvider(widget.orderId));
    return Scaffold(
      appBar: AppBar(title: const Text('طلب البيع')),
      body: AsyncView<OrderDetail>(
        value: async,
        onRetry: () => ref.invalidate(orderDetailProvider(widget.orderId)),
        loading: const SkeletonList(count: 4, itemHeight: 120),
        data: (o) => RefreshIndicator(
          onRefresh: () => ref.refresh(orderDetailProvider(widget.orderId).future),
          child: ListView(
            padding: const EdgeInsets.all(16),
            children: [
              // What was sold and where the order stands.
              AppCard(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      children: [
                        Flexible(
                          child: InkWell(
                            onTap: () {
                              Clipboard.setData(ClipboardData(text: o.summary.orderNumber));
                              context.showSnack('تم نسخ رقم الطلب');
                            },
                            child: Row(
                              mainAxisSize: MainAxisSize.min,
                              children: [
                                Flexible(
                                  child: Text(o.summary.orderNumber,
                                      textDirection: TextDirection.ltr,
                                      overflow: TextOverflow.ellipsis,
                                      style: const TextStyle(color: AppColors.textMuted, fontWeight: FontWeight.w700)),
                                ),
                                const SizedBox(width: 4),
                                const Icon(Icons.copy_rounded, size: 14, color: AppColors.textMuted),
                              ],
                            ),
                          ),
                        ),
                        const SizedBox(width: 8),
                        OrderStatusChip(status: o.status, label: o.summary.statusLabel),
                      ],
                    ),
                    const SizedBox(height: 12),
                    for (final item in o.items)
                      Padding(
                        padding: const EdgeInsets.symmetric(vertical: 3),
                        child: Row(
                          children: [
                            const Icon(Icons.inventory_2_outlined, size: 20, color: AppColors.primary),
                            const SizedBox(width: 8),
                            Expanded(child: Text(item.name, style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 16))),
                            if (item.quantity > 1) Text('× ${item.quantity}', style: const TextStyle(fontWeight: FontWeight.w800)),
                          ],
                        ),
                      ),
                    const Divider(height: 22),
                    InfoRow(label: 'طريقة الشراء', value: o.summary.purchaseType.label),
                    if (o.summary.cityName != null) InfoRow(label: 'الفرع', value: o.summary.cityName!),
                    InfoRow(label: 'تاريخ الطلب', value: Fmt.dateTime(o.summary.createdAt)),
                  ],
                ),
              ),
              if (o.status.hasPublicReason && (o.publicReason ?? '').isNotEmpty) ...[
                const SizedBox(height: 12),
                NoticeBanner(
                  title: o.status == OrderStatus.denied ? 'سبب الرفض' : 'سبب عدم إتمام البيع',
                  message: o.publicReason!,
                  icon: Icons.info_outline_rounded,
                  color: o.status.color,
                ),
              ],
              if (o.sale != null) ...[
                const SizedBox(height: 12),
                SalePanel(sale: o.sale!, finished: o.status == OrderStatus.finished),
              ],
              const SizedBox(height: 12),
              AppCard(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Text('مراحل الطلب', style: TextStyle(fontWeight: FontWeight.w800, fontSize: 16)),
                    const SizedBox(height: 14),
                    OrderTimeline(status: o.status, timeline: o.timeline),
                  ],
                ),
              ),
              const SizedBox(height: 12),
              AppCard(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Text('الزبون', style: TextStyle(fontWeight: FontWeight.w800, fontSize: 16)),
                    const SizedBox(height: 8),
                    InfoRow(label: 'الاسم', value: o.customer?.fullName ?? o.summary.customerName),
                    if (o.customer != null) _PhoneRow(label: 'الهاتف', phone: o.customer!.phone),
                    if (o.customer?.secondaryPhone != null) _PhoneRow(label: 'رقم ثانٍ', phone: o.customer!.secondaryPhone!),
                    if (o.address != null) ...[
                      InfoRow(label: 'المنطقة', value: o.address!.area),
                      InfoRow(label: 'العنوان', value: o.address!.details),
                      if (o.address!.nearestLandmark != null) InfoRow(label: 'قرب', value: o.address!.nearestLandmark!),
                    ],
                    if ((o.notes ?? '').isNotEmpty) InfoRow(label: 'ملاحظات', value: o.notes!),
                  ],
                ),
              ),
              if (o.shopPhotoUrl != null || o.address?.latitude != null || o.shop != null) ...[
                const SizedBox(height: 12),
                AppCard(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      const Text('المحل', style: TextStyle(fontWeight: FontWeight.w800, fontSize: 16)),
                      if (o.shop != null) ...[
                        const SizedBox(height: 6),
                        if (o.shop!.name.isNotEmpty) InfoRow(label: 'اسم المحل', value: o.shop!.name),
                        if (o.shop!.businessType.isNotEmpty) InfoRow(label: 'نوع المحل', value: o.shop!.businessType),
                      ],
                      const SizedBox(height: 10),
                      if (o.shopPhotoUrl != null) AppNetworkImage(url: o.shopPhotoUrl, height: 180, radius: 12),
                      if (o.address?.latitude != null && o.address?.longitude != null) ...[
                        const SizedBox(height: 10),
                        StaticPinMap(latitude: o.address!.latitude!, longitude: o.address!.longitude!),
                      ],
                    ],
                  ),
                ),
              ],
              if (o.canCancel) ...[
                const SizedBox(height: 18),
                OutlinedButton.icon(
                  style: OutlinedButton.styleFrom(
                    foregroundColor: AppColors.danger,
                    side: const BorderSide(color: AppColors.danger),
                  ),
                  onPressed: _cancelling ? null : () => _cancel(o),
                  icon: _cancelling
                      ? const SizedBox(width: 18, height: 18, child: CircularProgressIndicator(strokeWidth: 2))
                      : const Icon(Icons.cancel_outlined),
                  label: const Text('إلغاء الطلب'),
                ),
              ],
              const SizedBox(height: 24),
            ],
          ),
        ),
      ),
    );
  }
}
