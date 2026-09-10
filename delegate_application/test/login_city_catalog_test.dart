import 'package:delegate_application/config/login_city_catalog.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  group('LoginCityCatalog demo', () {
    test('demo cities contain only النجف - DEMO', () {
      final cities = LoginCityCatalog.demoCities();
      expect(cities, hasLength(1));
      expect(cities.single['name'], 'النجف - DEMO');
      expect(cities.single['link'], 'http://169.58.236.52:8081/api/');
      // Login never reads number; shape matches GetHaider (النجف = 11).
      expect(cities.single['number'], '11');
    });

    test('demo must not call production city API', () {
      expect(LoginCityCatalog.shouldCallProductionCityApi('demo'), isFalse);
      expect(LoginCityCatalog.shouldCallProductionCityApi('production'), isTrue);
    });

    test('demo ignores legacy/production caches', () {
      final resolved = LoginCityCatalog.resolveCacheJson(
        envName: 'demo',
        envSpecificCache: '[{"name":"الرصافة","link":"x","number":"1"}]',
        legacyCache: '[{"name":"الكرخ","link":"y","number":"2"}]',
      );
      expect(resolved, isNull);
    });
  });

  group('LoginCityCatalog production', () {
    test('uses production cache key', () {
      expect(
        LoginCityCatalog.cacheKeyForEnv('production'),
        'cached_city_data_production',
      );
      expect(LoginCityCatalog.cacheKeyForEnv('demo'), 'cached_city_data_demo');
    });

    test('production prefers env cache then legacy GetHaider cache', () {
      final prodOnly = LoginCityCatalog.resolveCacheJson(
        envName: 'production',
        envSpecificCache: '[{"name":"النجف","link":"a","number":"11"}]',
        legacyCache: '[{"name":"الرصافة","link":"b","number":"1"}]',
      );
      expect(prodOnly, contains('النجف'));

      final legacyFallback = LoginCityCatalog.resolveCacheJson(
        envName: 'production',
        envSpecificCache: null,
        legacyCache: '[{"name":"الرصافة","link":"b","number":"1"}]',
      );
      expect(legacyFallback, contains('الرصافة'));
    });

    test('parseCityJson maps GetHaider shape', () {
      const sample =
          '[{"name":"النجف","link":"http://delegatehaidernajaflast.alsaaeidy.com/api/","number":"11"}]';
      final parsed = LoginCityCatalog.parseCityJson(sample);
      expect(parsed.single['name'], 'النجف');
      expect(parsed.single['number'], '11');
      expect(LoginCityCatalog.productionCityApiUrl,
          'http://defaultdata.alsaaeidy.com/GetHaider');
    });

    test('demo and production cache keys are separated', () {
      expect(LoginCityCatalog.demoCacheKey,
          isNot(LoginCityCatalog.productionCacheKey));
      expect(LoginCityCatalog.demoCacheKey, isNot(LoginCityCatalog.legacyCacheKey));
    });
  });
}
