import 'package:branch_manager_application/main.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  setUp(() {
    SharedPreferences.setMockInitialValues({});
  });

  testWidgets('app boots to إدارة الفرع login', (tester) async {
    await tester.pumpWidget(const BranchManagerApp());
    await tester.pump();
    expect(find.text('تطبيق إدارة الفرع'), findsOneWidget);
    expect(find.text('دخول'), findsOneWidget);
  });
}
