import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sales_employee_application/data/mock_sales_repository.dart';
import 'package:sales_employee_application/data/sales_repository_factory.dart';
import 'package:sales_employee_application/screens/pending_sales_screen.dart';
import 'package:sales_employee_application/screens/login_screen.dart';
import 'package:sales_employee_application/widgets/company_loader.dart';
import 'package:sales_employee_application/utils/app_theme.dart';

void main() {
  setUpAll(() async {
    final font = FontLoader('ElMessiri');
    for (final weight in ['Regular', 'Medium', 'SemiBold', 'Bold']) {
      font.addFont(rootBundle.load('assets/fonts/ElMessiri-$weight.ttf'));
    }
    await font.load();
    final icons = FontLoader('MaterialIcons')
      ..addFont(rootBundle.load('fonts/MaterialIcons-Regular.otf'));
    await icons.load();
  });
  tearDown(() => SalesRepositoryFactory.reset());

  Widget app(Widget child, {double scale = 1}) => MaterialApp(
    theme: AppTheme.themeData,
    builder: (context, child) => MediaQuery(
      data: MediaQuery.of(context).copyWith(textScaler: TextScaler.linear(scale)),
      child: Directionality(textDirection: TextDirection.rtl, child: child!),
    ),
    home: child,
  );

  testWidgets('Dashboard remains scrollable on small screens with large text', (tester) async {
    tester.view.physicalSize = const Size(360, 640);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    SalesRepositoryFactory.setInstance(MockSalesRepository());
    await tester.pumpWidget(app(const PendingSalesScreen(), scale: 1.5));
    await tester.pump(const Duration(seconds: 1));
    expect(tester.takeException(), isNull);
    await tester.scrollUntilVisible(find.text('تم البيع'), 200, scrollable: find.byType(Scrollable).first);
    expect(find.text('تم البيع'), findsOneWidget);
    expect(tester.takeException(), isNull);
  });

  testWidgets('Dashboard visual reference', (tester) async {
    tester.view.physicalSize = const Size(390, 1000);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    SalesRepositoryFactory.setInstance(MockSalesRepository());
    await tester.pumpWidget(app(const PendingSalesScreen()));
    await tester.pump(const Duration(seconds: 1));
    await tester.pump(const Duration(seconds: 1));
    expect(tester.takeException(), isNull);
    await expectLater(find.byType(PendingSalesScreen), matchesGoldenFile('goldens/dashboard.png'));
  });

  testWidgets('Login fits small keyboard viewport', (tester) async {
    tester.view.physicalSize = const Size(360, 420);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    await tester.pumpWidget(app(const LoginScreen(), scale: 1.5));
    await tester.pump(const Duration(seconds: 1));
    await tester.ensureVisible(find.text('تسجيل الدخول'));
    expect(tester.takeException(), isNull);
  });

  testWidgets('Logo loader respects reduced motion and compact constraints', (tester) async {
    await tester.pumpWidget(MaterialApp(home: MediaQuery(
      data: const MediaQueryData(disableAnimations: true),
      child: const Center(child: SizedBox(width: 22, height: 22, child: CompanyLoader(size: 22))),
    )));
    await tester.pumpAndSettle();
    expect(find.byType(Image), findsOneWidget);
    expect(tester.hasRunningAnimations, isFalse);
    expect(tester.takeException(), isNull);
  });
}
