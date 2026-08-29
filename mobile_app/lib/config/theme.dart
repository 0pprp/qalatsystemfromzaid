import 'package:flutter/material.dart';

/// Spacing constants for consistent layout across the app.
class AppSpacing {
  AppSpacing._();
  static const double xs = 4;
  static const double sm = 8;
  static const double md = 16;
  static const double lg = 24;
  static const double xl = 32;
}

class AppTheme {
  AppTheme._();

  // ── Brand Colors ──────────────────────────────────
  static const Color primaryColor = Color(0xFF1e5799);
  static const Color primaryLight = Color(0xFF207cca);
  static const Color accentColor = Color(0xFF4A00E0);

  // ── Semantic Colors ───────────────────────────────
  static const Color successColor = Color(0xFF11998e);
  static const Color successLight = Color(0xFF38ef7d);
  static const Color warningColor = Color(0xFFF2994A);
  static const Color warningLight = Color(0xFFF2C94C);
  static const Color errorColor = Color(0xFFFF416C);
  static const Color errorLight = Color(0xFFFF4B2B);
  static const Color infoColor = Color(0xFF36D1DC);
  static const Color infoLight = Color(0xFF5B86E5);

  // Solid shades for chips and badges
  static const Color purpleSolid = Color(0xFF8E2DE2);
  static const Color slateSolid = Color(0xFF475569);

  // ── Gradients ─────────────────────────────────────
  static const LinearGradient primaryGradient = LinearGradient(
    colors: [Color(0xFF1e5799), Color(0xFF207cca)],
    begin: AlignmentDirectional.topStart,
    end: AlignmentDirectional.bottomEnd,
  );

  static const LinearGradient successGradient = LinearGradient(
    colors: [Color(0xFF11998e), Color(0xFF38ef7d)],
    begin: AlignmentDirectional.topStart,
    end: AlignmentDirectional.bottomEnd,
  );

  static const LinearGradient purpleGradient = LinearGradient(
    colors: [Color(0xFF8E2DE2), Color(0xFF4A00E0)],
    begin: AlignmentDirectional.topStart,
    end: AlignmentDirectional.bottomEnd,
  );

  static const LinearGradient warningGradient = LinearGradient(
    colors: [Color(0xFFF2994A), Color(0xFFF2C94C)],
    begin: AlignmentDirectional.topStart,
    end: AlignmentDirectional.bottomEnd,
  );

  static const LinearGradient infoGradient = LinearGradient(
    colors: [Color(0xFF36D1DC), Color(0xFF5B86E5)],
    begin: AlignmentDirectional.topStart,
    end: AlignmentDirectional.bottomEnd,
  );

  static const LinearGradient errorGradient = LinearGradient(
    colors: [Color(0xFFFF416C), Color(0xFFFF4B2B)],
    begin: AlignmentDirectional.topStart,
    end: AlignmentDirectional.bottomEnd,
  );

  static const LinearGradient secondaryGradient = LinearGradient(
    colors: [Color(0xFF475569), Color(0xFF64748b)],
    begin: AlignmentDirectional.topStart,
    end: AlignmentDirectional.bottomEnd,
  );

  static LinearGradient getGradient(String color) {
    switch (color) {
      case 'primary':
        return primaryGradient; // Fixed: was returning purpleGradient
      case 'success':
        return successGradient;
      case 'warning':
        return warningGradient;
      case 'error':
        return errorGradient;
      case 'info':
        return infoGradient;
      case 'secondary':
        return secondaryGradient;
      case 'purple':
        return purpleGradient;
      default:
        return primaryGradient;
    }
  }

  /// Returns the two gradient colours for the named colour key.
  static List<Color> gradientColors(String color) {
    switch (color) {
      case 'primary':
        return const [primaryColor, primaryLight];
      case 'success':
        return const [successColor, successLight];
      case 'warning':
        return const [warningColor, warningLight];
      case 'error':
        return const [errorColor, errorLight];
      case 'info':
        return const [infoColor, infoLight];
      case 'secondary':
        return const [slateSolid, Color(0xFF64748b)];
      case 'purple':
        return const [purpleSolid, accentColor];
      default:
        return const [primaryColor, primaryLight];
    }
  }

  // ── Cairo Text Theme ──────────────────────────────
  static const _cairo = 'Cairo';

