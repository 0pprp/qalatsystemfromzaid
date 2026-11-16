import 'dart:async';
import 'dart:convert';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_exception.dart';
import '../../../core/network/json.dart';
import '../../../core/providers.dart';
import '../../../core/push/push_service.dart';
import '../../../core/storage/token_storage.dart';
import 'auth_models.dart';
import 'auth_repository.dart';

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

  AuthRepository get _repo => ref.read(authRepositoryProvider);

  Future<void> _bootstrap() async {
    final tokens = await ref.read(tokenStorageProvider).read();
    if (tokens == null) {
      state = const AuthState.guest();
      return;
    }
    final cached = _readCachedMe();
    if (cached != null) state = AuthState.authenticated(cached);
    try {
      final me = await _repo.me().timeout(const Duration(seconds: 12));
      _setUser(me);
      // Also asks agents signed in before push existed (Android shows the prompt only while undecided).
      unawaited(ref.read(pushServiceProvider).onLoggedIn(requestPermission: true));
    } on ApiException catch (e) {
      if (e.isUnauthorized || e.code == 'account_suspended' || e.statusCode == 403) {
        await _clearLocal();
      } else if (cached == null) {
        // Offline and nothing cached: browse as guest for now (tokens are kept for the next launch).
        state = const AuthState.guest();
      }
    } on TimeoutException {
      if (cached == null) state = const AuthState.guest();
    }
  }

  Me? _readCachedMe() {
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

  /// Called after a successful `auth/login`.
  Future<void> completeLogin(AuthTokens tokens, Me me) async {
    await ref.read(tokenStorageProvider).save(tokens);
    _setUser(me);
    unawaited(ref.read(pushServiceProvider).onLoggedIn(requestPermission: true));
  }

  Future<void> refreshMe() async {
    if (!state.isLoggedIn) return;
    try {
      _setUser(await _repo.me());
    } catch (_) {}
  }

  void updateUser(Me me) => _setUser(me);

  void onUserRefreshed(Json user) {
    if (state.isLoggedIn) _setUser(Me.fromJson(user));
  }

  void onSessionExpired() {
    ref.read(sharedPrefsProvider).remove(_kCachedMe);
    state = const AuthState.guest();
  }

  Future<void> logout() async {
    final tokens = ref.read(tokenStorageProvider).current;
    final push = ref.read(pushServiceProvider);
    if (tokens != null) {
      try {
        await _repo
            .logout(refreshToken: tokens.refreshToken, pushToken: push.currentToken)
            .timeout(const Duration(seconds: 8));
      } catch (_) {
        // Logging out must always succeed locally.
      }
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

final isLoggedInProvider = Provider<bool>((ref) => ref.watch(authControllerProvider).isLoggedIn);

final currentUserProvider = Provider<Me?>((ref) => ref.watch(authControllerProvider).user);
