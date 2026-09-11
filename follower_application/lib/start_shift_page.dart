import 'package:follower_application/tracking/follower_shift_debug.dart';
import 'package:follower_application/tracking/shift_tracking_controller.dart';
import 'package:follower_application/ui/app_safe_scaffold.dart';
import 'package:follower_application/utils/AppTheme.dart';
import 'package:flutter/material.dart';

/// Sole entry into the operational app when no Active Shift exists.
class StartShiftPage extends StatefulWidget {
  const StartShiftPage({super.key});

  @override
  State<StartShiftPage> createState() => _StartShiftPageState();
}

class _StartShiftPageState extends State<StartShiftPage> {
  bool _busy = false;
  String _error = '';

  Future<void> _start() async {
    setState(() {
      _busy = true;
      _error = '';
    });
    try {
      final tracking = TrackingRuntime.instance ??= ShiftTrackingController();
      final shift = await tracking.startShiftFlow();
      if (!mounted) return;
      if (shift == null || !shift.isActive) {
        setState(() {
          _busy = false;
          _error = tracking.lastError ?? FollowerShiftDebug.generic;
        });
        return;
      }
      Navigator.pushNamedAndRemoveUntil(context, '/HomePage', (r) => false);
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _busy = false;
        _error = FollowerShiftDebug.apiFailure(e);
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    return PopScope(
      canPop: false,
      child: Directionality(
        textDirection: TextDirection.rtl,
        child: AppSafeScaffold(
          backgroundColor: AppTheme.backgroundColor,
          body: Center(
            child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: 28),
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  const Icon(Icons.work_history,
                      size: 72, color: AppTheme.primaryColor),
                  const SizedBox(height: 20),
                  const Text(
                    'بدء الدوام',
                    style: TextStyle(
                      fontFamily: 'Cairo',
                      fontSize: 26,
                      fontWeight: FontWeight.bold,
                      color: AppTheme.primaryColor,
                    ),
                  ),
                  const SizedBox(height: 12),
                  Text(
                    'يجب بدء الدوام قبل الدخول إلى التطبيق.\nتتبع الموقع يبدأ بعد 10 دقائق من بداية الدوام.',
                    textAlign: TextAlign.center,
                    style: TextStyle(
                      fontFamily: 'Cairo',
                      color: Colors.grey[700],
                      height: 1.5,
                    ),
                  ),
                  if (_error.isNotEmpty) ...[
                    const SizedBox(height: 16),
                    Text(
                      _error,
                      textAlign: TextAlign.center,
                      style: const TextStyle(
                        fontFamily: 'Cairo',
                        color: Colors.red,
                      ),
                    ),
                  ],
                  const SizedBox(height: 32),
                  SizedBox(
                    width: double.infinity,
                    height: 54,
                    child: ElevatedButton.icon(
                      onPressed: _busy ? null : _start,
                      icon: _busy
                          ? const SizedBox(
                              width: 22,
                              height: 22,
                              child: CircularProgressIndicator(
                                strokeWidth: 2,
                                color: Colors.white,
                              ),
                            )
                          : const Icon(Icons.play_arrow, color: Colors.white),
                      label: Text(
                        _busy ? 'جاري البدء...' : 'بدء الدوام',
                        style: const TextStyle(
                          fontFamily: 'Cairo',
                          color: Colors.white,
                          fontSize: 18,
                          fontWeight: FontWeight.bold,
                        ),
                      ),
                      style: ElevatedButton.styleFrom(
                        backgroundColor: AppTheme.primaryColor,
                        shape: RoundedRectangleBorder(
                          borderRadius: BorderRadius.circular(14),
                        ),
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}
