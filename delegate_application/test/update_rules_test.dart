import 'dart:convert';

import 'package:crypto/crypto.dart';
import 'package:delegate_application/config/app_env.dart';
import 'package:delegate_application/core/update/mandatory_update_cache.dart';
import 'package:delegate_application/core/update/update_models.dart';
import 'package:delegate_application/core/update/update_rules.dart';
import 'package:delegate_application/core/update/update_service.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';

MobileUpdateInfo _info({
  required int current,
  required int latest,
  required int minimum,
  required bool force,
  String apk = 'https://example.com/app.apk',
  String sha =
      'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa',
}) {
  return MobileUpdateInfo.fromJson({
    'appKey': 'delegate',
    'latestVersionName': '1.0.$latest',
    'latestVersionCode': latest,
    'minimumSupportedVersionCode': minimum,
    'forceUpdate': force,
    'apkUrl': apk,
    'sha256': sha,
    'releaseNotes': 'notes',
  }, currentVersionCode: current);
}

void main() {
  group('MobileUpdateRules', () {
    test('no update when current >= latest', () {
      expect(
        MobileUpdateRules.evaluate(
          currentVersionCode: 19,
          latestVersionCode: 19,
          minimumSupportedVersionCode: 10,
          forceUpdate: false,
        ),
        MobileUpdateKind.none,
      );
    });

    test('optional when newer and still supported', () {
      expect(
        MobileUpdateRules.evaluate(
          currentVersionCode: 18,
          latestVersionCode: 19,
          minimumSupportedVersionCode: 10,
          forceUpdate: false,
        ),
        MobileUpdateKind.optional,
      );
    });

    test('mandatory when below minimumSupportedVersionCode', () {
      expect(
        MobileUpdateRules.evaluate(
          currentVersionCode: 5,
          latestVersionCode: 19,
          minimumSupportedVersionCode: 10,
          forceUpdate: false,
        ),
        MobileUpdateKind.mandatory,
      );
    });

    test('mandatory when forceUpdate', () {
      expect(
        MobileUpdateRules.evaluate(
          currentVersionCode: 18,
          latestVersionCode: 19,
          minimumSupportedVersionCode: 10,
          forceUpdate: true,
        ),
        MobileUpdateKind.mandatory,
      );
    });
  });

  group('UpdateService.matchesDigest', () {
    test('accepts matching sha256', () {
      final bytes = utf8.encode('delegate-apk');
      final digest = sha256.convert(bytes).toString();
      expect(UpdateService.matchesDigest(bytes, digest), isTrue);
    });

    test('rejects wrong or missing digest', () {
      final bytes = utf8.encode('delegate-apk');
      expect(UpdateService.matchesDigest(bytes, 'abcd'), isFalse);
      expect(UpdateService.matchesDigest(bytes, null), isFalse);
    });
  });

  group('AppEnv gateway targeting', () {
    test('demo never resolves production gateway', () {
      final base = AppEnv.resolveGatewayApiBase(env: 'demo');
      expect(base, AppEnv.demoGatewayApiBaseUrl);
    });

    test('production never resolves demo gateway', () {
      final base = AppEnv.resolveGatewayApiBase(
        env: 'production',
        apiBaseUrlDefine: 'http://169.58.236.52:8080/sales-gw/api/',
      );
      expect(base, AppEnv.productionGatewayApiBaseUrl);
    });
  });

  group('MandatoryUpdateCache', () {
    setUp(() async {
      SharedPreferences.setMockInitialValues({});
      MandatoryUpdateCache.prefsOverride =
          await SharedPreferences.getInstance();
    });

    tearDown(() {
      MandatoryUpdateCache.prefsOverride = null;
    });

    test('server says mandatory -> state cached', () async {
      final info = _info(current: 10, latest: 20, minimum: 15, force: false);
      expect(info.mandatory, isTrue);
      expect(MandatoryUpdatePolicy.shouldCache(info), isTrue);

      await MandatoryUpdateCache.syncFromServer(info, installedVersionCode: 10);
      final cached = await MandatoryUpdateCache.read();
      expect(cached, isNotNull);
      expect(cached!.mandatory, isTrue);
      expect(cached.requiredVersionCode, 20);
      expect(cached.apkUrl, startsWith('http'));
      expect(cached.sha256!.length, 64);
    });

    test('restart offline -> still blocked from cache', () async {
      final info = _info(current: 10, latest: 20, minimum: 15, force: true);
      await MandatoryUpdateCache.syncFromServer(info, installedVersionCode: 10);

      final cached = await MandatoryUpdateCache.read();
      expect(
        MandatoryUpdatePolicy.shouldBlockFromCache(
          cache: cached,
          installedVersionCode: 10,
        ),
        isTrue,
      );
    });

    test('upgrade installed -> cache cleared', () async {
      final info = _info(current: 10, latest: 20, minimum: 15, force: true);
      await MandatoryUpdateCache.syncFromServer(info, installedVersionCode: 10);
      expect(await MandatoryUpdateCache.read(), isNotNull);

      await MandatoryUpdateCache.clearIfSatisfied(20);
      expect(await MandatoryUpdateCache.read(), isNull);
      expect(
        MandatoryUpdatePolicy.shouldBlockFromCache(
          cache: await MandatoryUpdateCache.read(),
          installedVersionCode: 20,
        ),
        isFalse,
      );
    });

    test('optional update -> not cached as mandatory', () async {
      final optional =
          _info(current: 18, latest: 19, minimum: 10, force: false);
      expect(optional.kind, MobileUpdateKind.optional);
      expect(MandatoryUpdatePolicy.shouldCache(optional), isFalse);

      await MandatoryUpdateCache.syncFromServer(
        optional,
        installedVersionCode: 18,
      );
      expect(await MandatoryUpdateCache.read(), isNull);
    });

    test('optional response clears prior mandatory cache', () async {
      final mandatory =
          _info(current: 10, latest: 20, minimum: 15, force: true);
      await MandatoryUpdateCache.syncFromServer(
        mandatory,
        installedVersionCode: 10,
      );
      expect(await MandatoryUpdateCache.read(), isNotNull);

      final optional =
          _info(current: 18, latest: 19, minimum: 10, force: false);
      await MandatoryUpdateCache.syncFromServer(
        optional,
        installedVersionCode: 18,
      );
      expect(await MandatoryUpdateCache.read(), isNull);
    });

    test('first launch offline with no cache -> allowed', () async {
      expect(await MandatoryUpdateCache.read(), isNull);
      expect(
        MandatoryUpdatePolicy.shouldBlockFromCache(
          cache: null,
          installedVersionCode: 19,
        ),
        isFalse,
      );
    });

    test('corrupted cache -> fail safely without bricking app', () async {
      final prefs = MandatoryUpdateCache.prefsOverride!;
      await prefs.setString(MandatoryUpdateCache.prefsKey, '{not-json');
      expect(MandatoryUpdateSnapshot.tryParse('{not-json'), isNull);
      expect(await MandatoryUpdateCache.read(), isNull);

      await prefs.setString(
        MandatoryUpdateCache.prefsKey,
        jsonEncode({
          'mandatory': true,
          'requiredVersionCode': 20,
          // missing apkUrl / sha256
        }),
      );
      expect(await MandatoryUpdateCache.read(), isNull);
      expect(
        MandatoryUpdatePolicy.shouldBlockFromCache(
          cache: MandatoryUpdateSnapshot.tryParse(
            prefs.getString(MandatoryUpdateCache.prefsKey),
          ),
          installedVersionCode: 10,
        ),
        isFalse,
      );
    });

    test('network failure alone never blocks without prior cache', () {
      expect(
        MandatoryUpdatePolicy.shouldBlockFromCache(
          cache: null,
          installedVersionCode: 5,
        ),
        isFalse,
      );
    });
  });
}
