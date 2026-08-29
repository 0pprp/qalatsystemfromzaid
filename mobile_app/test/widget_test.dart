import 'package:flutter_test/flutter_test.dart';
import 'package:management_app/main.dart';
import 'package:management_app/providers/auth_provider.dart';

void main() {
  testWidgets('App loads successfully', (WidgetTester tester) async {
    final auth = AuthProvider();
    await tester.pumpWidget(ManagementApp(authProvider: auth));
    expect(find.text('تطبيق الإدارة'), findsOneWidget);
  });
}
