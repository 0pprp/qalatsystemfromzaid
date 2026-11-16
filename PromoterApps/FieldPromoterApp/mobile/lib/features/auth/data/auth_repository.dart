import 'dart:io' show Platform;

import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_client.dart';
import '../../../core/network/json.dart';
import '../../../core/providers.dart';
import '../../../core/storage/token_storage.dart';
import 'auth_models.dart';

String get devicePlatform {
  if (kIsWeb) return 'web';
  return Platform.isIOS ? 'ios' : 'android';
}

class AuthRepository {
  AuthRepository(this._api, this._appVersion);

  final ApiClient _api;
  final String _appVersion;

  /// Username + password given by the dashboard. Returns the session tokens and the user.
  Future<(AuthTokens, Me)> login({required String username, required String password}) async {
    final data = await _api.post('auth/login', body: {
      'username': username.trim(),
      'password': password,
      'device': {'platform': devicePlatform, 'appVersion': _appVersion},
    });
    final json = asJson(data);
    final tokens = AuthTokens(
      accessToken: asString(json['accessToken']),
      refreshToken: asString(json['refreshToken']),
      expiresAt: asDate(json['accessTokenExpiresAt']),
    );
    return (tokens, Me.fromJson(asJson(json['user'])));
  }

  Future<void> logout({required String refreshToken, String? pushToken}) async {
    await _api.post('auth/logout', body: {
      'refreshToken': refreshToken,
      'pushToken': ?pushToken,
    });
  }

  Future<Me> me() async => Me.fromJson(asJson(await _api.get('me')));

  Future<void> registerDevice(String pushToken) async {
    await _api.post('me/devices', body: {
      'platform': devicePlatform,
      'pushToken': pushToken,
      'appVersion': _appVersion,
    });
  }

  Future<void> unregisterDevice(String pushToken) async {
    await _api.delete('me/devices/${Uri.encodeComponent(pushToken)}');
  }
}

final authRepositoryProvider = Provider(
  (ref) => AuthRepository(ref.watch(apiClientProvider), ref.watch(packageInfoProvider).version),
);
