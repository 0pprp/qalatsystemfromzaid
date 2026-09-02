import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sales_employee_application/data/mock_sales_repository.dart';
import 'package:sales_employee_application/data/sales_models.dart';
import 'package:sales_employee_application/data/sales_repository_factory.dart';
import 'package:sales_employee_application/screens/pending_sales_screen.dart';
import 'package:sales_employee_application/screens/sale_screen.dart';
import 'package:sales_employee_application/utils/app_theme.dart';

void main() {
  tearDown(() => SalesRepositoryFactory.reset());

  testWidgets('Requests tab is real and lists assigned requests', (tester) async {
    final repo = MockSalesRepository()
      ..seedRequest(SalesWorkRequest(
        id: 9,
        customerName: 'سعد كاظم',
        customerPhone: '0770',
        customerProvince: 'النجف',
        notes: 'يرجى الزيارة',
        status: 'New',
        createdAtUtc: DateTime.utc(2026, 9, 2),
      ));
    SalesRepositoryFactory.setInstance(repo);
    await tester.pumpWidget(MaterialApp(
      theme: AppTheme.themeData,
      home: const PendingSalesScreen(),
    ));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 400));
    expect(find.text('ستتوفر طلبات المبيعات لاحقاً'), findsNothing);
    await tester.tap(find.text('طلبات المبيعات'));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 300));
    expect(find.text('سعد كاظم'), findsOneWidget);
  });

  test('New -> Viewed is idempotent', () async {
    final repo = MockSalesRepository()
      ..seedRequest(SalesWorkRequest(
        id: 1,
        customerName: 'أ',
        status: 'New',
        createdAtUtc: DateTime.utc(2026, 9, 2),
      ));
    final first = await repo.viewSalesRequest(1);
    final second = await repo.viewSalesRequest(1);
    expect(first.status, 'Viewed');
    expect(second.status, 'Viewed');
  });

  test('Reject requires reason', () async {
    final repo = MockSalesRepository()
      ..seedRequest(SalesWorkRequest(
        id: 1,
        customerName: 'أ',
        status: 'New',
        createdAtUtc: DateTime.utc(2026, 9, 2),
      ));
    expect(() => repo.rejectSalesRequest(1, '  '), throwsException);
    final row = await repo.rejectSalesRequest(1, 'الزبون غير موجود');
    expect(row.status, 'Rejected');
  });

  test('Duplicate convert prevented', () async {
    final repo = MockSalesRepository();
    final req = SalesDraftCreateRequest(
      customer: const {'fullName': 'أ', 'phone': '1', 'province': 'النجف'},
      items: [SalesDraftItem(productId: 5, quantity: 1)],
      evaluationLevel: 3,
      evaluationNote: 'ملاحظة كافية',
      dailyInstallment: 10000,
      salesRequestId: 4,
    );
    await repo.createSale(req);
    expect(() => repo.createSale(req), throwsException);
  });

  testWidgets('Create sale from request prefills customer', (tester) async {
    final request = SalesWorkRequest(
      id: 3,
      customerName: 'ليث محمد',
      customerPhone: '0780',
      customerProvince: 'النجف',
      status: 'Viewed',
      createdAtUtc: DateTime.utc(2026, 9, 2),
    );
    await tester.pumpWidget(MaterialApp(
      theme: AppTheme.themeData,
      initialRoute: '/',
      onGenerateRoute: (_) => MaterialPageRoute(
        settings: RouteSettings(arguments: request),
        builder: (_) => const SaleScreen(),
      ),
    ));
    await tester.pump();
    expect(find.text('ليث محمد'), findsWidgets);
  });
}