  static TextTheme get _cairoTextTheme {
    return const TextTheme(
      displayLarge: TextStyle(fontFamily: _cairo, fontSize: 28, fontWeight: FontWeight.bold, letterSpacing: -0.5),
      displayMedium: TextStyle(fontFamily: _cairo, fontSize: 24, fontWeight: FontWeight.bold),
      displaySmall: TextStyle(fontFamily: _cairo, fontSize: 22, fontWeight: FontWeight.w700),
      headlineMedium: TextStyle(fontFamily: _cairo, fontSize: 20, fontWeight: FontWeight.w600),
      headlineSmall: TextStyle(fontFamily: _cairo, fontSize: 18, fontWeight: FontWeight.w600),
      titleLarge: TextStyle(fontFamily: _cairo, fontSize: 18, fontWeight: FontWeight.w600),
      titleMedium: TextStyle(fontFamily: _cairo, fontSize: 16, fontWeight: FontWeight.w500),
      titleSmall: TextStyle(fontFamily: _cairo, fontSize: 14, fontWeight: FontWeight.w500),
      bodyLarge: TextStyle(fontFamily: _cairo, fontSize: 16, fontWeight: FontWeight.w400),
      bodyMedium: TextStyle(fontFamily: _cairo, fontSize: 14, fontWeight: FontWeight.w400),
      bodySmall: TextStyle(fontFamily: _cairo, fontSize: 12, fontWeight: FontWeight.w400),
      labelLarge: TextStyle(fontFamily: _cairo, fontSize: 14, fontWeight: FontWeight.w600),
      labelMedium: TextStyle(fontFamily: _cairo, fontSize: 12, fontWeight: FontWeight.w500),
      labelSmall: TextStyle(fontFamily: _cairo, fontSize: 11, fontWeight: FontWeight.w500),
    );
  }

