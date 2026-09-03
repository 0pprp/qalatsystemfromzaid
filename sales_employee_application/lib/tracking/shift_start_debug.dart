import 'package:flutter/foundation.dart';
import 'package:sales_employee_application/config/app_env.dart';
import 'package:sales_employee_application/services/api_client.dart';

class ShiftStartDebug {
  static const generic = 'تعذر بدء الدوام';

  static bool get showDetail => kDebugMode || AppEnv.isDemo;

  static bool get isAndroidDevice =>
      !kIsWeb && defaultTargetPlatform == TargetPlatform.android;

  static void log(String message) {
    if (!kDebugMode) return;
    debugPrint('SHIFT START: ${sanitize(message)}');
  }

  static void logError(String stage, Object error, [StackTrace? stack]) {
    if (!kDebugMode) return;
    debugPrint('SHIFT START ERROR at $stage: ${error.runtimeType}');
    debugPrint('SHIFT START ERROR message: ${sanitize(error.toString())}');
    if (stack != null) {
      debugPrint('SHIFT START ERROR stack:\n$stack');
    }
  }

  static String sanitize(String text) {
    var value = text;
    value = value.replaceAll(RegExp(r'Bearer\s+\S+', caseSensitive: false), '[redacted]');
    value = value.replaceAll(
      RegExp(r'eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+'),
      '[redacted]',
    );
    value = value.replaceAll(RegExp(r'(password|token|secret)\s*[:=]\s*\S+', caseSensitive: false), '[redacted]');
    return value;
  }

  static String _clip(String text) {
    if (text.length <= 400) return text;
    return '${text.substring(0, 400)}…';
  }

  static String apiFailure(Object error) {
    if (!showDetail) return generic;
    if (error is ApiException) {
      final code = error.statusCode?.toString() ?? '-';
      final raw = (error.body != null && error.body!.trim().isNotEmpty)
          ? error.body!.trim()
          : error.message;
      final body = sanitize(_clip(raw));
      return '$generic: $code - $body (${error.runtimeType})';
    }
    return '$generic: ${error.runtimeType} - ${sanitize(error.toString())}';
  }

  static String trackingFailure(Object? error) {
    if (!showDetail) return generic;
    if (error == null) {
      return '$generic: tracking service - startForegroundService failed';
    }
    return '$generic: tracking service - ${error.runtimeType} - ${sanitize(error.toString())}';
  }
}
