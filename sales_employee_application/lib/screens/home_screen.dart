import 'package:flutter/material.dart';
import 'package:sales_employee_application/config/app_env.dart';
import 'package:sales_employee_application/services/session.dart';
import 'package:sales_employee_application/tracking/shift_tracking_controller.dart';
import 'package:sales_employee_application/utils/app_theme.dart';

class HomeScreen extends StatefulWidget {
  const HomeScreen({super.key});

  @override
  State<HomeScreen> createState() => _HomeScreenState();
}

class _HomeScreenState extends State<HomeScreen> with WidgetsBindingObserver {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
    TrackingRuntime.instance?.restoreIfNeeded();
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    super.dispose();
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed) {
      TrackingRuntime.instance?.restoreIfNeeded();
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: SafeArea(
        child: ListView(
          padding: const EdgeInsets.all(AppSpacing.lg),
          children: [
            const Text('قلعة الضمان',
                style: TextStyle(
                    fontSize: 22,
                    fontWeight: FontWeight.bold,
                    color: AppColors.darkGreen)),
            const SizedBox(height: AppSpacing.xs),
            Text(Session.userName.isEmpty ? 'موظف المبيعات' : Session.userName,
                style: const TextStyle(fontSize: 16, fontWeight: FontWeight.w700)),
            Text(
              Session.cityName.isEmpty ? AppEnv.loginCityLabel() : Session.cityName,
              style: const TextStyle(color: AppColors.muted),
            ),
            const SizedBox(height: AppSpacing.md),
            Container(
              padding: const EdgeInsets.symmetric(
                  horizontal: AppSpacing.md, vertical: AppSpacing.sm),
              decoration: BoxDecoration(
                color: AppColors.lightGreen.withValues(alpha: 0.12),
                borderRadius: BorderRadius.circular(AppRadius.sm),
              ),
              child: const Text('حالة الدوام: بدأ الدوام',
                  style: TextStyle(color: AppColors.darkGreen, fontWeight: FontWeight.w700)),
            ),
            const SizedBox(height: AppSpacing.lg),
            InkWell(
              onTap: () => Navigator.pushNamed(context, '/search'),
              child: InputDecorator(
                decoration: const InputDecoration(
                  prefixIcon: Icon(Icons.search),
                  hintText: 'ابحث عن زبون',
                ),
                child: const SizedBox(height: 20),
              ),
            ),
            const SizedBox(height: AppSpacing.lg),
            _ActionCard(
              title: 'إنشاء عملية بيع',
              icon: Icons.point_of_sale_outlined,
              onTap: () => Navigator.pushNamed(context, '/sale'),
            ),
            _ActionCard(
              title: 'مخزن الفرع',
              icon: Icons.inventory_2_outlined,
              onTap: () => Navigator.pushNamed(context, '/warehouse'),
            ),
            _ActionCard(
              title: 'المبيعات',
              icon: Icons.receipt_long_outlined,
              onTap: () => Navigator.pushNamed(context, '/sales'),
            ),
            Align(
              alignment: Alignment.centerLeft,
              child: TextButton(
                onPressed: () async {
                  await Session.logout();
                  if (context.mounted) {
                    Navigator.pushNamedAndRemoveUntil(context, '/login', (_) => false);
                  }
                },
                child: const Text('تسجيل الخروج'),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _ActionCard extends StatelessWidget {
  const _ActionCard({required this.title, required this.icon, required this.onTap});
  final String title;
  final IconData icon;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: ListTile(
        onTap: onTap,
        leading: Icon(icon, color: AppColors.darkGreen),
        title: Text(title, style: const TextStyle(fontWeight: FontWeight.w700)),
        trailing: const Icon(Icons.chevron_left),
      ),
    );
  }
}
