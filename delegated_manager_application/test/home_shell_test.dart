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
    await tester.pump();

    expect(find.text('الإحصائيات'), findsWidgets);
    expect(find.text('اللجان'), findsWidgets);
    expect(find.text('الرسائل والشكاوى'), findsWidgets);
    expect(find.text('طلبات الاستثناء'), findsWidgets);
    expect(find.text('المبيعات الخاطئة'), findsWidgets);
    expect(find.text('الإعدادات'), findsWidgets);

    await tester.pumpWidget(const SizedBox.shrink());
    await tester.pump();
  });

  testWidgets('committees top bar shows assignment and map titles', (tester) async {
    await tester.pumpWidget(_wrap(const HomeShell()));
    await tester.pump();

    final scaffoldState = tester.state<ScaffoldState>(find.byType(Scaffold).first);
    scaffoldState.openDrawer();
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 350));

    final committees = find.widgetWithText(ListTile, 'اللجان');
    expect(committees, findsOneWidget);
    await tester.tap(committees);
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 350));

    expect(find.text('التكليف'), findsWidgets);
    expect(find.text('خريطة المتابعين'), findsWidgets);
    expect(find.byType(TabBar), findsOneWidget);

    await tester.pumpWidget(const SizedBox.shrink());
    await tester.pump();
  });
}
