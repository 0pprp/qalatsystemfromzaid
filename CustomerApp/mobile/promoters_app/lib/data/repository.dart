import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:uuid/uuid.dart';

import '../core/network/api_client.dart';
import '../core/network/api_exception.dart';
import '../core/network/json.dart';
import '../core/providers.dart';
import '../core/storage/token_storage.dart';
import 'models.dart';

/// Every call the app makes to BE_PromotersApp (`/api/v1/`).
class Repository {
  Repository(this._api, this._appVersion);

  final ApiClient _api;
  final String _appVersion;

  // Auth
  Future<(AuthTokens, Me)> login(String username, String password, {String? pushToken}) async {
    final j = asJson(await _api.post('auth/login', body: {
      'username': username.trim(),
      'password': password,
      'device': {'platform': 'android', 'appVersion': _appVersion, 'pushToken': ?pushToken},
    }));
    return (
      AuthTokens(accessToken: asString(j['accessToken']), refreshToken: asString(j['refreshToken']), expiresAt: asDate(j['accessTokenExpiresAt'])),
      Me.fromJson(asJson(j['user'])),
    );
  }

  Future<void> logout(String refreshToken, {String? pushToken}) =>
      _api.post('auth/logout', body: {'refreshToken': refreshToken, 'pushToken': ?pushToken});

  Future<Me> me() async => Me.fromJson(asJson(await _api.get('me')));

  Future<RemoteConfig> config() async => RemoteConfig.fromJson(asJson(await _api.get('app/config')));

  Future<Overview> overview() async => Overview.fromJson(asJson(await _api.get('me/overview')));

  // Catalog
  Future<List<Product>> products() async => asList(await _api.get('products'), Product.fromJson);

  Future<List<Branch>> branches() async => asList(await _api.get('branches'), Branch.fromJson);

  // Orders
  Future<OrderDetail> createOrder({
    required int productId,
    required int quantity,
    required String customerName,
    required String customerPhone,
    required int? cityValue,
    required String address,
    String? notes,
    required String idempotencyKey,
  }) async =>
      OrderDetail.fromJson(asJson(await _api.post('orders', headers: {'Idempotency-Key': idempotencyKey}, body: {
        'productId': productId,
        'quantity': quantity,
        'customerName': customerName,
        'customerPhone': customerPhone,
        'cityValue': cityValue,
        'address': address,
        'notes': notes,
      })));

  Future<Paged<OrderSummary>> orders({String? status, int page = 1}) async =>
      Paged.fromJson(await _api.get('orders', query: {'status': status, 'page': page, 'pageSize': 20}), OrderSummary.fromJson);

  Future<Map<String, int>> orderCounts() async =>
      asJson(await _api.get('orders/counts')).map((k, v) => MapEntry(k, asInt(v)));

  Future<OrderDetail> order(int id) async => OrderDetail.fromJson(asJson(await _api.get('orders/$id')));

  Future<OrderDetail> cancelOrder(int id) async => OrderDetail.fromJson(asJson(await _api.post('orders/$id/cancel')));

  // Wallet
  Future<WalletSummary> wallet() async => WalletSummary.fromJson(asJson(await _api.get('wallet')));

  Future<Paged<WalletEntry>> entries({int page = 1}) async =>
      Paged.fromJson(await _api.get('wallet/entries', query: {'page': page, 'pageSize': 20}), WalletEntry.fromJson);

  Future<Paged<Payout>> payouts({int page = 1}) async =>
      Paged.fromJson(await _api.get('wallet/payouts', query: {'page': page, 'pageSize': 20}), Payout.fromJson);

  Future<(Payout, List<ChatMessage>)> requestPayout({required int amount, required String method, required String accountNumber}) async {
    final j = asJson(await _api.post('wallet/payouts', body: {'amount': amount, 'method': method, 'accountNumber': accountNumber}));
    return (Payout.fromJson(asJson(j['payout'])), asList(j['messages'], ChatMessage.fromJson));
  }

  Future<(Payout, List<ChatMessage>)> payout(int id) async {
    final j = asJson(await _api.get('wallet/payouts/$id'));
    return (Payout.fromJson(asJson(j['payout'])), asList(j['messages'], ChatMessage.fromJson));
  }

  Future<List<ChatMessage>> messagesAfter(int payoutId, int afterId) async =>
      asList(await _api.get('wallet/payouts/$payoutId/messages', query: {'afterId': afterId}), ChatMessage.fromJson);

  Future<ChatMessage> sendMessage(int payoutId, String body) async =>
      ChatMessage.fromJson(asJson(await _api.post('wallet/payouts/$payoutId/messages', body: {'body': body})));

  /// A photo or document in the withdrawal chat (15 MB at most), with optional text.
  Future<ChatMessage> sendAttachment(int payoutId, {required Uint8List bytes, required String fileName, String? body}) async {
    try {
      final res = await _api.dio.post<dynamic>(
        'wallet/payouts/$payoutId/messages/attachment',
        data: FormData.fromMap({
          'file': MultipartFile.fromBytes(bytes, filename: fileName),
          if (body != null && body.isNotEmpty) 'body': body,
        }),
        options: Options(contentType: 'multipart/form-data', sendTimeout: const Duration(minutes: 3), receiveTimeout: const Duration(minutes: 3)),
      );
      return ChatMessage.fromJson(asJson(res.data));
    } catch (e) {
      throw ApiException.from(e);
    }
  }

  /// Chat attachments and the transfer receipt are private: downloaded with the sign-in token.
  Future<Uint8List> attachmentBytes(int payoutId, int messageId) => _bytes('wallet/payouts/$payoutId/messages/$messageId/attachment');

  Future<Uint8List> receiptBytes(int payoutId) => _bytes('wallet/payouts/$payoutId/receipt');

  Future<Uint8List> _bytes(String path) async {
    try {
      final res = await _api.dio.get<List<int>>(path,
          options: Options(responseType: ResponseType.bytes, receiveTimeout: const Duration(minutes: 3)));
      return Uint8List.fromList(res.data ?? const []);
    } catch (e) {
      throw ApiException.from(e);
    }
  }

  // Team
  Future<Team> team() async => Team.fromJson(asJson(await _api.get('me/team')));

  Future<TeamMember> createMember({required String username, required String password, required String phone, required double cutPercent}) async =>
      TeamMember.fromJson(asJson(await _api.post('me/team', body: {'username': username, 'password': password, 'phone': phone, 'cutPercent': cutPercent})));

  Future<TeamMember> updateMemberCut(int id, double cutPercent) async =>
      TeamMember.fromJson(asJson(await _api.put('me/team/$id', body: {'cutPercent': cutPercent})));

  // Materials
  Future<List<MarketingMaterial>> materials() async => asList(await _api.get('materials'), MarketingMaterial.fromJson);

  // Notifications
  Future<Paged<AppNotification>> notifications({int page = 1}) async =>
      Paged.fromJson(await _api.get('notifications', query: {'page': page, 'pageSize': 20}), AppNotification.fromJson);

  Future<int> unreadCount() async => asInt(asJson(await _api.get('notifications/unread-count'))['count']);

  Future<void> markRead(int id) => _api.post('notifications/$id/read');

  Future<void> markAllRead() => _api.post('notifications/read-all');

  static String newIdempotencyKey() => const Uuid().v4();
}

final repositoryProvider = Provider((ref) => Repository(ref.watch(apiClientProvider), ref.watch(packageInfoProvider).version));
