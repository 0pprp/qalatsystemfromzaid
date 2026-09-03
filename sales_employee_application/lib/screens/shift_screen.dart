import 'package:flutter/material.dart';
import 'package:sales_employee_application/config/app_env.dart';
import 'package:sales_employee_application/data/sales_repository_factory.dart';
import 'package:sales_employee_application/screens/home_screen.dart';
import 'package:sales_employee_application/services/session.dart';
import 'package:sales_employee_application/tracking/shift_start_debug.dart';
import 'package:sales_employee_application/tracking/shift_tracking_controller.dart';
import 'package:sales_employee_application/utils/app_theme.dart';

class ShiftScreen extends StatefulWidget {
  const ShiftScreen({super.key, this.controller});

  final ShiftTrackingController? controller;

  @override
  State<ShiftScreen> createState() => _ShiftScreenState();
}

class _ShiftScreenState extends State<ShiftScreen> {
  bool _loading = false;
  String? _error;

  Future<void> _start() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    final controller = widget.controller ??
        (TrackingRuntime.instance ??= ShiftTrackingController(repository: SalesRepositoryFactory.instance));
    try {
      final shift = await controller.startShiftFlow();
      if (!mounted) return;
      if (shift == null) {
        setState(() {
          _loading = false;
          _error = controller.lastError ?? ShiftStartDebug.generic;
        });
        return;
      }
      if (!mounted) return;
      setState(() => _loading = false);
      ShiftStartDebug.log('navigating home');
      Navigator.pushReplacement(
        context,
        MaterialPageRoute(builder: (_) => const HomeScreen()),
      );
    } catch (e, st) {
      ShiftStartDebug.logError('ShiftScreen._start', e, st);
      if (!mounted) return;
      setState(() {
        _loading = false;
        _error = ShiftStartDebug.apiFailure(e);
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.all(AppSpacing.lg),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const SizedBox(height: AppSpacing.xl),
              Image.asset(
                'assets/icons/LogoCompany.png',
                height: 88,
                errorBuilder: (context, error, stackTrace) => const SizedBox(height: 88),
              ),
              const SizedBox(height: AppSpacing.md),
              const Text(
                'شركة قلعة الضمان',
                textAlign: TextAlign.center,
                style: TextStyle(
                  fontSize: 22,
                  fontWeight: FontWeight.bold,
                  color: AppColors.darkGreen,
                ),
              ),
              const SizedBox(height: AppSpacing.xl),
              Text(Session.userName.isEmpty ? 'موظف المبيعات' : Session.userName,
                  textAlign: TextAlign.center,
                  style: const TextStyle(fontSize: 18, fontWeight: FontWeight.w700)),
              const SizedBox(height: AppSpacing.xs),
              Text(
                Session.cityName.isEmpty ? AppEnv.loginCityLabel() : Session.cityName,
                textAlign: TextAlign.center,
                style: const TextStyle(color: AppColors.muted),
              ),
              const Spacer(),
              const Text(
                'يجب بدء الدوام لاستخدام تطبيق المبيعات',
                textAlign: TextAlign.center,
                style: TextStyle(fontSize: 16),
              ),
              if (_error != null) ...[
                const SizedBox(height: AppSpacing.sm),
                Text(_error!, textAlign: TextAlign.center, style: const TextStyle(color: AppColors.danger)),
              ],
              const SizedBox(height: AppSpacing.lg),
              ElevatedButton(
                onPressed: _loading ? null : _start,
                child: Text(_loading ? '...' : 'بدء الدوام'),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
