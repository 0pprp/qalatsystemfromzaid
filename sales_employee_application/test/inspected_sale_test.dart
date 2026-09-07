import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sales_employee_application/data/mock_sales_repository.dart';
import 'package:sales_employee_application/data/sales_models.dart';
import 'package:sales_employee_application/data/sales_repository_factory.dart';
import 'package:sales_employee_application/screens/pending_sales_screen.dart';
import 'package:sales_employee_application/utils/app_theme.dart';
import 'package:sales_employee_application/utils/sales_format.dart';

void main() {
  tearDown(() => SalesRepositoryFactory.reset());

  SalesDraftCreateRequest progress({
    int step = 2,
    int productId = 5,
    String shopName = 'محل سعد',
  }) =>
      SalesDraftCreateRequest(
        customer: {
          'fullName': 'سعد كاظم',
          'phone': '07701234567',
          'province': 'النجف',
          'nationalCardNumber': 'N99',
          'address': 'حي الأنصار',
          'nearestLandmark': 'جامع',
          'mukhtarName': 'حسن',
        },
        items: [SalesDraftItem(productId: productId, quantity: 1)],
        salesRequestId: 9,
        wizardCurrentStep: step,
        shop: SalesShopComplete(
          shopName: shopName,
          shopBusinessType: 'مواد غذائية',
          shopStockEstimatedValue: 1000000,
          estimatedDailyRevenue: 50000,
          shopLength: 4,
          shopWidth: 3,
          shopImageKey: 'sales/1/shop.jpg',
          latitude: 32.0,
          longitude: 44.3,
        ),
      );

  test('Inspected is not Completed and keeps the same draft', () async {
    final repo = MockSalesRepository()
      ..seedRequest(SalesWorkRequest(
        id: 9,
        customerName: 'سعد كاظم',
        status: 'Assigned',
        createdAtUtc: DateTime.utc(2026, 9, 2),
      ));
    final row = await repo.inspectSalesRequest(9, progress());
    expect(row.status, 'Inspected');
    expect(row.isSold, isFalse);
    expect(SalesRequestStatusLabels.of(row.status), 'تم الكشف');
    expect(repo.completeCalls, 0);
    expect(repo.deductionCount, 0);

    final draft = await repo.byId(row.convertedToSaleId!);
    expect(draft.isCompleted, isFalse);
    expect(draft.status, 'Pending');
    expect(draft.wizardCurrentStep, 2);
    expect(draft.fullName, 'سعد كاظم');
    expect(draft.shop?.shopName, 'محل سعد');
    expect(draft.items.single.productId, 5);

    final again = await repo.saveSaleProgress(progress(productId: 11, shopName: 'محل سعد'));
    expect(again.saleId, draft.saleId);
    expect(again.items.single.productId, 11);
  });

  testWidgets('Home shows Inspected bin and lists those requests', (tester) async {
    final repo = MockSalesRepository()
      ..seedRequest(SalesWorkRequest(
        id: 9,
        customerName: 'سعد كاظم',
        status: 'Inspected',
        createdAtUtc: DateTime.utc(2026, 9, 2),
        convertedToSaleId: 101,
      ));
    SalesRepositoryFactory.setInstance(repo);
    await tester.pumpWidget(MaterialApp(
      theme: AppTheme.themeData,
      home: const PendingSalesScreen(),
    ));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 400));
    expect(find.text('تم الكشف'), findsOneWidget);
    await tester.tap(find.text('تم الكشف'));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 400));
    expect(find.text('سعد كاظم'), findsOneWidget);
    expect(find.text('ahmed'), findsNothing);
    expect(find.text('متابعة البيع'), findsOneWidget);
    expect(find.widgetWithText(TextButton, 'مرفوض'), findsOneWidget);
    expect(find.widgetWithText(ElevatedButton, 'جاهز للبيع'), findsNothing);
    expect(find.widgetWithText(OutlinedButton, 'معلّق'), findsNothing);
  });

  test('Inspected available actions are continue and reject only', () {
    final row = SalesWorkRequest(
      id: 9,
      customerName: 'سعد كاظم',
      status: 'Inspected',
      createdAtUtc: DateTime.utc(2026, 9, 2),
      convertedToSaleId: 101,
    );
    expect(row.availableActions, ['continue', 'rejected']);
    expect(row.availableActions, isNot(contains('prepared')));
    expect(row.availableActions, isNot(contains('pending')));
    expect(row.canReject, isTrue);
    expect(row.canContinueSale, isTrue);
  });

  testWidgets('Inspected details screen shows continue and reject only', (tester) async {
    final repo = MockSalesRepository()
      ..seedRequest(SalesWorkRequest(
        id: 9,
        customerName: 'سعد كاظم',
        status: 'Inspected',
        createdAtUtc: DateTime.utc(2026, 9, 2),
        convertedToSaleId: 101,
      ));
    SalesRepositoryFactory.setInstance(repo);
    await tester.pumpWidget(MaterialApp(
      theme: AppTheme.themeData,
      home: const SalesRequestDetailsScreen(requestId: 9),
    ));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 400));
    expect(find.widgetWithText(ElevatedButton, 'متابعة البيع'), findsOneWidget);
    expect(find.widgetWithText(TextButton, 'مرفوض'), findsOneWidget);
    expect(find.widgetWithText(ElevatedButton, 'جاهز للبيع'), findsNothing);
    expect(find.widgetWithText(OutlinedButton, 'معلّق'), findsNothing);
    expect(find.widgetWithText(ElevatedButton, 'إنشاء بيع'), findsNothing);
  });

  test('Inspected preferDisplay keeps combined document only', () {
    final docs = SalesDocument.preferDisplay([
      SalesDocument(type: 'SaleDocuments', fileName: 'a.pdf', downloadUrl: 'u1', documentId: 1),
      SalesDocument(type: 'Contract', fileName: 'b.pdf', downloadUrl: 'u2', documentId: 2),
      SalesDocument(type: 'PromissoryNote', fileName: 'c.pdf', downloadUrl: 'u3', documentId: 3),
    ]);
    expect(docs, hasLength(1));
    expect(docs.single.type, 'SaleDocuments');
  });
}
