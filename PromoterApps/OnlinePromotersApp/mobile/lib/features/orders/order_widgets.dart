import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../core/theme/app_theme.dart';
import '../../core/utils/formatters.dart';
import '../../core/widgets/common.dart';
import '../../data/models.dart';

class ProductRow extends StatelessWidget {
  const ProductRow({super.key, required this.product, this.onTap});

  final Product product;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
        child: Row(
          children: [
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(product.name, style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 15)),
                  const SizedBox(height: 2),
                  Text(Fmt.money(product.price), style: const TextStyle(color: AppColors.textMuted, fontSize: 13)),
                ],
              ),
            ),
            Column(
              crossAxisAlignment: CrossAxisAlignment.end,
              children: [
                const Text('عمولتي', style: TextStyle(color: AppColors.textMuted, fontSize: 11)),
                Text(Fmt.money(product.myCommission),
                    style: const TextStyle(color: AppColors.success, fontWeight: FontWeight.w800, fontSize: 15)),
              ],
            ),
            if (onTap != null) ...[
              const SizedBox(width: 6),
              const Icon(Icons.chevron_left_rounded, color: AppColors.textMuted),
            ],
          ],
        ),
      ),
    );
  }
}

class OrderTile extends StatelessWidget {
  const OrderTile({super.key, required this.order});

  final OrderSummary order;

  @override
  Widget build(BuildContext context) {
    final color = OrderStatuses.color(order.status);
    return AppCard(
      onTap: () => context.push('/orders/${order.id}'),
      padding: const EdgeInsets.all(14),
      child: Row(
        children: [
          Container(
            width: 42,
            height: 42,
            decoration: BoxDecoration(color: color.withValues(alpha: 0.12), borderRadius: BorderRadius.circular(12)),
            child: Icon(OrderStatuses.icon(order.status), color: color),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(order.quantity > 1 ? '${order.productName} ×${order.quantity}' : order.productName,
                    style: const TextStyle(fontWeight: FontWeight.w700), maxLines: 1, overflow: TextOverflow.ellipsis),
                const SizedBox(height: 2),
                Text('${order.customerName} · ${Fmt.relative(order.createdAt)}',
                    style: const TextStyle(color: AppColors.textMuted, fontSize: 12.5), maxLines: 1, overflow: TextOverflow.ellipsis),
              ],
            ),
          ),
          const SizedBox(width: 8),
          Column(
            crossAxisAlignment: CrossAxisAlignment.end,
            children: [
              StatusChip(label: order.statusLabel, color: color),
              const SizedBox(height: 4),
              Text(
                Fmt.money(order.myCommission),
                style: TextStyle(
                  fontSize: 12.5,
                  fontWeight: FontWeight.w700,
                  color: order.status == 'Sold' ? AppColors.success : AppColors.textMuted,
                  decoration: order.status == 'NotSold' || order.status == 'Cancelled' ? TextDecoration.lineThrough : null,
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}
