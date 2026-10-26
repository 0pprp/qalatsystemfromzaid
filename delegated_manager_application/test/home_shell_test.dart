import 'package:delegated_manager_application/core/theme/app_theme.dart';
import 'package:delegated_manager_application/features/shell/presentation/home_shell.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

Widget _wrap(Widget child) => MaterialApp(
      theme: AppTheme.light,
      locale: const Locale('ar'),
      builder: (context, built) => Directionality(
        textDirection: TextDirection.rtl,
        child: built ?? const SizedBox.shrink(),
      ),
      home: child,
    );

void main() {
  testWidgets('home uses a drawer menu instead of a bottom bar', (tester) async {
    await tester.pumpWidget(_wrap(const HomeShell()));
    await tester.pump();

    expect(find.byType(NavigationBar), findsNothing);
    expect(find.byType(BottomNavigationBar), findsNothing);
    expect(find.byIcon(Icons.menu), findsOneWidget);

    await tester.tap(find.byIcon(Icons.menu));
    await tester.pumpAndSettle();

    expect(find.text('مهامي'), findsWidgets);
    expect(find.text('التقييمات'), findsWidgets);
    expect(find.text('اللجان'), findsNothing);
    expect(find.text('مستخدمو إدارة الفرع'), findsNothing);
    expect(find.text('الرسائل والشكاوى'), findsWidgets);
    expect(find.text('طلبات الاستثناء'), findsWidgets);
    expect(find.text('المبيعات الخاطئة'), findsWidgets);

    // Drawer list grew after «مهامي»; scroll to reach lower items.
    await tester.drag(find.byType(Drawer), const Offset(0, -400));
    await tester.pumpAndSettle();

    expect(find.text('حسابات إدارة الفرع'), findsWidgets);
    expect(find.text('الإعدادات'), findsWidgets);

    await tester.pumpWidget(const SizedBox.shrink());
    await tester.pump();
  });
}
