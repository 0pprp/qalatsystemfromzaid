import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../network/api_exception.dart';
import '../theme/app_theme.dart';

class EmptyState extends StatelessWidget {
  const EmptyState({
    super.key,
    required this.icon,
    required this.title,
    this.message,
    this.action,
    this.actionLabel,
  });

  final IconData icon;
  final String title;
  final String? message;
  final VoidCallback? action;
  final String? actionLabel;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Center(
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 32, vertical: 40),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Container(
              width: 96,
              height: 96,
              decoration: const BoxDecoration(color: AppColors.primaryLight, shape: BoxShape.circle),
              child: Icon(icon, size: 46, color: AppColors.primary),
            ),
            const SizedBox(height: 20),
            Text(title, style: theme.textTheme.titleMedium, textAlign: TextAlign.center),
            if (message != null) ...[
              const SizedBox(height: 8),
              Text(
                message!,
                textAlign: TextAlign.center,
                style: theme.textTheme.bodyMedium?.copyWith(color: AppColors.textMuted, height: 1.6),
              ),
            ],
            if (action != null && actionLabel != null) ...[
              const SizedBox(height: 20),
              FilledButton(onPressed: action, child: Text(actionLabel!)),
            ],
          ],
        ),
      ),
    );
  }
}

class ErrorView extends StatelessWidget {
  const ErrorView({super.key, required this.error, this.onRetry, this.compact = false});

  final Object error;
  final VoidCallback? onRetry;
  final bool compact;

  @override
  Widget build(BuildContext context) {
    final e = ApiException.from(error);
    final icon = e.isNetwork ? Icons.wifi_off_rounded : Icons.error_outline_rounded;
    if (compact) {
      return Padding(
        padding: const EdgeInsets.all(16),
        child: Row(
          children: [
            Icon(icon, color: AppColors.danger),
            const SizedBox(width: 10),
            Expanded(child: Text(e.message)),
            if (onRetry != null) TextButton(onPressed: onRetry, child: const Text('إعادة المحاولة')),
          ],
        ),
      );
    }
    return EmptyState(
      icon: icon,
      title: e.isNetwork ? 'لا يوجد اتصال' : 'حدث خطأ',
      message: e.message,
      action: onRetry,
      actionLabel: 'إعادة المحاولة',
    );
  }
}

/// Renders an [AsyncValue] with skeleton / error+retry / data states.
class AsyncView<T> extends StatelessWidget {
  const AsyncView({
    super.key,
    required this.value,
    required this.data,
    this.loading,
    this.onRetry,
  });

  final AsyncValue<T> value;
  final Widget Function(T data) data;
  final Widget? loading;
  final VoidCallback? onRetry;

  @override
  Widget build(BuildContext context) {
    // Keep showing previous data while refreshing.
    if (value.hasValue) return data(value.requireValue);
    if (value.hasError) return ErrorView(error: value.error!, onRetry: onRetry);
    return loading ?? const Center(child: CircularProgressIndicator());
  }
}

extension SnackX on BuildContext {
  void showSnack(String message, {bool error = false}) {
    final messenger = ScaffoldMessenger.maybeOf(this);
    if (messenger == null) return;
    messenger
      ..hideCurrentSnackBar()
      ..showSnackBar(SnackBar(
        content: Text(message),
        backgroundColor: error ? AppColors.danger : AppColors.text,
      ));
  }

  void showError(Object error) => showSnack(ApiException.from(error).message, error: true);
}
