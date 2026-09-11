import 'package:follower_application/tracking/shift_gate_coordinator.dart';
import 'package:follower_application/ui/app_safe_scaffold.dart';
import 'package:follower_application/utils/AppTheme.dart';
import 'package:flutter/material.dart';

class WelcomePage extends StatefulWidget {
  const WelcomePage({super.key});
  @override
  State<WelcomePage> createState() => _WelcomePage();
}

class _WelcomePage extends State<WelcomePage> {
  @override
  void initState() {
    super.initState();
    _boot();
  }

  Future<void> _boot() async {
    await Future.delayed(const Duration(milliseconds: 600));
    if (!mounted) return;
    final dest = await ShiftGateCoordinator().resolve(attachIfActive: true);
    if (!mounted) return;
    final route = ShiftGateCoordinator.routeFor(dest);
    Navigator.pushNamedAndRemoveUntil(context, route, (r) => false);
  }

  @override
  Widget build(BuildContext context) {
    return AppSafeScaffold(
      backgroundColor: Theme.of(context).scaffoldBackgroundColor,
      body: Stack(
        children: [
          Positioned(
              top: -60,
              right: -60,
              child: Container(
                width: 150,
                height: 150,
                decoration: BoxDecoration(
                    color: AppTheme.primaryColor.withValues(alpha: 0.1),
                    shape: BoxShape.circle),
              )),
          Positioned(
              bottom: -80,
              left: -80,
              child: Container(
                width: 200,
                height: 200,
                decoration: BoxDecoration(
                    color: AppTheme.secondaryColor.withValues(alpha: 0.1),
                    shape: BoxShape.circle),
              )),
          Center(
            child: Column(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                Hero(
                  tag: 'logo',
                  child: Image.asset('assets/icons/logoMain.png', width: 180),
                ),
                const SizedBox(height: 16),
                const Text(
                  'تطبيق المتابع',
                  style: TextStyle(
                    fontFamily: 'Cairo',
                    fontSize: 22,
                    fontWeight: FontWeight.bold,
                    color: AppTheme.primaryColor,
                  ),
                ),
                const SizedBox(height: 30),
                const CircularProgressIndicator(color: AppTheme.primaryColor),
                const SizedBox(height: 20),
                const Text('جاري التحقق من الجلسة والدوام...',
                    style: TextStyle(fontFamily: 'Cairo', color: Colors.grey))
              ],
            ),
          ),
        ],
      ),
    );
  }
}
