import 'dart:async';
import 'dart:convert';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/api_exception.dart';
import '../../core/network/json.dart';
import '../../core/providers.dart';
import '../../core/push_service.dart';
import '../../data/models.dart';
import '../../data/repository.dart';

enum AuthStatus { unknown, guest, authenticated }

class AuthState {
  const AuthState._(this.status, this.user);
  const AuthState.unknown() : this._(AuthStatus.unknown, null);
  const AuthState.guest() : this._(AuthStatus.guest, null);
  const AuthState.authenticated(Me user) : this._(AuthStatus.authenticated, user);

  final AuthStatus status;
  final Me? user;

  bool get isLoggedIn => status == AuthStatus.authenticated;
  bool get isKnown => status != AuthStatus.unknown;
}

class AuthController extends Notifier<AuthState> {
  static const _kCachedMe = 'auth.cachedMe';

  @override
  AuthState build() {
    Future.microtask(_bootstrap);
    return const AuthState.unknown();
  }

  Repository get _repo => ref.read(repositoryProvider);

  Future<void> _bootstrap() async {
    final tokens = await ref.read(tokenStorageProvider).read();
    if (tokens == null) {
      state = const AuthState.guest();
      return;
    }
    final cached = _readCached();
    if (cached != null) state = AuthState.authenticated(cached);
    try {
      _setUser(await _repo.me().timeout(const Duration(seconds: 12)));
      unawaited(ref.read(pushServiceProvider).onLoggedIn());
    } on ApiException catch (e) {
      if (e.isUnauthorized || e.statusCode == 403) {
        await _clearLocal();
      } else if (cached == null) {
        state = const AuthState.guest();
      }
    } on TimeoutException {
      if (cached == null) state = const AuthState.guest();
    }
  }

  Me? _readCached() {
    final raw = ref.read(sharedPrefsProvider).getString(_kCachedMe);
    if (raw == null) return null;
    try {
      return Me.fromJson(asJson(jsonDecode(raw)));
    } catch (_) {
      return null;
    }
  }

  void _setUser(Me me) {
    state = AuthState.authenticated(me);
    ref.read(sharedPrefsProvider).setString(_kCachedMe, jsonEncode(me.toJson()));
  }

  Future<void> login(String username, String password) async {
    final push = ref.read(pushServiceProvider);
    final (tokens, me) = await _repo.login(username, password, pushToken: await push.tokenForLogin());
    await ref.read(tokenStorageProvider).save(tokens);
    _setUser(me);
    unawaited(push.onLoggedIn());
  }

  /// Reloads the account (e.g. after the dashboard made it a full account).
  Future<void> refreshMe() async {
    if (!state.isLoggedIn) return;
    try {
      _setUser(await _repo.me());
    } catch (_) {}
  }

  void onUserRefreshed(Json user) {
    if (state.isLoggedIn) _setUser(Me.fromJson(user));
  }

  void onSessionExpired() {
    ref.read(sharedPrefsProvider).remove(_kCachedMe);
    state = const AuthState.guest();
  }

  Future<void> logout() async {
    final tokens = ref.read(tokenStorageProvider).current;
    if (tokens != null) {
      try {
        await _repo.logout(tokens.refreshToken, pushToken: ref.read(pushServiceProvider).currentToken).timeout(const Duration(seconds: 8));
      } catch (_) {}
    }
    await _clearLocal();
  }

  Future<void> _clearLocal() async {
    await ref.read(tokenStorageProvider).clear();
    await ref.read(sharedPrefsProvider).remove(_kCachedMe);
    ref.read(pushServiceProvider).onLoggedOut();
    state = const AuthState.guest();
  }
}

final authControllerProvider = NotifierProvider<AuthController, AuthState>(AuthController.new);

final currentUserProvider = Provider<Me?>((ref) => ref.watch(authControllerProvider).user);
