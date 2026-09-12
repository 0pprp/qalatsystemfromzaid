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

class _RecordingRepo extends FilterRepository {
  String? lastCityValue;
  String? lastListCityValue;

  @override
  Future<List<FilterCity>> myCities() async => [
        FilterCity(cityValue: 'baghdad-karkh', cityName: 'بغداد الكرخ'),
        FilterCity(cityValue: 'najaf-demo', cityName: 'النجف'),
      ];

  @override
  Future<List<FilterRequest>> list({required String status, String? cityValue, int page = 1}) async {
    lastListCityValue = cityValue;
    return [
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
  }

  @override
  Future<FilterRequest> get(String cityValue, int id) async {
    lastCityValue = cityValue;
    return FilterRequest(
      id: id,
      customerName: 'أحمد علي',
      customerPhone: '07701234567',
      cityName: 'بغداد الكرخ',
      customerAddress: 'حي الجامعة',
      wantedDescription: 'iPhone 16',
      filterStatus: FilterStatuses.pending,
    );
  }

  @override
  Future<FilterRequest> hold(String cityValue, int id, {String? note}) async {
    lastCityValue = cityValue;
    return get(cityValue, id);
  }

  @override
  Future<FilterRequest> ready(String cityValue, int id, {String? note}) async {
    lastCityValue = cityValue;
    return get(cityValue, id);
  }

  @override
  Future<FilterRequest> reject(String cityValue, int id, {required String reason, String? note}) async {
    lastCityValue = cityValue;
    return get(cityValue, id);
  }
}

class _SingleCityRepo extends _RecordingRepo {
  @override
  Future<List<FilterCity>> myCities() async => [
        FilterCity(cityValue: 'baghdad-karkh', cityName: 'بغداد الكرخ'),
      ];
}

void main() {
  testWidgets('login has no branch dropdown', (tester) async {
    await tester.pumpWidget(const MaterialApp(home: LoginScreen()));
    await tester.pumpAndSettle();
    expect(find.text('الفرع'), findsNothing);
    expect(find.byType(DropdownButtonFormField<dynamic>), findsNothing);
    expect(find.text('اسم المستخدم'), findsOneWidget);
  });

  test('login endpoint targets gateway Auth/LoginSalesFilter', () {
    final base = AppEnv.normalizeBase('http://127.0.0.1:5280/api/');
    expect(base.endsWith('/api/'), isTrue);
    expect('${base}Auth/LoginSalesFilter', 'http://127.0.0.1:5280/api/Auth/LoginSalesFilter');
  });

  testWidgets('tabs four arabic labels', (tester) async {
    await tester.pumpWidget(MaterialApp(
      home: HomeScreen(repository: _SingleCityRepo(), dialer: (_) async => true),
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
    expect(find.textContaining('سعر'), findsNothing);
  });

  testWidgets('reject requires reason', (tester) async {
    await tester.pumpWidget(MaterialApp(
      home: DetailScreen(
        requestId: 1,
        cityValue: 'baghdad-karkh',
        repository: _SingleCityRepo(),
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
      home: HomeScreen(repository: _SingleCityRepo(), dialer: (_) async => true),
    ));
    await tester.pumpAndSettle();
    expect(find.text('بغداد الكرخ'), findsWidgets);
  });

  testWidgets('changing city updates cityValue in list request', (tester) async {
    final repo = _RecordingRepo();
    await tester.pumpWidget(MaterialApp(
      home: HomeScreen(repository: repo, dialer: (_) async => true),
    ));
    await tester.pumpAndSettle();
    await tester.tap(find.byType(DropdownButtonFormField<String>));
    await tester.pumpAndSettle();
    await tester.tap(find.text('النجف').last);
    await tester.pumpAndSettle();
    expect(repo.lastListCityValue, 'najaf-demo');
  });

  testWidgets('details actions send cityValue', (tester) async {
    final repo = _RecordingRepo();
    await tester.pumpWidget(MaterialApp(
      home: DetailScreen(
        requestId: 9,
        cityValue: 'baghdad-karkh',
        repository: repo,
        dialer: (_) async => true,
      ),
    ));
    await tester.pumpAndSettle();
    expect(repo.lastCityValue, 'baghdad-karkh');
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
      home: HomeScreen(repository: _SingleCityRepo(), dialer: (_) async => true),
    ));
    await tester.pumpAndSettle();
    expect(find.byType(SafeArea), findsWidgets);
    await tester.pumpWidget(const MaterialApp(home: LoginScreen()));
    await tester.pumpAndSettle();
    expect(find.byType(SafeArea), findsWidgets);
  });
}
