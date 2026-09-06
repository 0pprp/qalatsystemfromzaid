import 'package:flutter/material.dart';
import 'package:sales_employee_application/tracking/tracking_channel.dart';
import 'package:sales_employee_application/utils/app_theme.dart';

class TappablePhone extends StatelessWidget {
  const TappablePhone(this.phone, {super.key, this.style});

  final String? phone;
  final TextStyle? style;

  static String? telNumber(String? raw) {
    final cleaned = (raw ?? '').replaceAll(RegExp(r'[^\d+]'), '');
    if (cleaned.isEmpty) return null;
    return cleaned;
  }

  static Future<void> confirmAndDial(BuildContext context, String? raw) async {
    final number = telNumber(raw);
    if (number == null) return;
    final ok = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('اتصال'),
        content: const Text('هل تريد الاتصال بهذا الرقم؟'),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx, false), child: const Text('إلغاء')),
          ElevatedButton(onPressed: () => Navigator.pop(ctx, true), child: const Text('اتصال')),
        ],
      ),
    );
    if (ok != true) return;
    await TrackingChannel.dial(number);
  }

  @override
  Widget build(BuildContext context) {
    final display = phone?.trim() ?? '';
    if (display.isEmpty) return const SizedBox.shrink();
    return InkWell(
      onTap: () => confirmAndDial(context, display),
      child: Text(
        display,
        style: style ??
            const TextStyle(
              color: AppColors.darkGreen,
              fontWeight: FontWeight.w600,
              decoration: TextDecoration.underline,
            ),
      ),
    );
  }
}
