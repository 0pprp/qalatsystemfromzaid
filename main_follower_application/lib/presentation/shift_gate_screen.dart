import 'package:flutter/material.dart';
import 'package:main_follower_application/core/auth/session.dart';
import 'package:main_follower_application/presentation/login_screen.dart';
import 'package:main_follower_application/presentation/missions_screen.dart';
import 'package:main_follower_application/presentation/start_shift_screen.dart';
import 'package:main_follower_application/tracking/shift_tracking_controller.dart';

/// After login / cold start: require an active shift before missions.
class ShiftGateScreen extends StatefulWidget {
  const ShiftGateScreen({super.key});

  @override
  State<ShiftGateScreen> createState() => _ShiftGateScreenState();
}

class _ShiftGateScreenState extends State<ShiftGateScreen> {
  late Future<Widget> _future;

  @override
  void initState() {
    super.initState();
    _future = _resolve();
  }

  Future<Widget> _resolve() async {
    if (!Session.isLoggedIn) return const LoginScreen();
    final shift = await ShiftTrackingController.instance.refreshCurrent();
    if (shift != null && shift.isActive) return const MissionsScreen();
    return const StartShiftScreen();
  }

  @override
  Widget build(BuildContext context) {
    return FutureBuilder<Widget>(
      future: _future,
      builder: (context, snap) {
        if (snap.connectionState != ConnectionState.done) {
          return const Scaffold(
            body: Center(child: CircularProgressIndicator()),
          );
        }
        return snap.data ?? const StartShiftScreen();
      },
    );
  }
}
