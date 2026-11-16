import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/app_state.dart';
import '../../core/theme/app_theme.dart';
import '../../core/utils/formatters.dart';
import '../../core/widgets/common.dart';
import '../../core/widgets/state_views.dart';
import '../../data/models.dart';
import '../auth/auth_controller.dart';
import '../orders/order_widgets.dart';
import '../shell/shell_screens.dart';

/// «الرئيسية»: balance, this month's numbers, the products with my commission, and the latest orders.
class HomeScreen extends ConsumerWidget {
  const HomeScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final me = ref.watch(currentUserProvider);
    final overview = ref.watch(overviewProvider);
    final products = ref.watch(productsProvider);
    ref.listen(overviewProvider, (_, next) {
      final n = next.value?.unreadNotifications;
      if (n != null) ref.read(unreadCountProvider.notifier).set(n);
    });

    Future<void> reload() async {
      ref.read(dataVersionProvider.notifier).bump();
      try {
        await ref.read(overviewProvider.future);
      } catch (_) {}
    }

    return Scaffold(
      appBar: AppBar(
        centerTitle: false,
        title: Text('أهلاً ${me?.username ?? ''}'),
        actions: const [NotificationsBell(), SizedBox(width: 4)],
      ),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: () => context.push('/new-order'),
        icon: const Icon(Icons.add_rounded),
        label: const Text('طلب بيع جديد'),
      ),
      body: RefreshIndicator(
        onRefresh: reload,
        child: ListView(
          padding: const EdgeInsets.fromLTRB(16, 4, 16, 96),
          children: [
            AsyncView<Overview>(
              value: overview,
              onRetry: () => ref.invalidate(overviewProvider),
              loading: const SizedBox(height: 180, child: Center(child: CircularProgressIndicator())),
              data: (o) => Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  _BalanceCard(overview: o),
                  const SizedBox(height: 12),
                  Row(
                    children: [
                      Expanded(child: StatTile(label: 'طلبات الشهر', value: '${o.ordersThisMonth}', icon: Icons.receipt_long_rounded, color: AppColors.info)),
                      const SizedBox(width: 10),
                      Expanded(child: StatTile(label: 'مبيعات الشهر', value: '${o.soldThisMonth}', icon: Icons.check_circle_rounded, color: AppColors.success)),
                      const SizedBox(width: 10),
                      Expanded(
                        child: StatTile(
                          label: 'قيد المتابعة',
                          value: '${o.openOrders}',
                          icon: Icons.schedule_rounded,
                          color: AppColors.warning,
                          onTap: () => context.go('/orders'),
                        ),
                      ),
                    ],
                  ),
                  if (me?.canCreateAccounts == true || o.teamCount > 0) ...[
                    const SizedBox(height: 12),
                    AppCard(
                      onTap: () => context.push('/team'),
                      child: Row(
                        children: [
                          const Icon(Icons.groups_rounded, color: AppColors.primary),
                          const SizedBox(width: 12),
                          Expanded(
                            child: Text(o.teamCount == 0 ? 'فريقي — أنشئ حساباً لمروّج يعمل معك' : 'فريقي (${o.teamCount})',
                                style: const TextStyle(fontWeight: FontWeight.w700)),
                          ),
                          const Icon(Icons.chevron_left_rounded, color: AppColors.textMuted),
                        ],
                      ),
                    ),
                  ],
                ],
              ),
            ),
            SectionHeader(title: 'المنتجات وعمولتي', padding: const EdgeInsets.fromLTRB(4, 22, 4, 8)),
            AsyncView<List<Product>>(
              value: products,
              onRetry: () => ref.invalidate(productsProvider),
              loading: const Padding(padding: EdgeInsets.all(24), child: Center(child: CircularProgressIndicator())),
              data: (list) => list.isEmpty
                  ? const AppCard(child: Text('لا توجد منتجات متاحة حالياً', style: TextStyle(color: AppColors.textMuted)))
                  : Card(
                      child: Column(
                        children: [
                          for (final (i, p) in list.indexed) ...[
                            if (i > 0) const Divider(height: 1),
                            ProductRow(product: p, onTap: () => context.push('/new-order', extra: p)),
                          ],
                        ],
                      ),
                    ),
            ),
            if (overview.value?.recentOrders.isNotEmpty == true) ...[
              SectionHeader(
                title: 'آخر الطلبات',
                actionLabel: 'الكل',
                onAction: () => context.go('/orders'),
                padding: const EdgeInsets.fromLTRB(4, 22, 0, 8),
              ),
              for (final o in overview.value!.recentOrders) ...[
                OrderTile(order: o),
                const SizedBox(height: 10),
              ],
            ],
          ],
        ),
      ),
    );
  }

}

class _BalanceCard extends StatelessWidget {
  const _BalanceCard({required this.overview});

  final Overview overview;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(22),
        gradient: const LinearGradient(colors: [AppColors.primary, AppColors.primaryDark], begin: Alignment.topRight, end: Alignment.bottomLeft),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text('رصيدي', style: TextStyle(color: Colors.white70)),
          const SizedBox(height: 4),
          FittedBox(
            fit: BoxFit.scaleDown,
            child: Text(Fmt.money(overview.balance),
                style: const TextStyle(color: Colors.white, fontSize: 30, fontWeight: FontWeight.w800)),
          ),
          const SizedBox(height: 10),
          Row(
            children: [
              Expanded(
                child: Text('أرباح هذا الشهر: ${Fmt.money(overview.earnedThisMonth)}',
                    style: const TextStyle(color: Colors.white, fontWeight: FontWeight.w600)),
              ),
              TextButton(
                style: TextButton.styleFrom(foregroundColor: Colors.white, backgroundColor: Colors.white24),
                onPressed: () => context.go('/wallet'),
                child: const Text('المحفظة'),
              ),
            ],
          ),
        ],
      ),
    );
  }
}
