import 'package:flutter_secure_storage/flutter_secure_storage.dart';

class AuthTokens {
  const AuthTokens({required this.accessToken, required this.refreshToken, this.expiresAt});

  final String accessToken;
  final String refreshToken;
  final DateTime? expiresAt;
}

/// Stores the session tokens in the platform keystore/keychain.
/// Keeps an in-memory copy so the interceptor does not hit secure storage on every request.
class TokenStorage {
  TokenStorage([FlutterSecureStorage? storage])
      : _storage = storage ??
            const FlutterSecureStorage(
              iOptions: IOSOptions(accessibility: KeychainAccessibility.first_unlock_this_device),
            );

  final FlutterSecureStorage _storage;
  AuthTokens? _cache;
  bool _loaded = false;

  static const _kAccess = 'auth.accessToken';
  static const _kRefresh = 'auth.refreshToken';
  static const _kExpires = 'auth.expiresAt';

  AuthTokens? get current => _cache;

  Future<AuthTokens?> read() async {
    if (_loaded) return _cache;
    try {
      final access = await _storage.read(key: _kAccess);
      final refresh = await _storage.read(key: _kRefresh);
      final expires = await _storage.read(key: _kExpires);
      if (access != null && refresh != null) {
        _cache = AuthTokens(
          accessToken: access,
          refreshToken: refresh,
          expiresAt: expires == null ? null : DateTime.tryParse(expires),
        );
      }
    } catch (_) {
      // Corrupted keystore (e.g. after a backup restore): treat as logged out.
      _cache = null;
    }
    _loaded = true;
    return _cache;
  }

  Future<void> save(AuthTokens tokens) async {
    _cache = tokens;
    _loaded = true;
    await _storage.write(key: _kAccess, value: tokens.accessToken);
    await _storage.write(key: _kRefresh, value: tokens.refreshToken);
    if (tokens.expiresAt != null) {
      await _storage.write(key: _kExpires, value: tokens.expiresAt!.toIso8601String());
    } else {
      await _storage.delete(key: _kExpires);
    }
  }

  Future<void> clear() async {
    _cache = null;
    _loaded = true;
    try {
      await _storage.delete(key: _kAccess);
      await _storage.delete(key: _kRefresh);
      await _storage.delete(key: _kExpires);
    } catch (_) {}
  }
}
