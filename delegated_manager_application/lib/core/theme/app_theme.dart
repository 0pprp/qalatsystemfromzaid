import 'package:flutter/material.dart';

class AppColors {
  static const Color primary = Color(0xFF1B4965);
  static const Color primaryDark = Color(0xFF123449);
  static const Color accent = Color(0xFF2A7F9E);
  static const Color background = Color(0xFFF4F6F8);
  static const Color card = Colors.white;
  static const Color text = Color(0xFF1F2A33);
  static const Color muted = Color(0xFF6B7A87);
  static const Color line = Color(0xFFE2E8ED);
  static const Color pending = Color(0xFF1565C0);
  static const Color approved = Color(0xFF2E7D32);
  static const Color rejected = Color(0xFFC62828);
  static const Color cancelled = Color(0xFF757575);
  static const Color warning = Color(0xFFF57C00);
  static const Color unread = Color(0xFFE8F1F7);
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
  static ThemeData get light => ThemeData(
        useMaterial3: true,
        fontFamily: 'Cairo',
        colorScheme: ColorScheme.fromSeed(
          seedColor: AppColors.primary,
          primary: AppColors.primary,
          surface: AppColors.background,
        ),
        scaffoldBackgroundColor: AppColors.background,
        appBarTheme: const AppBarTheme(
          backgroundColor: AppColors.primary,
          foregroundColor: Colors.white,
          centerTitle: true,
          elevation: 0,
          titleTextStyle: TextStyle(
            fontFamily: 'Cairo',
            fontSize: 18,
            fontWeight: FontWeight.bold,
            color: Colors.white,
          ),
        ),
        cardTheme: CardThemeData(
          color: AppColors.card,
          elevation: 1,
          shadowColor: Colors.black26,
          margin: EdgeInsets.zero,
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(AppRadius.md),
          ),
        ),
        dividerTheme: const DividerThemeData(
          color: AppColors.line,
          thickness: 1,
          space: 1,
        ),
        bottomNavigationBarTheme: const BottomNavigationBarThemeData(
          backgroundColor: Colors.white,
          selectedItemColor: AppColors.primary,
          unselectedItemColor: AppColors.muted,
          type: BottomNavigationBarType.fixed,
          selectedLabelStyle:
              TextStyle(fontFamily: 'Cairo', fontWeight: FontWeight.bold),
          unselectedLabelStyle: TextStyle(fontFamily: 'Cairo'),
        ),
        filledButtonTheme: FilledButtonThemeData(
          style: FilledButton.styleFrom(
            minimumSize: const Size.fromHeight(48),
            shape: RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(AppRadius.sm),
            ),
            textStyle: const TextStyle(
              fontFamily: 'Cairo',
              fontSize: 16,
              fontWeight: FontWeight.bold,
            ),
          ),
        ),
        inputDecorationTheme: InputDecorationTheme(
          filled: true,
          fillColor: Colors.white,
          contentPadding: const EdgeInsets.symmetric(
            horizontal: AppSpacing.md,
            vertical: AppSpacing.sm,
          ),
          border: OutlineInputBorder(
            borderRadius: BorderRadius.circular(AppRadius.sm),
            borderSide: const BorderSide(color: AppColors.line),
          ),
          enabledBorder: OutlineInputBorder(
            borderRadius: BorderRadius.circular(AppRadius.sm),
            borderSide: const BorderSide(color: AppColors.line),
          ),
        ),
      );
}

/// Server status vocabulary shared with `BE_SalesEmployee`.
class ExceptionStatuses {
  static const String pending = 'Pending';
  static const String approved = 'Approved';
  static const String rejected = 'Rejected';
  static const String cancelled = 'Cancelled';

  static const List<String> all = [pending, approved, rejected, cancelled];

  static String arabic(String? status) {
    switch (status) {
      case pending:
        return 'قيد الانتظار';
      case approved:
        return 'موافق عليه';
      case rejected:
        return 'مرفوض';
      case cancelled:
        return 'ملغى';
    }
    return status ?? '-';
  }

  static Color color(String? status) {
    switch (status) {
      case pending:
        return AppColors.pending;
      case approved:
        return AppColors.approved;
      case rejected:
        return AppColors.rejected;
      case cancelled:
        return AppColors.cancelled;
    }
    return AppColors.muted;
  }
}

class ComplaintStatuses {
  static const String unread = 'Unread';
  static const String read = 'Read';

  static String arabic(String? status) =>
      status == read ? 'مقروءة' : 'غير مقروءة';
}
