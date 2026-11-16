import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/theme/app_theme.dart';
import '../../../core/widgets/paged_list.dart';
import '../../../core/widgets/skeleton.dart';
import '../../../core/widgets/state_views.dart';
import '../data/order_models.dart';
import '../data/orders_repository.dart';
import 'order_widgets.dart';

/// One tab of «الطلبات»: a group of statuses an order passes through.
enum OrdersTab {
  open('قيد المتابعة', Icons.pending_actions_rounded,
      [OrderStatus.newOrder, OrderStatus.underReview, OrderStatus.transferred, OrderStatus.fieldInspection],
      'لا توجد طلبات قيد المتابعة', 'أرسل الطلب من تبويب «طلب بيع».'),
  paying('قيد التسديد', Icons.payments_rounded, [OrderStatus.sold],
      'لا توجد مبيعات قيد التسديد', 'عندما يتحول طلبك إلى مبيعة تظهر هنا مع نسبة التسديد حتى اكتمالها.'),
  finished('مكتملة', Icons.task_alt_rounded, [OrderStatus.finished],
      'لا توجد مبيعات مكتملة بعد', 'المبيعات المسددة بالكامل تظهر هنا.'),
  closed('مرفوضة / ملغاة', Icons.block_rounded, [OrderStatus.denied, OrderStatus.notSold, OrderStatus.cancelled],
      'لا توجد طلبات مرفوضة', null);

  const OrdersTab(this.label, this.icon, this.statuses, this.emptyTitle, this.emptyMessage);

  final String label;
  final IconData icon;
  final List<OrderStatus> statuses;
  final String emptyTitle;
  final String? emptyMessage;
}

/// الطلبات — sale orders from submission until the sale is fully paid.
class OrdersScreen extends ConsumerWidget {
  const OrdersScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final counts = ref.watch(orderCountsProvider).value;
    int countOf(OrdersTab t) => counts == null ? 0 : t.statuses.fold(0, (s, st) => s + (counts[st] ?? 0));
    return DefaultTabController(
      length: OrdersTab.values.length,
      child: Scaffold(
        appBar: AppBar(
          title: const Text('الطلبات'),
          bottom: TabBar(
            isScrollable: true,
            tabAlignment: TabAlignment.start,
            labelStyle: const TextStyle(fontWeight: FontWeight.w800, fontFamily: AppTheme.bodyFont),
            tabs: [
              for (final t in OrdersTab.values)
                Tab(
                  child: Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Icon(t.icon, size: 18),
                      const SizedBox(width: 6),
                      Text(t.label),
                      if (countOf(t) > 0) ...[
                        const SizedBox(width: 6),
                        Badge(label: Text('${countOf(t)}'), backgroundColor: AppColors.primary),
                      ],
                    ],
                  ),
                ),
            ],
          ),
        ),
        body: TabBarView(children: [for (final t in OrdersTab.values) _OrdersTabView(tab: t)]),
      ),
    );
  }
}

class _OrdersTabView extends ConsumerWidget {
  const _OrdersTabView({required this.tab});

  final OrdersTab tab;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final version = ref.watch(ordersVersionProvider);
    return PagedListView<OrderSummary>(
      key: ValueKey('orders-${tab.name}-$version'),
      fetch: (page) => ref.read(ordersRepositoryProvider).list(statuses: tab.statuses, page: page),
      onRefresh: () => ref.invalidate(orderCountsProvider),
      padding: const EdgeInsets.fromLTRB(16, 12, 16, 96),
      skeleton: const SkeletonList(count: 4, itemHeight: 110),
      itemBuilder: (_, o, _) => OrderCard(order: o),
      empty: EmptyState(icon: tab.icon, title: tab.emptyTitle, message: tab.emptyMessage),
    );
  }
}
