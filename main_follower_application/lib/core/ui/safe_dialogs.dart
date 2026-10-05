import 'package:flutter/material.dart';

/// Shows a dialog with bottom safe-area padding so actions stay above the
/// system navigation bar / gesture area.
Future<T?> showSafeDialog<T>({
  required BuildContext context,
  required WidgetBuilder builder,
  bool barrierDismissible = true,
}) {
  return showDialog<T>(
    context: context,
    barrierDismissible: barrierDismissible,
    builder: (ctx) => SafeArea(
      minimum: const EdgeInsets.only(bottom: 12),
      child: builder(ctx),
    ),
  );
}

/// Modal bottom sheet with safe bottom inset.
Future<T?> showSafeModalBottomSheet<T>({
  required BuildContext context,
  required WidgetBuilder builder,
  bool isScrollControlled = false,
}) {
  return showModalBottomSheet<T>(
    context: context,
    isScrollControlled: isScrollControlled,
    builder: (ctx) => SafeArea(
      minimum: const EdgeInsets.only(bottom: 12),
      child: builder(ctx),
    ),
  );
}
