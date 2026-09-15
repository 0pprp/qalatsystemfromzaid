import 'dart:convert';

import 'package:crypto/crypto.dart';
import 'package:delegated_manager_application/core/update/update_models.dart';
import 'package:delegated_manager_application/core/update/update_rules.dart';
import 'package:delegated_manager_application/core/update/update_service.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  group('MobileUpdateRules.evaluate mirrors the gateway rules', () {
    test('installed build at or above latest needs no update', () {
      expect(
        MobileUpdateRules.evaluate(
          currentVersionCode: 5,
          latestVersionCode: 5,
          minimumSupportedVersionCode: 3,
          forceUpdate: false,
        ),
        MobileUpdateKind.none,
      );
      expect(
        MobileUpdateRules.evaluate(
          currentVersionCode: 7,
          latestVersionCode: 5,
          minimumSupportedVersionCode: 3,
          forceUpdate: true,
        ),
        MobileUpdateKind.none,
      );
    });

    test('below the minimum supported build is mandatory', () {
      expect(
        MobileUpdateRules.evaluate(
          currentVersionCode: 2,
          latestVersionCode: 5,
          minimumSupportedVersionCode: 3,
          forceUpdate: false,
        ),
        MobileUpdateKind.mandatory,
      );
    });

    test('forceUpdate makes an otherwise optional update mandatory', () {
      expect(
        MobileUpdateRules.evaluate(
          currentVersionCode: 4,
          latestVersionCode: 5,
          minimumSupportedVersionCode: 3,
          forceUpdate: true,
        ),
        MobileUpdateKind.mandatory,
      );
    });

    test('supported but older build is optional', () {
      expect(
        MobileUpdateRules.evaluate(
          currentVersionCode: 4,
          latestVersionCode: 5,
          minimumSupportedVersionCode: 3,
          forceUpdate: false,
        ),
        MobileUpdateKind.optional,
      );
    });
  });

  group('MobileUpdateRules.compareVersionNames', () {
    test('compares numerically, not lexically', () {
      expect(MobileUpdateRules.compareVersionNames('1.2.10', '1.2.9'),
          greaterThan(0));
      expect(MobileUpdateRules.compareVersionNames('1.2.9', '1.2.10'),
          lessThan(0));
    });

    test('treats missing segments as zero', () {
      expect(MobileUpdateRules.compareVersionNames('1.2', '1.2.0'), 0);
      expect(
          MobileUpdateRules.compareVersionNames('2.0', '1.9.9'), greaterThan(0));
    });

    test('ignores build suffixes', () {
      expect(MobileUpdateRules.compareVersionNames('1.0.0+3', '1.0.0'), 0);
    });
  });

  group('MobileUpdateInfo.fromJson', () {
    const payload = {
      'appKey': 'delegated-manager',
      'latestVersionName': '1.1.0',
      'latestVersionCode': 11,
      'minimumSupportedVersionCode': 10,
      'forceUpdate': false,
      'apkUrl': 'https://cdn.invalid/delegated-manager.apk',
      'sha256':
          'AABBCCDDEEFF00112233445566778899AABBCCDDEEFF00112233445566778899',
      'releaseNotes': 'إصلاحات',
      'updatedAtUtc': '2026-09-15T10:20:30',
      'updateKind': 'Mandatory',
      'updateAvailable': true,
      'mandatory': true,
    };

    test('derives the kind from the installed build, not the payload', () {
      final stale =
          MobileUpdateInfo.fromJson(payload, currentVersionCode: 11);
      expect(stale.kind, MobileUpdateKind.none);
      expect(stale.updateAvailable, isFalse);

      final optional =
          MobileUpdateInfo.fromJson(payload, currentVersionCode: 10);
      expect(optional.kind, MobileUpdateKind.optional);
      expect(optional.mandatory, isFalse);

      final mandatory =
          MobileUpdateInfo.fromJson(payload, currentVersionCode: 9);
      expect(mandatory.kind, MobileUpdateKind.mandatory);
      expect(mandatory.mandatory, isTrue);
    });

    test('normalizes fields and parses the timestamp as UTC', () {
      final info = MobileUpdateInfo.fromJson(payload, currentVersionCode: 1);

      expect(info.appKey, 'delegated-manager');
      expect(info.latestVersionName, '1.1.0');
      expect(info.sha256, payload['sha256'].toString().toLowerCase());
      expect(info.canDownload, isTrue);
      expect(info.updatedAtUtc?.toUtc(),
          DateTime.utc(2026, 9, 15, 10, 20, 30));
    });

    test('tolerates an empty payload', () {
      final info =
          MobileUpdateInfo.fromJson(const {}, currentVersionCode: 1);

      expect(info.kind, MobileUpdateKind.none);
      expect(info.canDownload, isFalse);
      expect(info.sha256, isNull);
    });

    test('parseKind maps the server enum names', () {
      expect(MobileUpdateRules.parseKind('Optional'), MobileUpdateKind.optional);
      expect(
          MobileUpdateRules.parseKind('Mandatory'), MobileUpdateKind.mandatory);
      expect(MobileUpdateRules.parseKind(null), MobileUpdateKind.none);
    });
  });

  group('UpdateService.matchesDigest', () {
    final bytes = utf8.encode('delegated-manager-apk');
    final digest = sha256.convert(bytes).toString();

    test('accepts the published digest regardless of case', () {
      expect(UpdateService.matchesDigest(bytes, digest), isTrue);
      expect(UpdateService.matchesDigest(bytes, digest.toUpperCase()), isTrue);
    });

    test('rejects a mismatching or missing digest', () {
      expect(UpdateService.matchesDigest(bytes, 'a' * 64), isFalse);
      expect(UpdateService.matchesDigest(bytes, null), isFalse);
      expect(UpdateService.matchesDigest(bytes, 'abc'), isFalse);
    });
  });
}
