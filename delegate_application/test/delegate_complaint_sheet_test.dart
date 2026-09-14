import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:delegate_application/ui/delegate_complaint_sheet.dart';

void main() {
  testWidgets('complaint sheet shows confidential copy and safe submit',
      (tester) async {
    await tester.pumpWidget(
      const MaterialApp(
        home: Scaffold(
          body: DelegateComplaintSheet(),
        ),
      ),
    );

    expect(find.text('إرسال شكوى إلى المدير المفوض'), findsOneWidget);
    expect(
      find.text('الشكوى سرية وتصل مباشرة إلى المدير المفوض.'),
      findsOneWidget,
    );
    expect(
      find.textContaining('تُحفظ معلومات الحساب داخلياً'),
      findsOneWidget,
    );
    expect(find.text('مجهول'), findsNothing);

    await tester.enterText(find.byType(TextFormField), 'قصير');
    await tester.tap(find.text('إرسال'));
    await tester.pump();
    expect(find.textContaining('أحرف'), findsWidgets);
  });
}
