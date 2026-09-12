import 'package:flutter/material.dart';

class AppColors {
  static const Color darkGreen = Color(0xFF006D5B);
  static const Color lightGreen = Color(0xFF26A69A);
  static const Color background = Color(0xFFF7F8F4);
  static const Color card = Colors.white;
  static const Color text = Color(0xFF2C3332);
  static const Color muted = Color(0xFF6B7674);
  static const Color line = Color(0xFFE6EBE8);
  static const Color danger = Color(0xFFC62828);
  static const Color pendingBlue = Color(0xFF1565C0);
  static const Color holdAmber = Color(0xFFF57C00);
  static const Color readyGreen = Color(0xFF2E7D32);
  static const Color rejectedRed = Color(0xFFC62828);
}

class AppSpacing {
  static const double xs = 6;
  static const double sm = 12;
  static const double md = 16;
  static const double lg = 24;
}

class AppRadius {
  static const double sm = 10;
  static const double md = 14;
  static const double lg = 18;
}

class AppTheme {
  static const Color primary = AppColors.darkGreen;
  static const Color surface = AppColors.background;

  static ThemeData get light => ThemeData(
        useMaterial3: true,
        fontFamily: 'Cairo',
        colorScheme: ColorScheme.fromSeed(seedColor: primary, primary: primary),
        scaffoldBackgroundColor: surface,
        cardTheme: CardThemeData(
          color: AppColors.card,
          elevation: 1.5,
          shadowColor: Colors.black26,
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(AppRadius.md)),
          margin: EdgeInsets.zero,
        ),
        appBarTheme: const AppBarTheme(
          backgroundColor: primary,
          foregroundColor: Colors.white,
          centerTitle: true,
          titleTextStyle: TextStyle(fontFamily: 'Cairo', fontSize: 18, fontWeight: FontWeight.bold, color: Colors.white),
        ),
      );
}

class FilterStatuses {
  static const pending = 'PendingFilter';
  static const onHold = 'OnHold';
  static const ready = 'ReadyForSale';
  static const rejected = 'Rejected';

  static const bins = [
    (status: pending, title: 'طلبات البيع', icon: Icons.assignment_outlined, color: AppColors.pendingBlue),
    (status: onHold, title: 'معلق', icon: Icons.pause_circle_outline, color: AppColors.holdAmber),
    (status: ready, title: 'جاهز للبيع', icon: Icons.storefront_outlined, color: AppColors.readyGreen),
    (status: rejected, title: 'مرفوض', icon: Icons.cancel_outlined, color: AppColors.rejectedRed),
  ];
}
