import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sales_filter_application/config/app_env.dart';
import 'package:sales_filter_application/models/filter_models.dart';
import 'package:sales_filter_application/screens/detail_screen.dart';
import 'package:sales_filter_application/screens/home_screen.dart';
import 'package:sales_filter_application/screens/login_screen.dart';
import 'package:sales_filter_application/services/filter_repository.dart';
import 'package:sales_filter_application/theme/app_theme.dart';
import 'package:sales_filter_application/widgets/request_card.dart';

class _FakeRepo extends FilterRepository {
  String? lastHoldNote;

  @override
  Future<List<FilterCity>> myCities() async => [
        FilterCity(cityValue: 'baghdad-karkh', cityName: 'بغداد الكرخ'),
      ];

  @override
  Future<Map<String, int>> counts({required String cityValue}) async => {
        FilterStatuses.pending: 2,
        FilterStatuses.onHold: 1,
        FilterStatuses.ready: 3,
        FilterStatuses.rejected: 0,
      };

  @override
  Future<List<FilterRequest>> list({required String status, String? cityValue, int page = 1}) async => [
        FilterRequest(
          id: 1,
          customerName: 'أحمد علي',
          customerPhone: '07701234567',
          cityName: 'بغداد الكرخ',
          customerAddress: 'حي الجامعة',
          wantedDescription: 'iPhone 16',
          filterStatus: status,
          filterNote: status == FilterStatuses.onHold ? 'اتصل غداً' : null,
          rejectReason: status == FilterStatuses.rejected ? 'رقم خاطئ' : null,
        ),
      ];

  @override
  Future<FilterRequest> get(String cityValue, int id) async => FilterRequest(
        id: id,
        customerName: 'أحمد علي',
        customerPhone: '07701234567',
        cityName: 'بغداد الكرخ',
        customerAddress: 'حي الجامعة',
        wantedDescription: 'iPhone 16',
        filterStatus: FilterStatuses.pending,
      );

  @override
  Future<FilterRequest> hold(String cityValue, int id, {required String note}) async {
    lastHoldNote = note;
    return get(cityValue, id);
  }

  @override
  Future<FilterRequest> ready(String cityValue, int id, {String? note}) async => get(cityValue, id);

  @override
  Future<FilterRequest> reject(String cityValue, int id, {required String reason, String? note}) async => get(cityValue, id);
}

