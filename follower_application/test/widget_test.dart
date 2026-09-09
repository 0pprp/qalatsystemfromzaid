import 'package:follower_application/main.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  testWidgets('app loads', (WidgetTester tester) async {
    SharedPreferences.setMockInitialValues({});
    await tester.pumpWidget(const MyApp());
    expect(find.text('تطبيق المتابع'), findsOneWidget);
    // WelcomePage schedules a 2s navigation timer — advance so binding invariants pass.
    await tester.pump(const Duration(seconds: 2));
    await tester.pumpAndSettle();
  });
}
