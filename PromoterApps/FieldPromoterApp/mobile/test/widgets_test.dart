import 'package:customer_app/core/theme/app_theme.dart';
import 'package:customer_app/features/auth/presentation/auth_widgets.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

Widget wrap(Widget child) => MaterialApp(
      theme: AppTheme.light(),
      home: Directionality(textDirection: TextDirection.rtl, child: Scaffold(body: Center(child: child))),
    );

void main() {
  testWidgets('Phone field keeps only digits and normalises pasted +964 numbers', (tester) async {
    final controller = TextEditingController();
    await tester.pumpWidget(wrap(PhoneField(controller: controller)));
    expect(find.text('+964'), findsOneWidget);

    await tester.enterText(find.byType(TextField), '+964 770 123 4567');
    await tester.pump();
    expect(controller.text, '07701234567');

    await tester.enterText(find.byType(TextField), '07701234567999');
    await tester.pump();
    expect(controller.text.length, lessThanOrEqualTo(11));
  });
}
