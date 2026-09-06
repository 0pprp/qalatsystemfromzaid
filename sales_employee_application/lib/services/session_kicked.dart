import 'package:flutter/material.dart';
import 'package:sales_employee_application/app_nav.dart';
import 'package:sales_employee_application/services/device_handover.dart';

class SessionKicked {
  static const message = 'تم تسجيل الدخول إلى هذا الحساب من جهاز آخر.';
  static bool _busy = false;

  static bool matches({int? statusCode, String? messageText, String? body}) {
    if (statusCode != 401) return false;
    final hay = '${messageText ?? ''}\n${body ?? ''}';
    return hay.contains('SESSION_REPLACED') || hay.contains('من جهاز آخر');
  }

  static Future<void> handle() async {
    if (_busy) return;
    _busy = true;
    try {
      await DeviceHandover.kickPreviousUser(endServerShift: false);
      final nav = AppNav.key.currentState;
      if (nav != null) {
        nav.pushNamedAndRemoveUntil('/login', (_) => false);
      }
      final ctx = AppNav.key.currentContext;
      if (ctx == null || !ctx.mounted) return;
      await showDialog<void>(
        context: ctx,
        barrierDismissible: false,
        builder: (dialogCtx) => AlertDialog(
          title: const Text('تنبيه'),
          content: const Text(message),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(dialogCtx),
              child: const Text('موافق'),
            ),
          ],
        ),
      );
    } finally {
      _busy = false;
    }
  }
}
