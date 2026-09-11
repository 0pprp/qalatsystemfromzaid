import 'package:delegate_application/ui/main_bottom_nav.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  testWidgets('app smoke: bottom nav mounts', (WidgetTester tester) async {
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          bottomNavigationBar: MainBottomNavBar(
            selectedIndex: 2,
            onTap: (_) {},
          ),
        ),
      ),
    );
    expect(find.text('الرئيسية'), findsOneWidget);
  });
}
