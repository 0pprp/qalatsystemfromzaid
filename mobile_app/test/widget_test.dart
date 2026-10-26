import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:management_app/main.dart';
import 'package:management_app/providers/auth_provider.dart';
import 'package:shared_preferences/shared_preferences.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  setUp(() {
    SharedPreferences.setMockInitialValues({});
  });

  testWidgets('App loads successfully', (WidgetTester tester) async {
    final auth = AuthProvider();
    await auth.tryAutoLogin();
    await tester.pumpWidget(
      ManagementApp(
        authProvider: auth,
        loginCitiesLoader: () async => [
          {
            'name': 'بغداد',
            'link': 'http://example.test/',
            'database': 'demo',
          },
        ],
      ),
    );
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 900));

    expect(find.text('تطبيق الإدارة'), findsOneWidget);

    await tester.pumpWidget(const SizedBox.shrink());
    await tester.pump();
  });
}
