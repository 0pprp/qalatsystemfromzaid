import 'package:branch_manager_application/ui/branch_visit_page.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  testWidgets('visit entry form builds without delegate ratings', (tester) async {
    await tester.pumpWidget(
      MaterialApp(
        home: BranchVisitPage(
          visit: const {
            'id': 'v1',
            'cityName': 'النجف',
            'arrivedAtUtc': '2026-10-25T00:30:00Z',
            'members': [
              {'displayName': 'عضو'},
            ],
            'assignedTasks': [],
          },
          onContinue: () {},
        ),
      ),
    );
    await tester.pump();

    expect(tester.takeException(), isNull);
    expect(find.text('استمارة دخول الفرع'), findsOneWidget);
    expect(find.textContaining('وقت الوصول: 2026/10/25'), findsOneWidget);
    expect(find.textContaining('0–10'), findsNothing);
    expect(find.textContaining('مطلوب'), findsNothing);
    expect(find.text('متابعة العمل في المحافظة'), findsOneWidget);
  });
}
