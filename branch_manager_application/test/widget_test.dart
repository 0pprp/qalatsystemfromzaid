import 'package:branch_manager_application/main.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  testWidgets('app boots', (tester) async {
    await tester.pumpWidget(const BranchManagerApp());
    expect(find.text('مدير الفرع'), findsOneWidget);
  });
}
