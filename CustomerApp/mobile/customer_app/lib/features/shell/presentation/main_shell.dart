import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app_config/data/app_config_repository.dart';
import '../../../core/utils/launcher.dart';
import '../../../core/config/app_config.dart';
import '../../work/data/work_controller.dart';

/// Bottom navigation: new sale order (opens first), my summary, orders, working day, account.
class MainShell extends ConsumerStatefulWidget {
  const MainShell({super.key, required this.shell});

  final StatefulNavigationShell shell;

  @override
  ConsumerState<MainShell> createState() => _MainShellState();
}

class _MainShellState extends ConsumerState<MainShell> {
  bool _softUpdateShown = false;
  void _maybeShowSoftUpdate() {
    if (_softUpdateShown || !ref.read(updateAvailableProvider)) return;
    _softUpdateShown = true;
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(
        content: const Text('يتوفر إصدار أحدث من التطبيق'),
        duration: const Duration(seconds: 6),
        action: SnackBarAction(
          label: 'تحديث',
          onPressed: () => Launch.url(context, ref.read(configProvider).apkUrl ?? AppConfig.apkDownloadUrl),
        ),
      ));
    });
  }

  @override
  Widget build(BuildContext context) {
    ref.listen(updateAvailableProvider, (_, _) => _maybeShowSoftUpdate());
    _maybeShowSoftUpdate();
    final working = ref.watch(workControllerProvider.select((s) => s.isWorking));
    final shell = widget.shell;
    return Scaffold(
      body: shell,
      bottomNavigationBar: NavigationBar(
        selectedIndex: shell.currentIndex,
        onDestinationSelected: (i) => shell.goBranch(i, initialLocation: i == shell.currentIndex),
        destinations: [
          const NavigationDestination(
            icon: Icon(Icons.add_circle_outline_rounded),
            selectedIcon: Icon(Icons.add_circle_rounded),
            label: 'طلب بيع',
          ),
          const NavigationDestination(
            icon: Icon(Icons.storefront_outlined),
            selectedIcon: Icon(Icons.storefront_rounded),
            label: 'المحلات',
          ),
          const NavigationDestination(
            icon: Icon(Icons.receipt_long_outlined),
            selectedIcon: Icon(Icons.receipt_long_rounded),
            label: 'الطلبات',
          ),
          NavigationDestination(
            icon: Badge(
              isLabelVisible: working,
              backgroundColor: Colors.green,
              smallSize: 10,
              child: const Icon(Icons.map_outlined),
            ),
            selectedIcon: Badge(
              isLabelVisible: working,
              backgroundColor: Colors.green,
              smallSize: 10,
              child: const Icon(Icons.map_rounded),
            ),
            label: 'الدوام',
          ),
          const NavigationDestination(
            icon: Icon(Icons.person_outline_rounded),
            selectedIcon: Icon(Icons.person_rounded),
            label: 'حسابي',
          ),
        ],
      ),
    );
  }
}