void main() {
  testWidgets('login has no branch dropdown', (tester) async {
    await tester.pumpWidget(const MaterialApp(home: LoginScreen()));
    await tester.pumpAndSettle();
    expect(find.text('الفرع'), findsNothing);
    expect(find.text('اسم المستخدم'), findsOneWidget);
  });

  test('login endpoint targets gateway', () {
    final base = AppEnv.normalizeBase('http://127.0.0.1:5280/api/');
    expect('${base}Auth/LoginSalesFilter', 'http://127.0.0.1:5280/api/Auth/LoginSalesFilter');
  });

  testWidgets('four section cards visible', (tester) async {
    await tester.pumpWidget(MaterialApp(
      home: HomeScreen(repository: _FakeRepo(), dialer: (_) async => true),
    ));
    await tester.pumpAndSettle();
    expect(find.text('طلبات البيع'), findsOneWidget);
    expect(find.text('معلق'), findsOneWidget);
    expect(find.text('جاهز للبيع'), findsOneWidget);
    expect(find.text('مرفوض'), findsOneWidget);
    expect(find.byType(Card), findsWidgets);
  });

  testWidgets('label is نوع المبيع not شنو يريد', (tester) async {
    await tester.pumpWidget(MaterialApp(
      home: Scaffold(
        body: RequestCard(
          request: FilterRequest(
            id: 1,
            customerName: 'أحمد علي',
            customerPhone: '0770',
            cityName: 'النجف',
            customerAddress: 'حي الأمير',
            wantedDescription: 'iPhone 16',
            filterStatus: FilterStatuses.pending,
          ),
          onTap: () {},
          onDial: (_) async {},
        ),
      ),
    ));
    expect(find.textContaining('نوع المبيع'), findsOneWidget);
    expect(find.textContaining('شنو يريد'), findsNothing);
    expect(find.textContaining('سعر'), findsNothing);
  });

  testWidgets('onHold card shows note and rejected shows reason', (tester) async {
    await tester.pumpWidget(MaterialApp(
      home: Scaffold(
        body: Column(
          children: [
            RequestCard(
              request: FilterRequest(
                id: 1,
                customerName: 'أ',
                filterStatus: FilterStatuses.onHold,
                filterNote: 'اتصل غداً',
              ),
              onTap: () {},
              onDial: (_) async {},
            ),
            RequestCard(
              request: FilterRequest(
                id: 2,
                customerName: 'ب',
                filterStatus: FilterStatuses.rejected,
                rejectReason: 'رقم خاطئ',
              ),
              onTap: () {},
              onDial: (_) async {},
            ),
          ],
        ),
      ),
    ));
    expect(find.textContaining('ملاحظة التعليق'), findsOneWidget);
    expect(find.textContaining('اتصل غداً'), findsOneWidget);
    expect(find.textContaining('سبب الرفض'), findsOneWidget);
    expect(find.textContaining('رقم خاطئ'), findsOneWidget);
  });

  testWidgets('hold requires note', (tester) async {
    await tester.pumpWidget(MaterialApp(
      home: DetailScreen(
        requestId: 1,
        cityValue: 'baghdad-karkh',
        repository: _FakeRepo(),
        dialer: (_) async => true,
      ),
    ));
    await tester.pumpAndSettle();
    await tester.tap(find.text('معلق'));
    await tester.pumpAndSettle();
    expect(find.text('ملاحظة التعليق *'), findsOneWidget);
    await tester.tap(find.text('تأكيد التعليق'));
    await tester.pumpAndSettle();
    expect(find.text('ملاحظة التعليق مطلوبة'), findsOneWidget);
  });

  testWidgets('hold sends note', (tester) async {
    final repo = _FakeRepo();
    await tester.pumpWidget(MaterialApp(
      home: DetailScreen(
        requestId: 1,
        cityValue: 'baghdad-karkh',
        repository: repo,
        dialer: (_) async => true,
      ),
    ));
    await tester.pumpAndSettle();
    await tester.tap(find.text('معلق'));
    await tester.pumpAndSettle();
    await tester.enterText(find.byType(TextField), 'الزبون مشغول');
    await tester.tap(find.text('تأكيد التعليق'));
    await tester.pumpAndSettle();
    expect(repo.lastHoldNote, 'الزبون مشغول');
  });

  testWidgets('app title and SafeArea', (tester) async {
    await tester.pumpWidget(MaterialApp(
      home: HomeScreen(repository: _FakeRepo(), dialer: (_) async => true),
    ));
    await tester.pumpAndSettle();
    expect(find.text('تطبيق فلترة المبيعات'), findsOneWidget);
    expect(find.byType(SafeArea), findsWidgets);
  });

  testWidgets('ready card opens from dashboard', (tester) async {
    await tester.pumpWidget(MaterialApp(
      home: HomeScreen(repository: _FakeRepo(), dialer: (_) async => true),
    ));
    await tester.pumpAndSettle();
    await tester.tap(find.text('جاهز للبيع'));
    await tester.pumpAndSettle();
    expect(find.text('أحمد علي'), findsOneWidget);
    expect(find.textContaining('نوع المبيع'), findsOneWidget);
  });

  testWidgets('reject requires reason', (tester) async {
    await tester.pumpWidget(MaterialApp(
      home: DetailScreen(
        requestId: 1,
        cityValue: 'baghdad-karkh',
        repository: _FakeRepo(),
        dialer: (_) async => true,
      ),
    ));
    await tester.pumpAndSettle();
    await tester.tap(find.text('مرفوض'));
    await tester.pumpAndSettle();
    expect(find.text('سبب الرفض *'), findsOneWidget);
    await tester.tap(find.text('تأكيد الرفض'));
    await tester.pumpAndSettle();
    expect(find.text('سبب الرفض مطلوب'), findsOneWidget);
  });
}
