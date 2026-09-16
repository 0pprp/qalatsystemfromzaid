import 'package:delegated_manager_application/core/utils/dm_format.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  group('DmFormat.money', () {
    test('13000 → 13,000 د.ع', () {
      expect(DmFormat.money(13000), '13,000 د.ع');
    });

    test('1250000 → 1,250,000 د.ع', () {
      expect(DmFormat.money(1250000), '1,250,000 د.ع');
    });

    test('null → غير متوفر', () {
      expect(DmFormat.money(null), 'غير متوفر');
    });
  });

  group('DmFormat.dateOnly', () {
    test('strips time', () {
      final dt = DateTime.utc(2026, 9, 15, 1, 54);
      final text = DmFormat.dateOnly(dt);
      expect(text.contains(':'), isFalse);
      expect(text.contains('ص'), isFalse);
      expect(RegExp(r'^\d{2}/\d{2}/\d{4}$').hasMatch(text), isTrue);
    });

    test('null → غير متوفر', () {
      expect(DmFormat.dateOnly(null), 'غير متوفر');
    });
  });

  group('DmFormat.friendlyCity', () {
    test('never shows DatabaseCompanyBasra when mapped', () {
      expect(DmFormat.friendlyCity('DatabaseCompanyBasra', 'basra-demo'), 'البصرة');
      expect(DmFormat.friendlyCity('DatabaseCompanyBasra'), 'البصرة');
      expect(DmFormat.friendlyCity(null, 'basra-demo'), 'البصرة');
    });

    test('never shows DatabaseCompanyNajaf when mapped', () {
      expect(DmFormat.friendlyCity('DatabaseCompanyNajaf', 'najaf-demo'), 'النجف');
      expect(DmFormat.friendlyCity(null, 'najaf-demo'), 'النجف');
    });

    test('unresolved legacy → غير متوفر', () {
      expect(DmFormat.friendlyCity('DatabaseCompanyUnknownZzz'), 'غير متوفر');
    });

    test('prefers Arabic cityName', () {
      expect(DmFormat.friendlyCity('البصرة', 'DatabaseCompanyBasra'), 'البصرة');
    });
  });
}
