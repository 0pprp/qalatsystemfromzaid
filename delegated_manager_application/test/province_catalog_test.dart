import 'package:delegated_manager_application/core/config/province_catalog.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  group('ProvinceCatalog demo', () {
    test('demo exposes only النجف - DEMO with DatabaseCompanyNajaf_DEMO', () {
      final provinces = ProvinceCatalog.forEnv('demo');
      expect(provinces, hasLength(1));
      expect(provinces.single.value, ProvinceCatalog.demoBranchValue);
      expect(provinces.single.value, 'DatabaseCompanyNajaf_DEMO');
      expect(provinces.single.name, 'النجف - DEMO');
    });

    test('DEMO env is case-insensitive', () {
      expect(ProvinceCatalog.forEnv('DEMO'), hasLength(1));
    });

    test('local exposes محلي only', () {
      final provinces = ProvinceCatalog.forEnv('local');
      expect(provinces, hasLength(1));
      expect(provinces.single.value, 'local');
      expect(provinces.single.name, 'محلي');
    });
  });

  group('ProvinceCatalog production mapping', () {
    test('prefers DatabaseCompany* as mission cityValue', () {
      final provinces = ProvinceCatalog.fromGetAdmin([
        {
          'value': 11,
          'name': 'النجف',
          'database': 'DatabaseCompanyNajaf',
          'link': 'http://example/api/',
        },
        {
          'value': 2,
          'name': 'البصرة',
          'database': 'DatabaseCompanyBasra',
          'link': 'http://example/api/',
        },
      ]);
      expect(provinces.map((p) => p.value).toList(), [
        'DatabaseCompanyNajaf',
        'DatabaseCompanyBasra',
      ]);
      expect(provinces.map((p) => p.name).toList(), ['النجف', 'البصرة']);
    });

    test('excludes legal/demo/monthly branches', () {
      final provinces = ProvinceCatalog.fromGetAdmin([
        {
          'value': 1,
          'name': 'قانونية الشركة',
          'database': 'DatabaseCompanyLegal',
          'link': 'http://example/api/',
        },
        {
          'value': 2,
          'name': 'تجريبي',
          'database': 'DatabaseCompanyTest',
          'link': 'http://example/api/',
        },
        {
          'value': 3,
          'name': 'الشهري',
          'database': 'DatabaseCompanyMonthly',
          'link': 'http://example/api/',
        },
        {
          'value': 11,
          'name': 'النجف',
          'database': 'DatabaseCompanyNajaf',
          'link': 'http://example/api/',
        },
      ]);
      expect(provinces, hasLength(1));
      expect(provinces.single.name, 'النجف');
    });

    test('falls back to value when database is empty', () {
      final provinces = ProvinceCatalog.fromGetAdmin([
        {
          'value': 'baghdad',
          'name': 'بغداد',
          'database': '',
          'link': 'http://example/api/',
        },
      ]);
      expect(provinces.single.value, 'baghdad');
    });

    test('skips rows without link', () {
      final provinces = ProvinceCatalog.fromGetAdmin([
        {
          'value': 1,
          'name': 'بدون رابط',
          'database': 'DatabaseCompanyX',
          'link': '',
        },
      ]);
      expect(provinces, isEmpty);
    });
  });
}
