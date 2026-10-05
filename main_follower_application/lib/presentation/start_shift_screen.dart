import 'package:flutter/material.dart';
import 'package:main_follower_application/presentation/missions_screen.dart';
import 'package:main_follower_application/tracking/shift_tracking_controller.dart';

/// Blocks the app until the main follower starts an active shift.
class StartShiftScreen extends StatefulWidget {
  const StartShiftScreen({super.key});

  @override
  State<StartShiftScreen> createState() => _StartShiftScreenState();
}

class _StartShiftScreenState extends State<StartShiftScreen> {
  bool _busy = false;
  String? _error;

  Future<void> _start() async {
    setState(() {
      _busy = true;
      _error = null;
    });
    final tracking = ShiftTrackingController.instance;
    final shift = await tracking.startShift();
    if (!mounted) return;
    if (shift == null || !shift.isActive) {
      setState(() {
        _busy = false;
        _error = tracking.lastError ?? 'تعذر بدء الدوام';
      });
      return;
    }
    Navigator.of(context).pushReplacement(
      MaterialPageRoute(builder: (_) => const MissionsScreen()),
    );
  }

  @override
  Widget build(BuildContext context) {
    return PopScope(
      canPop: false,
      child: Scaffold(
        body: SafeArea(
          child: Padding(
            padding: const EdgeInsets.all(28),
            child: Column(
              mainAxisAlignment: MainAxisAlignment.center,
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                const Icon(Icons.work_history, size: 72, color: Color(0xFF0F3D3E)),
                const SizedBox(height: 20),
                const Text(
                  'بدء الدوام',
                  textAlign: TextAlign.center,
                  style: TextStyle(
                    fontSize: 26,
                    fontWeight: FontWeight.bold,
                    color: Color(0xFF0F3D3E),
                  ),
                ),
                const SizedBox(height: 12),
                const Text(
                  'لا يمكن الدخول إلى المهام قبل بدء الدوام.\n'
                  'بعد البدء يُرسل موقعك كل 10 دقائق ليظهر على خريطة المدير المفوض.',
                  textAlign: TextAlign.center,
                  style: TextStyle(height: 1.5, color: Colors.black54),
                ),
                if (_error != null) ...[
                  const SizedBox(height: 16),
                  Text(_error!,
                      textAlign: TextAlign.center,
                      style: const TextStyle(color: Colors.red)),
                ],
                const SizedBox(height: 28),
                FilledButton(
                  onPressed: _busy ? null : _start,
                  style: FilledButton.styleFrom(
                    backgroundColor: const Color(0xFF0F3D3E),
                    padding: const EdgeInsets.symmetric(vertical: 14),
                  ),
                  child: _busy
                      ? const SizedBox(
                          height: 22,
                          width: 22,
                          child: CircularProgressIndicator(
                            strokeWidth: 2,
                            color: Colors.white,
                          ),
                        )
                      : const Text('بدء الدوام الآن'),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
