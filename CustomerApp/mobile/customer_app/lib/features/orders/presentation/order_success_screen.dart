import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:go_router/go_router.dart';

import '../../../core/theme/app_theme.dart';
import '../../../core/widgets/state_views.dart';
import '../data/order_models.dart';

class OrderSuccessScreen extends StatelessWidget {
  const OrderSuccessScreen({super.key, required this.order});

  final OrderDetail? order;

  @override
  Widget build(BuildContext context) {
    final o = order;
    return PopScope(
      canPop: false,
      onPopInvokedWithResult: (didPop, _) {
        if (!didPop) context.go('/orders');
      },
      child: Scaffold(
        backgroundColor: Colors.white,
        body: SafeArea(
          child: Padding(
            padding: const EdgeInsets.all(24),
            child: Column(
              children: [
                const Spacer(),
                TweenAnimationBuilder<double>(
                  tween: Tween(begin: 0.4, end: 1),
                  duration: const Duration(milliseconds: 600),
                  curve: Curves.elasticOut,
                  builder: (_, v, child) => Transform.scale(scale: v, child: child),
                  child: Container(
                    width: 120,
                    height: 120,
                    decoration: const BoxDecoration(color: AppColors.primaryLight, shape: BoxShape.circle),
                    child: const Icon(Icons.check_circle_rounded, color: AppColors.success, size: 84),
                  ),
                ),
                const SizedBox(height: 24),
                Text('تم إرسال الطلب', style: Theme.of(context).textTheme.headlineSmall),
                const SizedBox(height: 10),
                Text(
                  o == null || o.status == OrderStatus.newOrder
                      ? 'الطلب محفوظ وسيُرسل إلى قسم المبيعات تلقائياً.'
                      : 'وصل الطلب إلى قسم المبيعات. تابع حالته من «الطلبات» حتى يكتمل تسديده.',
                  textAlign: TextAlign.center,
                  style: const TextStyle(color: AppColors.textMuted, height: 1.7),
                ),
                if (o != null) ...[
                  const SizedBox(height: 22),
                  InkWell(
                    borderRadius: BorderRadius.circular(16),
                    onTap: () {
                      Clipboard.setData(ClipboardData(text: o.summary.orderNumber));
                      context.showSnack('تم نسخ رقم الطلب');
                    },
                    child: Container(
                      padding: const EdgeInsets.symmetric(horizontal: 22, vertical: 14),
                      decoration: BoxDecoration(
                        color: AppColors.background,
                        borderRadius: BorderRadius.circular(16),
                        border: Border.all(color: AppColors.border),
                      ),
                      child: Column(
                        children: [
                          const Text('رقم الطلب', style: TextStyle(color: AppColors.textMuted)),
                          const SizedBox(height: 4),
                          Row(
                            mainAxisSize: MainAxisSize.min,
                            children: [
                              Flexible(
                                child: FittedBox(
                                  fit: BoxFit.scaleDown,
                                  child: Text(
                                    o.summary.orderNumber,
                                    textDirection: TextDirection.ltr,
                                    style: const TextStyle(fontSize: 22, fontWeight: FontWeight.w800, letterSpacing: 1),
                                  ),
                                ),
                              ),
                              const SizedBox(width: 8),
                              const Icon(Icons.copy_rounded, size: 18, color: AppColors.textMuted),
                            ],
                          ),
                          const SizedBox(height: 6),
                          Text(o.summary.itemsSummary,
                              textAlign: TextAlign.center, style: const TextStyle(color: AppColors.primary, fontWeight: FontWeight.w700)),
                        ],
                      ),
                    ),
                  ),
                ],
                const Spacer(),
                if (o != null)
                  FilledButton(
                    style: FilledButton.styleFrom(minimumSize: const Size.fromHeight(52)),
                    onPressed: () {
                      final router = GoRouter.of(context);
                      router.go('/orders');
                      router.push('/orders/${o.id}');
                    },
                    child: const Text('متابعة الطلب'),
                  ),
                const SizedBox(height: 10),
                OutlinedButton(
                  style: OutlinedButton.styleFrom(minimumSize: const Size.fromHeight(52)),
                  onPressed: () => context.go('/new'),
                  child: const Text('طلب جديد'),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
