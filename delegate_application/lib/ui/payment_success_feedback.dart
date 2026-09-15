import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

/// One-shot success feedback for a user-initiated local payment save.
///
/// Sound means: "تم تسجيل تسديدة الزبون بنجاح على الجهاز".
/// Never play on batch upload / retry / WorkManager sync.
class PaymentSuccessFeedback {
  PaymentSuccessFeedback._();

  static bool _playing = false;

  /// Play once after durable local save succeeds (before any server sync).
  static Future<void> playLocalSaveSuccess(BuildContext context) async {
    if (_playing) return;
    _playing = true;
    try {
      await SystemSound.play(SystemSoundType.click);
      try {
        await HapticFeedback.lightImpact();
      } catch (_) {
        // Vibration must never block payment completion.
      }
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
                  'تم تسجيل تسديدة الزبون بنجاح على الجهاز',
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

  /// @deprecated Prefer [playLocalSaveSuccess] — kept for callers that still
  /// check server ACK in the same session (no extra sound on retry paths).
  static Future<void> playServerSuccess(BuildContext context) =>
      playLocalSaveSuccess(context);

  /// Informational only — no sound (gate closed / offline queue).
  static void showQueued(BuildContext context, {bool beforeSyncGate = false}) {
    ScaffoldMessenger.of(context).hideCurrentSnackBar();
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text(
          beforeSyncGate
              ? 'تم حفظ التسديد وسيُرفع تلقائياً بعد الساعة 4 مساءً'
              : 'تم حفظ التسديد وسيتم رفعه عند توفر الإنترنت',
          style: const TextStyle(fontFamily: 'Cairo'),
        ),
        behavior: SnackBarBehavior.floating,
        duration: const Duration(seconds: 3),
      ),
    );
  }
}
