import 'dart:async';

import 'package:audioplayers/audioplayers.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

/// One-shot success feedback for a user-initiated local payment save.
///
/// Short center flash + payment ding — like electronic payment apps.
/// Never play on batch upload / retry / WorkManager sync.
class PaymentSuccessFeedback {
  PaymentSuccessFeedback._();

  static bool _playing = false;
  static final AudioPlayer _player = AudioPlayer();

  /// Play once after durable local save succeeds (before any server sync).
  static Future<void> playLocalSaveSuccess(BuildContext context) async {
    if (_playing) return;
    _playing = true;
    try {
      unawaited(_playDing());
      try {
        await HapticFeedback.mediumImpact();
      } catch (_) {
        // Vibration must never block payment completion.
      }
      if (!context.mounted) return;
      await _showCenterFlash(context);
    } finally {
      _playing = false;
    }
  }

  static Future<void> _playDing() async {
    try {
      await _player.stop();
      await _player.play(AssetSource('sounds/payment_ding.wav'));
    } catch (_) {
      try {
        await SystemSound.play(SystemSoundType.click);
      } catch (_) {}
    }
  }

  static Future<void> _showCenterFlash(BuildContext context) async {
    final overlay = Overlay.maybeOf(context);
    if (overlay == null) return;

    late OverlayEntry entry;
    entry = OverlayEntry(
      builder: (ctx) => IgnorePointer(
        child: Material(
          color: Colors.transparent,
          child: Center(
            child: TweenAnimationBuilder<double>(
              tween: Tween(begin: 0.85, end: 1),
              duration: const Duration(milliseconds: 120),
              curve: Curves.easeOut,
              builder: (context, scale, child) => Transform.scale(
                scale: scale,
                child: child,
              ),
              child: Container(
                padding:
                    const EdgeInsets.symmetric(horizontal: 28, vertical: 22),
                decoration: BoxDecoration(
                  color: Colors.green.shade700.withValues(alpha: 0.95),
                  borderRadius: BorderRadius.circular(20),
                  boxShadow: [
                    BoxShadow(
                      color: Colors.black.withValues(alpha: 0.25),
                      blurRadius: 18,
                      offset: const Offset(0, 8),
                    ),
                  ],
                ),
                child: const Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Icon(Icons.check_circle, color: Colors.white, size: 44),
                    SizedBox(height: 8),
                    Text(
                      'تم التسديد',
                      style: TextStyle(
                        fontFamily: 'Cairo',
                        color: Colors.white,
                        fontWeight: FontWeight.w800,
                        fontSize: 18,
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );

    overlay.insert(entry);
    await Future<void>.delayed(const Duration(milliseconds: 420));
    entry.remove();
  }

  /// @deprecated Prefer [playLocalSaveSuccess] — kept for callers that still
  /// check server ACK in the same session (no extra sound on retry paths).
  static Future<void> playServerSuccess(BuildContext context) =>
      playLocalSaveSuccess(context);

  /// Informational only — no sound (offline queue).
  static void showQueued(BuildContext context, {bool beforeSyncGate = false}) {
    ScaffoldMessenger.of(context).hideCurrentSnackBar();
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text(
          beforeSyncGate
              ? 'تم حفظ التسديد وسيُرفع أونلاين فوراً عند توفر الإنترنت'
              : 'تم حفظ التسديد وسيتم رفعه أونلاين عند توفر الإنترنت',
          style: const TextStyle(fontFamily: 'Cairo'),
        ),
        behavior: SnackBarBehavior.floating,
        duration: const Duration(seconds: 3),
      ),
    );
  }
}