  // ── Light Theme ───────────────────────────────────
  static ThemeData get lightTheme {
    final colorScheme = const ColorScheme.light(
      primary: primaryColor,
      secondary: accentColor,
      surface: Color(0xFFF8FAFC),
      onPrimary: Colors.white,
      onSecondary: Colors.white,
      onSurface: Color(0xFF1E293B),
      outline: Color(0xFFE2E8F0),
      error: errorColor,
      onError: Colors.white,
    );

    return ThemeData(
      useMaterial3: true,
      brightness: Brightness.light,
      fontFamily: _cairo,
      colorScheme: colorScheme,
      textTheme: _cairoTextTheme,
      scaffoldBackgroundColor: const Color(0xFFF1F5F9),

      // ── AppBar ──
      appBarTheme: const AppBarTheme(
        backgroundColor: primaryColor,
        foregroundColor: Colors.white,
        elevation: 0,
        centerTitle: true,
        titleTextStyle: TextStyle(fontFamily: _cairo, fontSize: 18, fontWeight: FontWeight.w600, color: Colors.white),
      ),

      // ── Cards ──
      cardTheme: CardThemeData(
        elevation: 2,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
        clipBehavior: Clip.antiAlias,
        margin: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
      ),

      // ── Inputs ──
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: Colors.white,
        border: OutlineInputBorder(borderRadius: BorderRadius.circular(12), borderSide: const BorderSide(color: Color(0xFFE2E8F0))),
        enabledBorder: OutlineInputBorder(borderRadius: BorderRadius.circular(12), borderSide: const BorderSide(color: Color(0xFFE2E8F0))),
        focusedBorder: OutlineInputBorder(borderRadius: BorderRadius.circular(12), borderSide: const BorderSide(color: primaryColor, width: 2)),
        errorBorder: OutlineInputBorder(borderRadius: BorderRadius.circular(12), borderSide: const BorderSide(color: errorColor)),
        focusedErrorBorder: OutlineInputBorder(borderRadius: BorderRadius.circular(12), borderSide: const BorderSide(color: errorColor, width: 2)),
        contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
        hintStyle: TextStyle(color: Colors.grey[400], fontSize: 14, fontFamily: _cairo),
      ),

      // ── Buttons ──
      elevatedButtonTheme: ElevatedButtonThemeData(
        style: ElevatedButton.styleFrom(
          backgroundColor: primaryColor,
          foregroundColor: Colors.white,
          elevation: 2,
          padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 14),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
          textStyle: const TextStyle(fontFamily: _cairo, fontSize: 16, fontWeight: FontWeight.w600),
        ),
      ),
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          foregroundColor: primaryColor,
          side: const BorderSide(color: primaryColor),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
          padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 12),
        ),
      ),
      textButtonTheme: TextButtonThemeData(
        style: TextButton.styleFrom(foregroundColor: primaryColor),
      ),

      // ── Chips ──
      chipTheme: ChipThemeData(
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
        labelStyle: const TextStyle(fontFamily: _cairo, fontSize: 12),
        backgroundColor: primaryColor.withAlpha(20),
        selectedColor: primaryColor.withAlpha(38),
      ),

      // ── Dividers ──
      dividerTheme: const DividerThemeData(color: Color(0xFFE2E8F0), thickness: 1),

      // ── Bottom Nav ──
      bottomNavigationBarTheme: const BottomNavigationBarThemeData(
        backgroundColor: Colors.white,
        selectedItemColor: primaryColor,
        unselectedItemColor: Color(0xFF94A3B8),
        type: BottomNavigationBarType.fixed,
        elevation: 8,
        selectedLabelStyle: TextStyle(fontFamily: _cairo, fontSize: 11),
        unselectedLabelStyle: TextStyle(fontFamily: _cairo, fontSize: 11),
      ),

      // ── SnackBar ──
      snackBarTheme: SnackBarThemeData(
        behavior: SnackBarBehavior.floating,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
        contentTextStyle: const TextStyle(fontFamily: _cairo, fontSize: 14),
      ),

      // ── Dialog ──
      dialogTheme: DialogThemeData(
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20)),
        titleTextStyle: const TextStyle(fontFamily: _cairo, fontSize: 18, fontWeight: FontWeight.w600),
        contentTextStyle: const TextStyle(fontFamily: _cairo, fontSize: 14),
      ),

      // ── Floating Action Button ──
      floatingActionButtonTheme: const FloatingActionButtonThemeData(
        backgroundColor: primaryColor,
        foregroundColor: Colors.white,
        elevation: 4,
        shape: CircleBorder(),
      ),

      // ── Tab Bar ──
      tabBarTheme: const TabBarThemeData(
        labelStyle: TextStyle(fontFamily: _cairo, fontSize: 14, fontWeight: FontWeight.w600),
        unselectedLabelStyle: TextStyle(fontFamily: _cairo, fontSize: 14),
      ),

      // ── DataTable ──
      dataTableTheme: DataTableThemeData(
        headingTextStyle: const TextStyle(fontFamily: _cairo, fontWeight: FontWeight.bold, fontSize: 12, color: primaryColor),
        dataTextStyle: const TextStyle(fontFamily: _cairo, fontSize: 12),
        decoration: BoxDecoration(borderRadius: BorderRadius.circular(16)),
      ),

      // ── Date Picker ──
      datePickerTheme: DatePickerThemeData(
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20)),
        headerBackgroundColor: primaryColor,
        headerForegroundColor: Colors.white,
        todayForegroundColor: WidgetStateColor.resolveWith((states) {
          if (states.contains(WidgetState.selected)) return Colors.white;
          return primaryColor;
        }),
      ),
    );
  }

  // ── Dark Theme ────────────────────────────────────
  static ThemeData get darkTheme {
    final colorScheme = const ColorScheme.dark(
      primary: Color(0xFF60A5FA),
      secondary: Color(0xFFA78BFA),
      surface: Color(0xFF1E293B),
      onPrimary: Colors.white,
      onSecondary: Colors.white,
      onSurface: Color(0xFFF1F5F9),
      outline: Color(0xFF334155),
      error: Color(0xFFFF6B6B),
      onError: Color(0xFF1E293B),
    );

    return ThemeData(
      useMaterial3: true,
      brightness: Brightness.dark,
      fontFamily: _cairo,
      colorScheme: colorScheme,
      textTheme: _cairoTextTheme,
      scaffoldBackgroundColor: const Color(0xFF0F172A),

      // ── AppBar ──
      appBarTheme: const AppBarTheme(
        backgroundColor: Color(0xFF1E293B),
        foregroundColor: Colors.white,
        elevation: 0,
        centerTitle: true,
        titleTextStyle: TextStyle(fontFamily: _cairo, fontSize: 18, fontWeight: FontWeight.w600, color: Colors.white),
      ),

      // ── Cards ──
      cardTheme: CardThemeData(
        elevation: 2,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
        clipBehavior: Clip.antiAlias,
        margin: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
        color: const Color(0xFF1E293B),
      ),

      // ── Inputs ──
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: const Color(0xFF334155),
        border: OutlineInputBorder(borderRadius: BorderRadius.circular(12), borderSide: const BorderSide(color: Color(0xFF475569))),
        enabledBorder: OutlineInputBorder(borderRadius: BorderRadius.circular(12), borderSide: const BorderSide(color: Color(0xFF475569))),
        focusedBorder: OutlineInputBorder(borderRadius: BorderRadius.circular(12), borderSide: const BorderSide(color: Color(0xFF60A5FA), width: 2)),
        errorBorder: OutlineInputBorder(borderRadius: BorderRadius.circular(12), borderSide: const BorderSide(color: Color(0xFFFF6B6B))),
        focusedErrorBorder: OutlineInputBorder(borderRadius: BorderRadius.circular(12), borderSide: const BorderSide(color: Color(0xFFFF6B6B), width: 2)),
        contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
        hintStyle: TextStyle(color: Colors.grey[500], fontSize: 14, fontFamily: _cairo),
      ),

      // ── Buttons ──
      elevatedButtonTheme: ElevatedButtonThemeData(
        style: ElevatedButton.styleFrom(
          backgroundColor: const Color(0xFF3B82F6),
          foregroundColor: Colors.white,
          elevation: 2,
          padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 14),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
          textStyle: const TextStyle(fontFamily: _cairo, fontSize: 16, fontWeight: FontWeight.w600),
        ),
      ),
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          foregroundColor: const Color(0xFF60A5FA),
          side: const BorderSide(color: Color(0xFF60A5FA)),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
          padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 12),
        ),
      ),

      // ── Chips ──
      chipTheme: ChipThemeData(
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
        labelStyle: const TextStyle(fontFamily: _cairo, fontSize: 12),
        backgroundColor: const Color(0xFF334155),
        selectedColor: const Color(0xFF60A5FA).withAlpha(77),
      ),

      // ── Dividers ──
      dividerTheme: const DividerThemeData(color: Color(0xFF334155), thickness: 1),

      // ── Bottom Nav ──
      bottomNavigationBarTheme: BottomNavigationBarThemeData(
        backgroundColor: const Color(0xFF1E1E2E),
        selectedItemColor: const Color(0xFF60A5FA),
        unselectedItemColor: const Color(0xFF64748B),
        type: BottomNavigationBarType.fixed,
        elevation: 8,
        selectedLabelStyle: const TextStyle(fontFamily: _cairo, fontSize: 11),
        unselectedLabelStyle: const TextStyle(fontFamily: _cairo, fontSize: 11),
      ),

      // ── SnackBar ──
      snackBarTheme: SnackBarThemeData(
        behavior: SnackBarBehavior.floating,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
        contentTextStyle: const TextStyle(fontFamily: _cairo, fontSize: 14),
      ),

      // ── Dialog ──
      dialogTheme: DialogThemeData(
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20)),
        titleTextStyle: const TextStyle(fontFamily: _cairo, fontSize: 18, fontWeight: FontWeight.w600),
        contentTextStyle: const TextStyle(fontFamily: _cairo, fontSize: 14),
      ),

      // ── Tab Bar ──
      tabBarTheme: const TabBarThemeData(
        labelStyle: TextStyle(fontFamily: _cairo, fontSize: 14, fontWeight: FontWeight.w600),
        unselectedLabelStyle: TextStyle(fontFamily: _cairo, fontSize: 14),
      ),

      // ── DataTable ──
      dataTableTheme: DataTableThemeData(
        headingTextStyle: const TextStyle(fontFamily: _cairo, fontWeight: FontWeight.bold, fontSize: 12, color: Color(0xFF60A5FA)),
        dataTextStyle: const TextStyle(fontFamily: _cairo, fontSize: 12),
      ),

      // ── Date Picker ──
      datePickerTheme: DatePickerThemeData(
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20)),
        headerBackgroundColor: const Color(0xFF1E293B),
        headerForegroundColor: Colors.white,
        todayForegroundColor: WidgetStateColor.resolveWith((states) {
          if (states.contains(WidgetState.selected)) return Colors.white;
          return const Color(0xFF60A5FA);
        }),
      ),
    );
  }
}
