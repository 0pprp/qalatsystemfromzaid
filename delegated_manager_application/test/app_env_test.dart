import 'package:delegated_manager_application/core/config/app_env.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  group('AppEnv demo targeting', () {
    test('demo resolves to the demo gateway and keeps the sales-gw path', () {
      final base = AppEnv.resolveApiBase(env: 'demo');

      expect(base, 'http://169.58.236.52:8080/sales-gw/api/');
      expect(base.contains('sales-gw'), isTrue);
      expect(base.endsWith('/'), isTrue);
    });

    test('demo ignores an API_BASE_URL pointing at production', () {
      final base = AppEnv.resolveApiBase(
        env: 'DEMO',
        apiBaseUrlDefine: 'http://admincompany.alsaaeidy.com/sales-gw/api/',
      );

      expect(base, AppEnv.demoHostFallback);
    });

    test('demo ignores a stored production session base', () {
      final base = AppEnv.resolveApiBase(
        env: 'demo',
        sessionBase: 'http://admincompany.alsaaeidy.com/sales-gw/api/',
      );

      expect(base, AppEnv.demoHostFallback);
    });
  });

  group('AppEnv production targeting', () {
    test('production falls back to the production gateway with sales-gw', () {
      final base = AppEnv.resolveApiBase(env: 'production');

      expect(base, 'http://admincompany.alsaaeidy.com/sales-gw/api/');
      expect(base.contains('sales-gw'), isTrue);
      expect(base.contains('169.58.236.52'), isFalse);
    });

    test('an unknown environment name behaves as production', () {
      expect(AppEnv.resolveApiBase(env: 'staging'),
          AppEnv.productionHostFallback);
    });

    test('production never uses a demo host from defines or session', () {
      final fromDefine = AppEnv.resolveApiBase(
        env: 'production',
        apiBaseDefine: 'http://169.58.236.52:8080/sales-gw/api/',
      );
      final fromSession = AppEnv.resolveApiBase(
        env: 'production',
        sessionBase: 'http://169.58.236.52:8080/sales-gw/api/',
      );

      expect(fromDefine, AppEnv.productionHostFallback);
      expect(fromSession, AppEnv.productionHostFallback);
    });

    test('explicit production override wins over the session base', () {
      final base = AppEnv.resolveApiBase(
        env: 'production',
        apiBaseDefine: 'http://gw.internal/sales-gw/api',
        sessionBase: 'http://old.internal/sales-gw/api/',
      );

      expect(base, 'http://gw.internal/sales-gw/api/');
    });

    test('session base is used when no define is provided', () {
      final base = AppEnv.resolveApiBase(
        env: 'production',
        sessionBase: 'http://admincompany.alsaaeidy.com/sales-gw/api',
      );

      expect(base, 'http://admincompany.alsaaeidy.com/sales-gw/api/');
    });
  });

  group('AppEnv local targeting', () {
    test('local resolves to the local gateway without sales-gw', () {
      final base = AppEnv.resolveApiBase(env: 'local');

      expect(base, 'http://127.0.0.1:5280/api/');
      expect(base.contains('sales-gw'), isFalse);
    });
  });

  group('AppEnv.normalizeBase', () {
    test('collapses duplicated api segments and keeps sales-gw', () {
      expect(
        AppEnv.normalizeBase('http://admincompany.alsaaeidy.com/sales-gw/api/api/'),
        'http://admincompany.alsaaeidy.com/sales-gw/api/',
      );
    });

    test('adds a trailing slash', () {
      expect(
        AppEnv.normalizeBase('http://gw.internal/sales-gw/api'),
        'http://gw.internal/sales-gw/api/',
      );
    });

    test('appends api when only the demo port is provided', () {
      expect(AppEnv.normalizeBase('http://127.0.0.1:8080'),
          'http://127.0.0.1:8080/api/');
    });

    test('any demo host is pinned to the demo fallback', () {
      expect(AppEnv.normalizeBase('http://169.58.236.52:8080/'),
          AppEnv.demoHostFallback);
    });

    test('empty stays empty', () {
      expect(AppEnv.normalizeBase('  '), '');
    });
  });
}
