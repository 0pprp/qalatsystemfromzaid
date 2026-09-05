import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sales_employee_application/data/mock_sales_repository.dart';
import 'package:sales_employee_application/data/sales_models.dart';
import 'package:sales_employee_application/data/sales_repository_factory.dart';
import 'package:sales_employee_application/screens/pending_sales_screen.dart';
import 'package:sales_employee_application/screens/sale_screen.dart';
import 'package:sales_employee_application/utils/app_theme.dart';
import 'package:sales_employee_application/utils/sales_format.dart';

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
    expect(find.text('تجهيز المبيع'), findsOneWidget);
    expect(find.text('معلقة'), findsOneWidget);
    expect(find.text('رفض'), findsOneWidget);
    expect(find.text('طلبات معلقة'), findsNothing);
  });

  testWidgets('Pending note dialog can confirm without crashing', (tester) async {
    tester.view.physicalSize = const Size(800, 2000);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    final repo = MockSalesRepository()
      ..seedRequest(SalesWorkRequest(
        id: 9,
        customerName: 'سعد كاظم',
        customerPhone: '0770',
        customerProvince: 'النجف',
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
    await tester.tap(find.text('طلبات المبيعات'));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 800));
    final pendBtn = find.widgetWithText(OutlinedButton, 'معلقة');
    expect(pendBtn, findsOneWidget);
    await tester.tap(pendBtn);
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 300));
    expect(find.text('تعليق الطلب'), findsOneWidget);
    await tester.enterText(find.byType(TextField), 'الزبون مشغول');
    await tester.pump();
    await tester.tap(find.widgetWithText(ElevatedButton, 'إرسال'));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 400));
    expect(tester.takeException(), isNull);
    expect(find.text('سعد كاظم'), findsOneWidget);
    expect(find.text('ملاحظة التعليق: الزبون مشغول'), findsOneWidget);
  });

  testWidgets('Completed sale appears under today not pending', (tester) async {
    final repo = MockSalesRepository();
    await tester.runAsync(() async {
      await repo.createSale(SalesDraftCreateRequest(
        customer: const {'fullName': 'ahmed', 'phone': '0780', 'province': 'النجف'},
        items: [SalesDraftItem(productId: 5, quantity: 1)],
        evaluationLevel: 5,
        evaluationNote: 'ممتاز',
        dailyInstallment: 10000,
        customerListId: 1,
      ));
      final created = (await repo.pending()).first;
      await repo.completeSale(created.saleId);
    });
    SalesRepositoryFactory.setInstance(repo);
    await tester.pumpWidget(MaterialApp(
      theme: AppTheme.themeData,
      home: const PendingSalesScreen(),
    ));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 400));
    expect(find.text('ahmed'), findsNothing);
    await tester.tap(find.text('مبيعات اليوم'));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 300));
    expect(find.text('ahmed'), findsOneWidget);
    expect(find.text('مكتمل'), findsWidgets);
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

  test('Pending requires note and stays separate from PreparedForSale', () async {
    final repo = MockSalesRepository()
      ..seedRequest(SalesWorkRequest(
        id: 1,
        customerName: 'أ',
        status: 'Assigned',
        createdAtUtc: DateTime.utc(2026, 9, 2),
      ));
    expect(() => repo.pendSalesRequest(1, '  '), throwsException);
    final pending = await repo.pendSalesRequest(1, 'الزبون مشغول');
    expect(pending.status, 'Pending');
    expect(pending.pendingNote, 'الزبون مشغول');
    final prepared = await repo.prepareSalesRequest(1);
    expect(prepared.status, 'PreparedForSale');
  });

  test('Returned request shows return note and can be prepared again', () async {
    final repo = MockSalesRepository()
      ..seedRequest(SalesWorkRequest(
        id: 2,
        customerName: 'ب',
        status: 'Returned',
        returnNote: 'أكمل البيانات',
        createdAtUtc: DateTime.utc(2026, 9, 2),
      ));
    final row = await repo.salesRequest(2);
    expect(row.returnNote, 'أكمل البيانات');
    final prepared = await repo.prepareSalesRequest(2);
    expect(prepared.status, 'PreparedForSale');
  });

  test('Accepted evaluation does not double price', () async {
    final repo = MockSalesRepository();
    final draft = await repo.createSale(SalesDraftCreateRequest(
      customer: const {'fullName': 'أ', 'phone': '1', 'province': 'النجف'},
      items: [SalesDraftItem(productId: 5, quantity: 1)],
      evaluationLevel: 2,
      evaluationNote: 'مقبول مع ملاحظة',
      dailyInstallment: 10000,
      customerListId: 1,
    ));
    expect(draft.status, 'Rejected');
    expect(draft.finalSalePrice, 0);
    expect(draft.isRejected, isTrue);
    expect(draft.canComplete, isFalse);
  });

  test('Inventory filter hides fit-out and external mobiles', () {
    expect(SalesStaffInventoryFilter.isHidden('تجهيز محل'), isTrue);
    expect(SalesStaffInventoryFilter.isHidden('موبايلات خارجية'), isTrue);
    expect(SalesStaffInventoryFilter.isHidden('ثلاجة سامسونج'), isFalse);
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
      customerListId: 1,
    );
    await repo.createSale(req);
    expect(() => repo.createSale(req), throwsException);
  });

  testWidgets('Create sale from request prefills customer', (tester) async {
    SalesRepositoryFactory.setInstance(MockSalesRepository());
    final request = SalesWorkRequest(
      id: 3,
      customerName: 'ليث محمد',
      customerPhone: '0780',
      customerProvince: 'النجف',
      customerAddress: 'الكوفة',
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
    await tester.pump(const Duration(milliseconds: 300));
    expect(find.text('ليث محمد'), findsWidgets);
    expect(find.text('الاسم الكامل *'), findsNothing);
    expect(find.text('رقم الهاتف *'), findsNothing);
    expect(find.text('المحافظة *'), findsNothing);
    expect(find.text('العنوان *'), findsNothing);
    expect(find.text('زبون موجود'), findsNothing);
    expect(find.text('رقم البطاقة الوطنية *'), findsOneWidget);
    expect(find.text('أقرب نقطة دالة *'), findsOneWidget);
    expect(find.text('اسم المختار *'), findsOneWidget);
    expect(find.text('قائمة الزبون *'), findsOneWidget);
    expect(find.text('رقم مركز التموين (اختياري)'), findsNothing);
  });
}
