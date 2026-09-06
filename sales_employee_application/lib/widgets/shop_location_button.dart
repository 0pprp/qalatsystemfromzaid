import 'package:flutter/material.dart';
import 'package:sales_employee_application/utils/app_theme.dart';

class ShopLocationButton extends StatelessWidget {
  const ShopLocationButton({
    super.key,
    required this.loading,
    required this.captured,
    required this.onPressed,
  });

  final bool loading;
  final bool captured;
  final VoidCallback? onPressed;

  @override
  Widget build(BuildContext context) {
    return ElevatedButton(
      style: ElevatedButton.styleFrom(
        backgroundColor: captured ? const Color(0xFF2E7D32) : AppColors.darkGreen,
        foregroundColor: Colors.white,
        disabledBackgroundColor: captured ? const Color(0xFF2E7D32) : AppColors.darkGreen.withValues(alpha: 0.7),
        disabledForegroundColor: Colors.white,
      ),
      onPressed: loading ? null : onPressed,
      child: loading
          ? const SizedBox(
              height: 22,
              width: 22,
              child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white),
            )
          : Text(captured ? 'تم تحديد الموقع' : 'تحديد الموقع'),
    );
  }
}
