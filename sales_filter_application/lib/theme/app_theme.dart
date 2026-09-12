import 'package:flutter/material.dart';

class AppTheme {
  static const Color primary = Color(0xFF0F5C4C);
  static const Color surface = Color(0xFFF4F7F6);

  static ThemeData get light => ThemeData(
        useMaterial3: true,
        fontFamily: 'Cairo',
        colorScheme: ColorScheme.fromSeed(seedColor: primary, primary: primary),
        scaffoldBackgroundColor: surface,
        appBarTheme: const AppBarTheme(
          backgroundColor: primary,
          foregroundColor: Colors.white,
          centerTitle: true,
        ),
      );
}

class FilterStatuses {
  static const pending = 'PendingFilter';
  static const onHold = 'OnHold';
  static const ready = 'ReadyForSale';
  static const rejected = 'Rejected';

  static const tabs = [
    (pending, 'طلبات البيع'),
    (onHold, 'معلق'),
    (ready, 'جاهز للبيع'),
    (rejected, 'مرفوض'),
  ];
}
