import 'package:flutter/material.dart';

class AppColors {
  static const Color darkGreen = Color(0xFF006A64);
  static const Color lightGreen = Color(0xFF1D6B50);
  static const Color background = Color(0xFFF8F8F8);
  static const Color card = Colors.white;
  static const Color text = Color(0xFF00201E);
  static const Color muted = Color(0xFF6F797A);
  static const Color line = Color(0xFFE5EEEC);
  static const Color danger = Color(0xFF8A4A4A);
  static const Color warningSoft = Color(0xFFF4EFE6);
}

class AppSpacing {
  static const double xs = 6;
  static const double sm = 12;
  static const double md = 16;
  static const double lg = 24;
  static const double xl = 32;
}

class AppRadius {
  static const double sm = 10;
  static const double md = 16;
  static const double lg = 18;
}

class AppTypography {
  static const String fontFamily = 'ElMessiri';
}

class AppTheme {
  static const Color primaryColor = AppColors.darkGreen;
  static const Color secondaryColor = AppColors.lightGreen;
  static const Color accentColor = Color(0xFFB2DFDB);
  static const Color backgroundColor = AppColors.background;
  static const Color cardColor = AppColors.card;
  static const Color textColor = AppColors.text;
  static const Color subtitleColor = AppColors.muted;
  static const String fontFamily = AppTypography.fontFamily;

  static ThemeData get themeData {
    return ThemeData(
      useMaterial3: true,
      primaryColor: primaryColor,
      scaffoldBackgroundColor: backgroundColor,
      fontFamily: fontFamily,
      textTheme: const TextTheme(
        headlineSmall: TextStyle(fontSize: 24, fontWeight: FontWeight.w700, color: AppColors.text),
        titleLarge: TextStyle(fontSize: 20, fontWeight: FontWeight.w600, color: AppColors.text),
        titleMedium: TextStyle(fontSize: 16, fontWeight: FontWeight.w600, color: AppColors.text),
        bodyLarge: TextStyle(fontSize: 16, height: 1.5, color: AppColors.text),
        bodyMedium: TextStyle(fontSize: 14, height: 1.5, color: AppColors.text),
        bodySmall: TextStyle(fontSize: 12, height: 1.4, color: AppColors.muted),
      ),
      dividerTheme: const DividerThemeData(color: AppColors.line, space: 24),
      navigationBarTheme: NavigationBarThemeData(
        backgroundColor: Colors.white,
        surfaceTintColor: Colors.transparent,
        elevation: 0,
        height: 76,
        indicatorColor: const Color(0xFFCCE1E0),
        labelTextStyle: WidgetStateProperty.resolveWith((states) => TextStyle(
          fontFamily: fontFamily, fontSize: 12,
          fontWeight: states.contains(WidgetState.selected) ? FontWeight.w700 : FontWeight.w400,
          color: states.contains(WidgetState.selected) ? primaryColor : AppColors.muted,
        )),
      ),
      chipTheme: ChipThemeData(
        backgroundColor: const Color(0xFFF4FBF9),
        selectedColor: const Color(0xFFCCE1E0),
        side: BorderSide.none,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
        labelStyle: const TextStyle(fontFamily: fontFamily, color: primaryColor),
      ),
      colorScheme: ColorScheme.fromSeed(
        seedColor: primaryColor,
        primary: primaryColor,
        secondary: secondaryColor,
        surface: cardColor,
      ),
      appBarTheme: const AppBarTheme(
        backgroundColor: backgroundColor,
        surfaceTintColor: Colors.transparent,
        scrolledUnderElevation: 0,
        elevation: 0,
        centerTitle: true,
        titleTextStyle: TextStyle(
          fontFamily: fontFamily,
          color: textColor,
          fontSize: 20,
          fontWeight: FontWeight.bold,
        ),
        iconTheme: IconThemeData(color: textColor),
      ),
      elevatedButtonTheme: ElevatedButtonThemeData(
        style: ElevatedButton.styleFrom(
          backgroundColor: primaryColor,
          foregroundColor: Colors.white,
          elevation: 0,
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(AppRadius.md),
          ),
          padding: const EdgeInsets.symmetric(vertical: 14, horizontal: 24),
          textStyle: const TextStyle(
            fontFamily: fontFamily,
            fontWeight: FontWeight.bold,
            fontSize: 16,
          ),
        ),
      ),
      cardTheme: CardThemeData(
        color: cardColor,
        elevation: 0,
        margin: const EdgeInsets.only(bottom: AppSpacing.sm),
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(AppRadius.md),
          side: const BorderSide(color: AppColors.line),
        ),
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: Colors.white,
        floatingLabelBehavior: FloatingLabelBehavior.auto,
        alignLabelWithHint: true,
        contentPadding: const EdgeInsets.fromLTRB(16, 20, 16, 16),
        border: OutlineInputBorder(
          borderRadius: BorderRadius.circular(AppRadius.md),
          borderSide: const BorderSide(color: AppColors.line),
        ),
        enabledBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(AppRadius.md),
          borderSide: const BorderSide(color: AppColors.line),
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(AppRadius.md),
          borderSide: const BorderSide(color: primaryColor, width: 1.5),
        ),
        hintStyle: const TextStyle(fontFamily: fontFamily, color: AppColors.muted),
      ),
    );
  }
}
