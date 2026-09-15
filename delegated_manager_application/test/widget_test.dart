import 'package:delegated_manager_application/core/theme/app_theme.dart';
import 'package:delegated_manager_application/features/authentication/presentation/login_screen.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

Widget _wrap(Widget child) => MaterialApp(
      theme: AppTheme.light,
      locale: const Locale('ar'),
      builder: (context, built) => Directionality(
        textDirection: TextDirection.rtl,
        child: built ?? const SizedBox.shrink(),
      ),
      home: child,
    );

void main() {
  testWidgets('login screen is Arabic RTL and has no city selector',
      (tester) async {
    await tester.pumpWidget(_wrap(const LoginScreen()));

    expect(find.text('تطبيق المدير المفوض'), findsOneWidget);
    expect(find.text('اسم المستخدم'), findsOneWidget);
    expect(find.text('كلمة المرور'), findsOneWidget);
    expect(find.text('تسجيل الدخول'), findsOneWidget);
    expect(find.text('المحافظة'), findsNothing);
    expect(find.byType(DropdownButtonFormField<String>), findsNothing);

    expect(
      Directionality.of(tester.element(find.byType(LoginScreen))),
      TextDirection.rtl,
    );
  });

  testWidgets('login validates empty credentials before calling the gateway',
      (tester) async {
    await tester.pumpWidget(_wrap(const LoginScreen()));

    await tester.tap(find.text('تسجيل الدخول'));
    await tester.pump();

    expect(find.text('اسم المستخدم مطلوب'), findsOneWidget);
    expect(find.text('كلمة المرور مطلوبة'), findsOneWidget);
  });
}
