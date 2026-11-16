import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/config/app_config.dart';
import '../../../core/network/api_client.dart';
import '../../../core/network/json.dart';
import '../../../core/providers.dart';
import 'order_models.dart';

class OrdersRepository {
  OrdersRepository(this._api);

  final ApiClient _api;

  /// [idempotencyKey] must be generated once per submit attempt and reused on retries of that attempt.
  Future<OrderDetail> create(CreateOrderRequest request, {required String idempotencyKey}) async {
    final data = await _api.post('orders', body: request.toJson(), headers: {'Idempotency-Key': idempotencyKey});
    return OrderDetail.fromJson(asJson(data));
  }

  /// Uploads the shop photo and returns the URL to send with the order.
  Future<String> uploadShopPhoto(String path) async {
    final form = FormData.fromMap({'file': await MultipartFile.fromFile(path, filename: 'shop.jpg')});
    // Demo mode keeps the photo on the phone: the fake backend reads its path from this header.
    final data = await _api.post('orders/shop-photo', body: form, headers: AppConfig.useFakeApi ? {'X-Local-Path': path} : null);
    return asString(asJson(data)['url']);
  }

  /// [statuses] filters to any of them (one tab of the sales screen).
  Future<Paged<OrderSummary>> list({List<OrderStatus>? statuses, int page = 1}) async {
    final data = await _api.get('orders', query: {
      'status': statuses == null || statuses.isEmpty ? null : statuses.map((s) => s.api).join(','),
      'page': page,
    });
    return Paged.fromJson(data, OrderSummary.fromJson);
  }

  /// Number of my orders per status (tab badges, home stats).
  Future<Map<OrderStatus, int>> counts() async {
    final j = asJson(await _api.get('orders/counts'));
    return {for (final s in OrderStatus.values) s: asInt(j[s.api])};
  }

  /// «الرئيسية».
  Future<AgentOverview> overview() async => AgentOverview.fromJson(asJson(await _api.get('me/overview')));

  /// Company branches for the order's «الفرع».
  Future<List<Branch>> branches() async => asList(await _api.get('branches'), Branch.fromJson);

  Future<OrderDetail> detail(int id) async => OrderDetail.fromJson(asJson(await _api.get('orders/$id')));

  Future<OrderDetail> cancel(int id, {String? reason}) async =>
      OrderDetail.fromJson(asJson(await _api.post('orders/$id/cancel', body: {'reason': reason})));
}

final ordersRepositoryProvider = Provider((ref) => OrdersRepository(ref.watch(apiClientProvider)));

final orderCountsProvider = FutureProvider.autoDispose<Map<OrderStatus, int>>((ref) {
  ref.watch(ordersVersionProvider);
  return ref.watch(ordersRepositoryProvider).counts();
});

final overviewProvider = FutureProvider.autoDispose<AgentOverview>((ref) {
  ref.watch(ordersVersionProvider);
  return ref.watch(ordersRepositoryProvider).overview();
});

final branchesProvider = FutureProvider<List<Branch>>((ref) => ref.watch(ordersRepositoryProvider).branches());

final orderDetailProvider =
    FutureProvider.autoDispose.family<OrderDetail, int>((ref, id) => ref.watch(ordersRepositoryProvider).detail(id));

/// Bumped after creating/cancelling an order so lists reload.
class OrdersVersion extends Notifier<int> {
  @override
  int build() => 0;

  void bump() => state++;
}

final ordersVersionProvider = NotifierProvider<OrdersVersion, int>(OrdersVersion.new);
