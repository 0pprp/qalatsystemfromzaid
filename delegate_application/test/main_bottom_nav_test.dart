import 'package:delegate_application/ui/main_bottom_nav.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  testWidgets('bottom nav RTL order: sales, customers, gap, payments', (tester) async {
    int? tapped;
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: const SizedBox.shrink(),
          bottomNavigationBar: MainBottomNavBar(
            selectedIndex: 2,
            onTap: (i) => tapped = i,
          ),
        ),
      ),
    );

    expect(find.text('المبيعات'), findsOneWidget);
    expect(find.text('العملاء'), findsOneWidget);
    expect(find.text('التسديدات'), findsOneWidget);

    // Visual RTL order right→left: المبيعات, العملاء, التسديدات
    final sales = tester.getCenter(find.text('المبيعات'));
    final customers = tester.getCenter(find.text('العملاء'));
    final payments = tester.getCenter(find.text('التسديدات'));

    expect(sales.dx, greaterThan(customers.dx));
    expect(customers.dx, greaterThan(payments.dx));

    await tester.tap(find.text('المبيعات'));
    expect(tapped, 0);
    await tester.tap(find.text('العملاء'));
    expect(tapped, 1);
    await tester.tap(find.text('التسديدات'));
    expect(tapped, 3);
  });

  testWidgets('no overflow at compact width 360', (tester) async {
    tester.view.physicalSize = const Size(360, 800);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: const SizedBox.shrink(),
          bottomNavigationBar: MainBottomNavBar(
            selectedIndex: 0,
            onTap: (_) {},
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
    expect(tester.takeException(), isNull);
  });
}
