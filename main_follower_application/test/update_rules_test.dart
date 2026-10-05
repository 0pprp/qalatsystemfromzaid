import 'package:flutter_test/flutter_test.dart';
import 'package:main_follower_application/core/update/update_rules.dart';

void main() {
  test('optional when below latest but above minimum', () {
    expect(
      MobileUpdateRules.evaluate(
        currentVersionCode: 2,
        latestVersionCode: 5,
        minimumSupportedVersionCode: 1,
        forceUpdate: false,
      ),
      MobileUpdateKind.optional,
    );
  });

  test('mandatory when below minimum', () {
    expect(
      MobileUpdateRules.evaluate(
        currentVersionCode: 1,
        latestVersionCode: 5,
        minimumSupportedVersionCode: 3,
        forceUpdate: false,
      ),
      MobileUpdateKind.mandatory,
    );
  });

  test('forceUpdate makes optional mandatory', () {
    expect(
      MobileUpdateRules.evaluate(
        currentVersionCode: 2,
        latestVersionCode: 5,
        minimumSupportedVersionCode: 1,
        forceUpdate: true,
      ),
      MobileUpdateKind.mandatory,
    );
  });

  test('none when up to date', () {
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
}
