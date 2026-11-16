import 'package:customer_app/core/fake/fake_backend.dart';
import 'package:customer_app/core/network/api_client.dart';
import 'package:customer_app/core/network/api_exception.dart';
import 'package:customer_app/core/storage/token_storage.dart';
import 'package:customer_app/features/auth/data/auth_repository.dart';
import 'package:customer_app/features/orders/data/order_models.dart';
import 'package:customer_app/features/orders/data/orders_repository.dart';
import 'package:customer_app/features/work/data/prospects_repository.dart';
import 'package:customer_app/features/work/data/work_repository.dart';
import 'package:flutter_test/flutter_test.dart';

class MemoryTokenStorage extends TokenStorage {
  AuthTokens? _t;

  @override
  AuthTokens? get current => _t;

  @override
  Future<AuthTokens?> read() async => _t;

  @override
  Future<void> save(AuthTokens tokens) async => _t = tokens;

  @override
  Future<void> clear() async => _t = null;
}

/// Runs the repositories (and therefore all model parsers) against the in-memory fake backend.
void main() {
  late MemoryTokenStorage tokens;
  late ApiClient api;

  setUp(() {
    tokens = MemoryTokenStorage();
    api = ApiClient(
      tokens: tokens,
      onSessionExpired: () {},
      fakeBackend: FakeBackend(latency: Duration.zero),
      baseUrl: 'http://fake/api/v1/',
    );
  });

  Future<void> login(String username) async {
    final (t, me) = await AuthRepository(api, '1.0.0').login(username: username, password: '123456');
    await tokens.save(t);
    expect(me.username, username);
  }

  test('branches and the home overview parse', () async {
    final repo = OrdersRepository(api);
    expect(await repo.branches(), isNotEmpty);
    await login('agent1');
    final o = await repo.overview();
    expect(o.recentOrders, isNotEmpty);
    expect(o.recentOrders.length, lessThanOrEqualTo(5));
    expect(o.fullyPaid, greaterThan(0));
    expect(o.waitingPayment, greaterThan(0));
    expect(o.openOrders, greaterThan(0));
  });

  test('an order needs a product', () async {
    await login('agent1');
    try {
      await OrdersRepository(api).create(
        const CreateOrderRequest(
          cityValue: 1,
          fullName: 'حيدر كريم جواد',
          phone: '07709998877',
          address: OrderAddress(area: '', details: 'حي الأنصار شارع 20'),
          items: [OrderItem(name: '  ')],
        ),
        idempotencyKey: 'empty',
      );
      fail('should throw');
    } on ApiException catch (e) {
      expect(e.fields.keys, contains('items'));
    }
  });

  test('wrong password is rejected with a clear message', () async {
    try {
      await AuthRepository(api, '1.0.0').login(username: 'agent1', password: 'wrong');
      fail('should throw');
    } on ApiException catch (e) {
      expect(e.statusCode, 401);
      expect(e.code, 'invalid_credentials');
      expect(e.message, contains('كلمة المرور'));
    }
  });

  test('authenticated endpoints require a token', () async {
    expect(() => OrdersRepository(api).list(), throwsA(isA<ApiException>().having((e) => e.statusCode, 'status', 401)));
  });

  test('orders: create, duplicate guard (409) and idempotency', () async {
    await login('agent1');
    final repo = OrdersRepository(api);
    final list = await repo.list();
    expect(list.items, isNotEmpty);

    // The short order: product, full name, phone, branch and address; photo and location are optional.
    const request = CreateOrderRequest(
      cityValue: 1,
      fullName: 'حيدر كريم جواد',
      phone: '07709998877',
      address: OrderAddress(area: '', details: 'حي الأنصار شارع 20'),
      items: [OrderItem(name: 'كرسي مساج')],
    );
    expect(request.toJson()['purchaseType'], 'DailyInstallments');
    final created = await repo.create(request, idempotencyKey: 'k1');
    // Sent straight to the sales department («طلبات البيع» → غير المسندة).
    expect(created.status, OrderStatus.transferred);
    expect(created.canCancel, isFalse);
    expect(created.shopPhotoUrl, isNull);
    expect(created.address?.latitude, isNull);
    expect(created.summary.customerName, 'حيدر كريم جواد');
    expect(created.summary.purchaseType, PurchaseType.dailyInstallments);
    expect(created.items.single.name, 'كرسي مساج');

    final replay = await repo.create(request, idempotencyKey: 'k1');
    expect(replay.id, created.id);

    try {
      await repo.create(request, idempotencyKey: 'k2');
      fail('should throw');
    } on ApiException catch (e) {
      expect(e.code, 'duplicate_open_order');
      expect(e.stringData('orderNumber'), created.summary.orderNumber);
    }

    expect(() => repo.cancel(created.id), throwsA(isA<ApiException>().having((e) => e.statusCode, 'status', 409)));
  });

  test('prospects: agents pin shops and see the pins of others nearby', () async {
    await login('agent1');
    final repo = ProspectsRepository(api);
    final near = await repo.inArea(minLat: 32.0, minLng: 44.3, maxLat: 32.05, maxLng: 44.36);
    expect(near, isNotEmpty);
    expect(near.any((p) => !p.isMine && p.createdByName.contains('زينب')), isTrue);
    expect(near.any((p) => p.isMine), isTrue);

    try {
      await repo.create(const ProspectInput(lat: 32.03, lng: 44.33, shopName: '', interest: 0));
      fail('should throw');
    } on ApiException catch (e) {
      expect(e.fields.keys, containsAll(['shopName', 'interest']));
    }

    final pin = await repo.create(const ProspectInput(
      lat: 32.031, lng: 44.331, shopName: 'محل الأمانة', description: 'أجهزة كهربائية', interest: 4,
      phone: '07801112222', goodsValue: 20000000, lengthM: 6, widthM: 4, agentLat: 32.0305, agentLng: 44.3301,
    ));
    expect(pin.isMine, isTrue);
    expect(pin.agentLat, 32.0305);
    expect(pin.level.label, 'مهتم');
    expect(pin.areaM2, 24);

    await login('agent2');
    final seen = (await repo.inArea(minLat: 32.0, minLng: 44.3, maxLat: 32.05, maxLng: 44.36)).firstWhere((p) => p.id == pin.id);
    expect(seen.isMine, isFalse);
    expect(() => repo.delete(pin.id), throwsA(isA<ApiException>().having((e) => e.statusCode, 'status', 403)));
  });

  test('sales are followed until fully paid', () async {
    await login('agent1');
    final repo = OrdersRepository(api);
    final counts = await repo.counts();
    expect(counts[OrderStatus.sold], greaterThan(0));
    expect(counts[OrderStatus.finished], greaterThan(0));

    final paying = await repo.list(statuses: [OrderStatus.sold]);
    expect(paying.items, isNotEmpty);
    final sale = paying.items.first.sale!;
    expect(sale.remaining, greaterThan(0));
    expect(sale.paid + sale.remaining, sale.total);

    final finished = await repo.list(statuses: [OrderStatus.finished]);
    expect(finished.items.every((o) => o.sale?.progressPercent == 100), isTrue);
    final detail = await repo.detail(finished.items.first.id);
    expect(detail.shopPhotoUrl, isNotNull);
    expect(detail.timeline.last.status, OrderStatus.finished);
  });

  test('work day: start, locations, end', () async {
    await login('agent2');
    final work = WorkRepository(api);
    expect((await work.today()).isWorking, isFalse);
    expect(await work.sendLocations([
      {'latitude': 33.3, 'longitude': 44.4, 'recordedAt': DateTime.now().toUtc().toIso8601String()},
    ]), isFalse);

    final started = await work.start(lat: 33.31, lng: 44.36);
    expect(started.isWorking, isTrue);
    expect(started.current, isNotNull);
    expect(await work.sendLocations([
      {'latitude': 33.312, 'longitude': 44.362, 'recordedAt': DateTime.now().toUtc().toIso8601String()},
    ]), isTrue);

    final ended = await work.end(lat: 33.313, lng: 44.363);
    expect(ended.isWorking, isFalse);
    expect(ended.sessions, hasLength(1));
    expect(ended.track, hasLength(3));
    expect(ended.lastEndedAt, isNotNull);
  });
}
