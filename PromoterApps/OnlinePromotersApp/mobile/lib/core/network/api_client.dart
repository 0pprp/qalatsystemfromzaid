import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';

import '../config/app_config.dart';
import '../storage/token_storage.dart';
import 'api_exception.dart';
import 'json.dart';

/// Thin wrapper over Dio. Every method throws [ApiException] (never [DioException]).
class ApiClient {
  ApiClient({
    required this.tokens,
    required this.onSessionExpired,
    this.onTokensRefreshed,
    Interceptor? fakeBackend,
    String? baseUrl,
  }) {
    final options = BaseOptions(
      baseUrl: baseUrl ?? AppConfig.apiBaseUrl,
      connectTimeout: AppConfig.connectTimeout,
      receiveTimeout: AppConfig.receiveTimeout,
      sendTimeout: AppConfig.receiveTimeout,
      contentType: Headers.jsonContentType,
      responseType: ResponseType.json,
      headers: {'Accept': 'application/json', 'Accept-Language': 'ar-IQ'},
    );
    _refreshDio = Dio(options);
    dio = Dio(options);
    dio.interceptors.add(AuthInterceptor(this));
    if (fakeBackend != null) {
      _refreshDio.interceptors.add(fakeBackend);
      dio.interceptors.add(fakeBackend);
    }
    if (kDebugMode && fakeBackend == null) {
      dio.interceptors.add(LogInterceptor(
        requestBody: true,
        responseBody: false,
        logPrint: (o) => debugPrint(o.toString()),
      ));
    }
  }

  final TokenStorage tokens;

  /// Called when the refresh token is rejected; the app should switch to guest mode.
  final void Function() onSessionExpired;

  /// Called with the `user` object returned by `auth/refresh`.
  final void Function(Json user)? onTokensRefreshed;

  late final Dio dio;
  late final Dio _refreshDio;

  Future<dynamic> get(String path, {Map<String, dynamic>? query}) =>
      _run(() => dio.get<dynamic>(path, queryParameters: _clean(query)));

  Future<dynamic> post(String path, {Object? body, Map<String, dynamic>? headers}) => _run(
        () => dio.post<dynamic>(path, data: body ?? const {}, options: Options(headers: headers)),
      );

  Future<dynamic> put(String path, {Object? body}) =>
      _run(() => dio.put<dynamic>(path, data: body ?? const {}));

  Future<dynamic> delete(String path, {Object? body}) =>
      _run(() => dio.delete<dynamic>(path, data: body));

  Future<dynamic> _run(Future<Response<dynamic>> Function() call) async {
    try {
      final res = await call();
      return res.data;
    } catch (e) {
      throw ApiException.from(e);
    }
  }

  static Map<String, dynamic>? _clean(Map<String, dynamic>? q) {
    if (q == null) return null;
    final out = <String, dynamic>{};
    q.forEach((k, v) {
      if (v == null) return;
      if (v is String && v.isEmpty) return;
      out[k] = v;
    });
    return out;
  }

  Future<Response<dynamic>> retryRaw(RequestOptions req) => _refreshDio.fetch<dynamic>(req);

  /// Exchanges the refresh token for a new pair. Returns false when the session is dead.
  /// Throws [ApiException] on network problems (session kept).
  Future<bool> refreshTokens() async {
    final current = tokens.current;
    if (current == null) return false;
    try {
      final res = await _refreshDio.post<dynamic>(
        'auth/refresh',
        data: {'refreshToken': current.refreshToken},
      );
      final json = asJson(res.data);
      final access = asStringOrNull(json['accessToken']);
      final refresh = asStringOrNull(json['refreshToken']);
      if (access == null || refresh == null) return false;
      await tokens.save(AuthTokens(
        accessToken: access,
        refreshToken: refresh,
        expiresAt: asDate(json['accessTokenExpiresAt']),
      ));
      final user = json['user'];
      if (user is Map) onTokensRefreshed?.call(Map<String, dynamic>.from(user));
      return true;
    } on DioException catch (e) {
      final status = e.response?.statusCode;
      if (status != null && status >= 400 && status < 500) return false;
      throw ApiException.from(e);
    }
  }
}

/// Adds the bearer token and transparently refreshes it once on 401.
/// [QueuedInterceptor] serialises error handling so parallel 401s trigger a single refresh.
class AuthInterceptor extends QueuedInterceptor {
  AuthInterceptor(this.client);

  final ApiClient client;
  static const _retriedKey = 'auth_retried';

  static bool _isAuthEndpoint(String path) =>
      path.startsWith('auth/otp') || path.startsWith('auth/refresh');

  @override
  void onRequest(RequestOptions options, RequestInterceptorHandler handler) {
    final token = client.tokens.current?.accessToken;
    if (token != null && !_isAuthEndpoint(options.path)) {
      options.headers['Authorization'] = 'Bearer $token';
    }
    handler.next(options);
  }

  @override
  Future<void> onError(DioException err, ErrorInterceptorHandler handler) async {
    final req = err.requestOptions;
    final sentAuth = req.headers['Authorization'] as String?;
    final shouldTry = err.response?.statusCode == 401 &&
        sentAuth != null &&
        !_isAuthEndpoint(req.path) &&
        req.extra[_retriedKey] != true;
    if (!shouldTry) return handler.next(err);

    try {
      final current = client.tokens.current?.accessToken;
      // Another queued request may already have refreshed the token.
      final alreadyRefreshed = current != null && 'Bearer $current' != sentAuth;
      if (!alreadyRefreshed) {
        final ok = await client.refreshTokens();
        if (!ok) {
          await client.tokens.clear();
          client.onSessionExpired();
          return handler.next(err);
        }
      }
      final newToken = client.tokens.current?.accessToken;
      req.headers['Authorization'] = 'Bearer $newToken';
      req.extra[_retriedKey] = true;
      // Retry on the plain Dio (no auth interceptor) to avoid re-entering this queue.
      final response = await client.retryRaw(req);
      return handler.resolve(response);
    } on DioException catch (e) {
      return handler.next(e);
    } catch (_) {
      return handler.next(err);
    }
  }
}
