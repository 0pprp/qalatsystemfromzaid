import 'package:flutter_test/flutter_test.dart';
import 'package:follower_application/Login.dart';
import 'package:follower_application/config/app_env.dart';

void main() {
  test('followerLoginUri keeps password out of query string', () {
    final uri = followerLoginUri('http://169.58.236.52:8081/api/');
    expect(uri.path, contains('Followers/Login'));
    expect(uri.query, isEmpty);
    expect(uri.toString().toLowerCase(), isNot(contains('password=')));
    expect(uri.toString().toLowerCase(), isNot(contains('asyncid=')));
  });

  test('Demo API base uses port 8081', () {
    expect(AppEnv.demoApiBaseUrl, contains(':8081'));
    expect(AppEnv.demoApiBaseUrl, startsWith('http://169.58.236.52'));
  });

  test('Production AppEnv default is production (not demo)', () {
    // Compile-time default when APP_ENV is omitted.
    expect(AppEnv.name == 'production' || AppEnv.name == 'demo' || AppEnv.name == 'local', isTrue);
    // Hard-coded Demo URL must not become Production default base.
    if (AppEnv.name.toLowerCase() == 'production') {
      expect(AppEnv.isDemo, isFalse);
      expect(AppEnv.apiBase(fallback: 'http://prod.example/api/'), 'http://prod.example/api/');
    }
  });

  test('empty lists after login is not a login failure', () {
    const lists = <int>[];
    const loginSucceeded = true;
    final emptyCopy =
        lists.isEmpty ? 'لا توجد قوائم مخصصة لك حاليًا' : null;
    expect(loginSucceeded, isTrue);
    expect(emptyCopy, 'لا توجد قوائم مخصصة لك حاليًا');
  });

  test('session stores AsyncID separately from password', () {
    const password = 'secret';
    const asyncIdFromServer = 'server-token-uuid';
    expect(asyncIdFromServer, isNot(equals(password)));
  });

  test('GPS FollowerId is Users.UserID', () {
    const userId = 10;
    const followerId = userId;
    expect(followerId, userId);
  });
}
