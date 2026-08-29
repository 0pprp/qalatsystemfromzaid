import 'package:follower_application/main.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  testWidgets('app loads', (WidgetTester tester) async {
    await tester.pumpWidget(const MyApp());
    expect(find.text('تطبيق المتابع'), findsOneWidget);
  });
}
