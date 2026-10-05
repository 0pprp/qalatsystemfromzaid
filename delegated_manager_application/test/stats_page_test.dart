import 'package:delegated_manager_application/core/theme/app_theme.dart';
import 'package:delegated_manager_application/features/stats/presentation/stats_page.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

Widget _wrap(Widget child) => MaterialApp(
      theme: AppTheme.light,
      locale: const Locale('ar'),
      builder: (context, built) => Directionality(
        textDirection: TextDirection.rtl,
        child: built ?? const SizedBox.shrink(),
      ),
      home: Scaffold(body: child),
    );

void main() {
  testWidgets('ratings page copies branch-manager scopes and asks for a city',
      (tester) async {
    await tester.pumpWidget(_wrap(const StatsPage()));
    await tester.pump();

    expect(find.text('اختر المحافظة'), findsOneWidget);
    expect(find.text('الفرع'), findsOneWidget);
    expect(find.text('قائمة'), findsOneWidget);
    expect(find.text('متابع'), findsOneWidget);
    expect(find.text('موظف مبيعات'), findsOneWidget);

    await tester.pumpWidget(const SizedBox.shrink());
    await tester.pump();
  });
}
