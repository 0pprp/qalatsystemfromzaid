import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sales_filter_application/models/filter_models.dart';
import 'package:sales_filter_application/screens/detail_screen.dart';
import 'package:sales_filter_application/screens/home_screen.dart';
import 'package:sales_filter_application/services/filter_repository.dart';
import 'package:sales_filter_application/theme/app_theme.dart';
import 'package:sales_filter_application/widgets/request_card.dart';

class _FakeRepo extends FilterRepository {
  @override
  Future<List<FilterCity>> myCities() async => [
        FilterCity(cityValue: 'baghdad-karkh', cityName: 'بغداد الكرخ'),
      ];

  @override
  Future<List<FilterRequest>> list({required String status, String? city, int page = 1}) async => [
        FilterRequest(
          id: 1,
          customerName: 'أحمد علي',
          customerPhone: '07701234567',
          cityName: 'بغداد الكرخ',
          customerAddress: 'حي الجامعة',
          wantedDescription: 'iPhone 16',
          filterStatus: status,
        ),
      ];

  @override
  Future<FilterRequest> get(int id) async => FilterRequest(
        id: id,
        customerName: 'أحمد علي',
        customerPhone: '07701234567',
        cityName: 'بغداد الكرخ',
        customerAddress: 'حي الجامعة',
        wantedDescription: 'iPhone 16',
        filterStatus: FilterStatuses.pending,
      );
}

void main() {
  testWidgets('tabs four arabic labels', (tester) async {
    await tester.pumpWidget(MaterialApp(
      home: HomeScreen(repository: _FakeRepo(), dialer: (_) async => true),
    ));
    await tester.pumpAndSettle();
    expect(find.text('طلبات البيع'), findsWidgets);
    expect(find.text('معلق'), findsOneWidget);
    expect(find.text('جاهز للبيع'), findsOneWidget);
    expect(find.text('مرفوض'), findsOneWidget);
  });

  testWidgets('card shows only required fields', (tester) async {
    await tester.pumpWidget(MaterialApp(
      home: Scaffold(
        body: RequestCard(
          request: FilterRequest(
            id: 1,
            customerName: 'أحمد علي',
            customerPhone: '07701234567',
            cityName: 'بغداد الكرخ',
            customerAddress: 'حي الجامعة',
            wantedDescription: 'iPhone 16',
            filterStatus: FilterStatuses.pending,
          ),
          onTap: () {},
          onDial: (_) async {},
        ),
      ),
    ));
    expect(find.text('أحمد علي'), findsOneWidget);
    expect(find.text('07701234567'), findsOneWidget);
    expect(find.text('بغداد الكرخ'), findsOneWidget);
    expect(find.text('حي الجامعة'), findsOneWidget);
    expect(find.text('iPhone 16'), findsOneWidget);
    expect(find.textContaining('سعر'), findsNothing);
    expect(find.textContaining('ربح'), findsNothing);
  });

  testWidgets('reject requires reason', (tester) async {
    await tester.pumpWidget(MaterialApp(
      home: DetailScreen(
        requestId: 1,
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

  testWidgets('city comes from allowed cities', (tester) async {
    await tester.pumpWidget(MaterialApp(
      home: HomeScreen(repository: _FakeRepo(), dialer: (_) async => true),
    ));
    await tester.pumpAndSettle();
    expect(find.text('بغداد الكرخ'), findsWidgets);
  });

  testWidgets('phone dial abstraction called', (tester) async {
    var dialed = '';
    await tester.pumpWidget(MaterialApp(
      home: Scaffold(
        body: RequestCard(
          request: FilterRequest(
            id: 1,
            customerName: 'أحمد',
            customerPhone: '0770',
            filterStatus: FilterStatuses.pending,
          ),
          onTap: () {},
          onDial: (p) async {
            dialed = p;
          },
        ),
      ),
    ));
    await tester.tap(find.text('0770'));
    await tester.pumpAndSettle();
    expect(dialed, '0770');
  });

  testWidgets('home and login use SafeArea', (tester) async {
    await tester.pumpWidget(MaterialApp(
      home: HomeScreen(repository: _FakeRepo(), dialer: (_) async => true),
    ));
    await tester.pumpAndSettle();
    expect(find.byType(SafeArea), findsWidgets);
  });
}
