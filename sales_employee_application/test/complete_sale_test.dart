import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sales_employee_application/data/sales_models.dart';
import 'package:sales_employee_application/data/sales_repository.dart';
import 'package:sales_employee_application/data/sales_repository_factory.dart';
import 'package:sales_employee_application/screens/sale_complete_success_screen.dart';
import 'package:sales_employee_application/screens/sale_details_screen.dart';
import 'package:sales_employee_application/tracking/work_shift.dart';
import 'package:sales_employee_application/utils/app_theme.dart';

class _MemRepo implements SalesRepository {
  _MemRepo(this.draft);
  SalesDraft draft;
  int completeCalls = 0;
  Completer<SalesCompleteResult>? hold;
  bool failDownload = false;

  @override
  Future<SalesMe> me() async => throw UnimplementedError();
  @override
  Future<List<SalesCustomer>> searchCustomers(String query) async => [];
  @override
  Future<List<SalesInventoryItem>> inventory() async => [];
  @override
  Future<SalesDraft> createSale(SalesDraftCreateRequest request) async => draft;
  @override
  Future<List<SalesDraft>> pending() async => draft.isCompleted ? [] : [draft];
  @override
  Future<List<SalesDraft>> todayCompleted() async =>
      draft.isCompleted ? [draft] : [];
  @override
  Future<SalesDraft> byId(int id) async => draft;
  @override
  Future<SalesCompleteResult> completeSale(int id) async {
    completeCalls++;
    if (hold != null) return hold!.future;
    draft = draft.copyWith(
      status: 'Completed',
      completedAt: DateTime(2026, 9, 2),
      documentsStatus: 'DocumentsReady',
      documents: [
        SalesDocument(type: 'Contract', fileName: 'Sale_1_Contract.pdf', downloadUrl: 'c', documentId: 1),
        SalesDocument(type: 'PromissoryNote', fileName: 'Sale_1_PromissoryNote.pdf', downloadUrl: 'p', documentId: 2),
      ],
    );
    return SalesCompleteResult(
      saleId: draft.saleId,
      status: draft.status,
      finalSalePrice: draft.finalSalePrice,
      completedAt: draft.completedAt,
      documents: draft.documents,
    );
  }

  @override
  Future<List<SalesDocument>> documents(int saleId) async => draft.documents;

  @override
  Future<List<int>> downloadDocument(int saleId, SalesDocument document) async {
    if (failDownload) throw Exception('download failed');
    return [37, 80, 68, 70];
  }

  @override
  Future<WorkShift> startShift() async => throw UnimplementedError();
  @override
  Future<WorkShift?> currentShift() async => null;
  @override
  Future<LocationBatchResult> uploadLocationBatch(int shiftId, List<LocalLocationPoint> points) async =>
      LocationBatchResult();
  @override
  Future<void> recordTrackingEvent(int? shiftId, String eventType) async {}
  @override
  Future<List<SalesWorkRequest>> salesRequests() async => [];
  @override
  Future<SalesWorkRequest> salesRequest(int id) async => throw UnimplementedError();
  @override
  Future<SalesWorkRequest> viewSalesRequest(int id) async => throw UnimplementedError();
  @override
  Future<SalesWorkRequest> startSalesRequest(int id) async => throw UnimplementedError();
  @override
  Future<SalesWorkRequest> rejectSalesRequest(int id, String reason) async => throw UnimplementedError();
}

SalesDraft _draft({String status = 'Pending', int eval = 3}) => SalesDraft(
      saleId: 1,
      fullName: 'أحمد علي',
      status: status,
      evaluationLevel: eval,
      evaluationNote: 'ملاحظة',
      baseSalePrice: 2000000,
      finalSalePrice: 2000000,
      dailyInstallment: 25000,
      createdAt: DateTime(2026, 9, 1),
      items: [SalesDraftItem(productId: 5, quantity: 1, productName: 'ثلاجة')],
    );

Widget _app(Widget home) => MaterialApp(
      theme: AppTheme.themeData,
      home: home,
    );

void main() {
  tearDown(() => SalesRepositoryFactory.reset());

  testWidgets('Complete button only for Pending', (tester) async {
    SalesRepositoryFactory.setInstance(_MemRepo(_draft()));
    await tester.pumpWidget(_app(const SaleDetailsScreen(saleId: 1)));
    await tester.pumpAndSettle();
    expect(find.widgetWithText(ElevatedButton, 'تم البيع'), findsOneWidget);
  });

  testWidgets('No Complete for Rejected', (tester) async {
    SalesRepositoryFactory.setInstance(_MemRepo(_draft(status: 'Rejected', eval: 1)));
    await tester.pumpWidget(_app(const SaleDetailsScreen(saleId: 1)));
    await tester.pumpAndSettle();
    expect(find.text('طلب مرفوض'), findsOneWidget);
    expect(find.widgetWithText(ElevatedButton, 'تم البيع'), findsNothing);
  });

  testWidgets('Loading prevents double tap', (tester) async {
    final repo = _MemRepo(_draft())
      ..hold = Completer<SalesCompleteResult>()
      ..failDownload = true;
    SalesRepositoryFactory.setInstance(repo);
    await tester.pumpWidget(_app(const SaleDetailsScreen(saleId: 1)));
    await tester.pumpAndSettle();
    await tester.tap(find.text('تم البيع'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('نعم، تم البيع'));
    await tester.pump();
    await tester.tap(find.text('نعم، تم البيع'), warnIfMissed: false);
    await tester.pump();
    expect(repo.completeCalls, 1);
    expect(find.byType(CircularProgressIndicator), findsWidgets);
    repo.hold!.complete(SalesCompleteResult(saleId: 1, status: 'Completed', finalSalePrice: 2000000));
    await tester.pumpAndSettle();
  });

  testWidgets('Success screen', (tester) async {
    await tester.pumpWidget(_app(const SaleCompleteSuccessScreen(
      saleId: 1,
      finalSalePrice: 4000000,
      contractPath: 'c.pdf',
      receiptPath: 'p.pdf',
    )));
    expect(find.text('✓ تمت عملية البيع بنجاح'), findsOneWidget);
    expect(find.text('✓ عقد البيع'), findsOneWidget);
    expect(find.text('✓ وصل الأمانة'), findsOneWidget);
    expect(find.text('فتح عقد البيع'), findsOneWidget);
    expect(find.text('العودة للمبيعات'), findsOneWidget);
  });

  testWidgets('PDF download error state', (tester) async {
    await tester.pumpWidget(_app(SaleCompleteSuccessScreen(
      saleId: 1,
      finalSalePrice: 4000000,
      downloadFailed: true,
      onRetryDownload: () async {},
    )));
    expect(find.textContaining('تم البيع بنجاح'), findsOneWidget);
    expect(find.text('إعادة تنزيل المستندات'), findsOneWidget);
  });
}
