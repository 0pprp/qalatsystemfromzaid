import 'package:flutter_test/flutter_test.dart';
import 'package:sales_employee_application/core/update/update_rules.dart';

void main() {
  group('MobileUpdateRules', () {
    test('none when current is up to date', () {
      expect(
        MobileUpdateRules.evaluate(
          currentVersionCode: 5,
          latestVersionCode: 5,
          minimumSupportedVersionCode: 1,
          forceUpdate: true,
        ),
        MobileUpdateKind.none,
      );
    });

    test('mandatory when below minimum supported', () {
      expect(
        MobileUpdateRules.evaluate(
          currentVersionCode: 2,
          latestVersionCode: 10,
          minimumSupportedVersionCode: 5,
          forceUpdate: false,
        ),
        MobileUpdateKind.mandatory,
      );
    });

    test('mandatory when forceUpdate flag set', () {
      expect(
        MobileUpdateRules.evaluate(
          currentVersionCode: 8,
          latestVersionCode: 10,
          minimumSupportedVersionCode: 1,
          forceUpdate: true,
        ),
        MobileUpdateKind.mandatory,
      );
    });

    test('optional when behind latest but above minimum', () {
      expect(
        MobileUpdateRules.evaluate(
          currentVersionCode: 8,
          latestVersionCode: 10,
          minimumSupportedVersionCode: 1,
          forceUpdate: false,
        ),
        MobileUpdateKind.optional,
      );
    });
  });
}
