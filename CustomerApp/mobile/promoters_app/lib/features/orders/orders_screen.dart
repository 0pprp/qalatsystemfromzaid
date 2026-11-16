import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/app_state.dart';
import '../../core/widgets/paged_list.dart';
import '../../core/widgets/state_views.dart';
import '../../data/models.dart';
import '../../data/repository.dart';
import 'order_widgets.dart';

/// «طلباتي»: follow every order until it becomes a sale.
class OrdersScreen extends ConsumerWidget {
  const OrdersScreen({super.key});

  static const _tabs = [
    ('قيد المتابعة', OrderStatuses.open),
    ('تم البيع', OrderStatuses.sold),
    ('لم يتم', OrderStatuses.closed),
    ('الكل', ''),
  ];

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final version = ref.watch(dataVersionProvider);
    return DefaultTabController(
      length: _tabs.length,
      child: Scaffold(
        appBar: AppBar(
          title: const Text('طلباتي'),
          bottom: TabBar(isScrollable: true, tabAlignment: TabAlignment.start, tabs: [for (final t in _tabs) Tab(text: t.$1)]),
        ),
        floatingActionButton: FloatingActionButton(
          onPressed: () => context.push('/new-order'),
          tooltip: 'طلب بيع جديد',
          child: const Icon(Icons.add_rounded),
        ),
        body: TabBarView(
          children: [
            for (final t in _tabs)
              PagedListView<OrderSummary>(
                key: ValueKey('${t.$2}-$version'),
                fetch: (page) => ref.read(repositoryProvider).orders(status: t.$2.isEmpty ? null : t.$2, page: page),
                itemBuilder: (_, o, _) => OrderTile(order: o),
                padding: const EdgeInsets.fromLTRB(16, 12, 16, 96),
                separator: 10,
                empty: EmptyState(
                  icon: Icons.receipt_long_rounded,
                  title: t.$2 == OrderStatuses.sold ? 'لا توجد مبيعات بعد' : 'لا توجد طلبات',
                  message: 'كل طلب ترسله يظهر هنا حتى يصبح مبيعة',
                ),
              ),
          ],
        ),
      ),
    );
  }
}
