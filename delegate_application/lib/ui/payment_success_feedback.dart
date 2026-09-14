import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

/// One-shot success feedback for a user-initiated payment action.
class PaymentSuccessFeedback {
  PaymentSuccessFeedback._();

  static bool _playing = false;

  /// Server-confirmed success (or same-session sync completed after user action).
  static Future<void> playServerSuccess(BuildContext context) async {
    if (_playing) return;
    _playing = true;
    try {
      await SystemSound.play(SystemSoundType.click);
      await HapticFeedback.mediumImpact();
      if (!context.mounted) return;
      ScaffoldMessenger.of(context).hideCurrentSnackBar();
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: const Row(
            children: [
              Icon(Icons.check_circle, color: Colors.white),
              SizedBox(width: 10),
              Expanded(
                child: Text(
                  'تم التسديد بنجاح',
                  style: TextStyle(fontFamily: 'Cairo', fontWeight: FontWeight.bold),
                ),
              ),
            ],
          ),
          backgroundColor: Colors.green.shade700,
          behavior: SnackBarBehavior.floating,
          duration: const Duration(seconds: 3),
        ),
      );
    } finally {
      _playing = false;
    }
  }

  /// Offline queue only — no success sound.
  static void showQueued(BuildContext context) {
    ScaffoldMessenger.of(context).hideCurrentSnackBar();
    ScaffoldMessenger.of(context).showSnackBar(
      const SnackBar(
        content: Text(
          'تم حفظ التسديد وسيتم رفعه عند توفر الإنترنت',
          style: TextStyle(fontFamily: 'Cairo'),
        ),
        behavior: SnackBarBehavior.floating,
        duration: Duration(seconds: 3),
      ),
    );
  }
}
