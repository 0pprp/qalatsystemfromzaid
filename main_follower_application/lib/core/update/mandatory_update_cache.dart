import 'dart:convert';

import 'package:main_follower_application/core/update/update_models.dart';
import 'package:main_follower_application/core/update/update_rules.dart';
import 'package:shared_preferences/shared_preferences.dart';

class MandatoryUpdatePolicy {
  static bool shouldCache(MobileUpdateInfo info) =>
      info.mandatory &&
      info.canDownload &&
      (info.sha256 ?? '').length == 64;

  static bool shouldClear({
    required int installedVersionCode,
    required int requiredVersionCode,
  }) =>
      installedVersionCode >= requiredVersionCode;

  static bool shouldBlockFromCache({
    required MandatoryUpdateSnapshot? cache,
    required int installedVersionCode,
  }) {
    if (cache == null || !cache.mandatory) return false;
    if (cache.requiredVersionCode <= 0) return false;
    if (!(cache.apkUrl ?? '').startsWith('http')) return false;
    if ((cache.sha256 ?? '').length != 64) return false;
    return installedVersionCode < cache.requiredVersionCode;
  }
}

class MandatoryUpdateSnapshot {
  const MandatoryUpdateSnapshot({
    required this.mandatory,
    required this.requiredVersionCode,
    required this.latestVersionName,
    required this.apkUrl,
    required this.sha256,
    this.releaseNotes,
  });

  final bool mandatory;
  final int requiredVersionCode;
  final String latestVersionName;
  final String? apkUrl;
  final String? sha256;
  final String? releaseNotes;

  MobileUpdateInfo toUpdateInfo({required int currentVersionCode}) {
    return MobileUpdateInfo(
      appKey: 'main-follower',
      latestVersionName: latestVersionName,
      latestVersionCode: requiredVersionCode,
      minimumSupportedVersionCode: requiredVersionCode,
      forceUpdate: true,
      kind: MobileUpdateRules.evaluate(
        currentVersionCode: currentVersionCode,
        latestVersionCode: requiredVersionCode,
        minimumSupportedVersionCode: requiredVersionCode,
        forceUpdate: true,
      ),
      apkUrl: apkUrl,
      sha256: sha256,
      releaseNotes: releaseNotes,
    );
  }

  Map<String, dynamic> toJson() => {
        'mandatory': mandatory,
        'requiredVersionCode': requiredVersionCode,
        'latestVersionName': latestVersionName,
        'apkUrl': apkUrl,
        'sha256': sha256,
        'releaseNotes': releaseNotes,
      };

  static MandatoryUpdateSnapshot? tryParse(String? raw) {
    if (raw == null || raw.trim().isEmpty) return null;
    try {
      final decoded = jsonDecode(raw);
      if (decoded is! Map) return null;
      final map = Map<String, dynamic>.from(decoded);
      if (map['mandatory'] != true) return null;
      final required = _asInt(map['requiredVersionCode']);
      if (required <= 0) return null;
      final apkUrl = _optional(map['apkUrl']);
      final sha256 = _optional(map['sha256'])?.toLowerCase();
      if (apkUrl == null || !apkUrl.startsWith('http')) return null;
      if (sha256 == null || sha256.length != 64) return null;
      return MandatoryUpdateSnapshot(
        mandatory: true,
        requiredVersionCode: required,
        latestVersionName: '${map['latestVersionName'] ?? ''}',
        apkUrl: apkUrl,
        sha256: sha256,
        releaseNotes: _optional(map['releaseNotes']),
      );
    } catch (_) {
      return null;
    }
  }

  static MandatoryUpdateSnapshot fromMandatoryInfo(MobileUpdateInfo info) {
    return MandatoryUpdateSnapshot(
      mandatory: true,
      requiredVersionCode: info.latestVersionCode,
      latestVersionName: info.latestVersionName,
      apkUrl: info.apkUrl,
      sha256: info.sha256,
      releaseNotes: info.releaseNotes,
    );
  }

  static int _asInt(dynamic v) {
    if (v is int) return v;
    if (v is num) return v.toInt();
    return int.tryParse('$v') ?? 0;
  }

  static String? _optional(dynamic v) {
    if (v == null) return null;
    final s = '$v'.trim();
    return s.isEmpty ? null : s;
  }
}

class MandatoryUpdateCache {
  static const prefsKey = 'main_follower_mandatory_update_v1';

  static Future<MandatoryUpdateSnapshot?> read() async {
    final prefs = await SharedPreferences.getInstance();
    return MandatoryUpdateSnapshot.tryParse(prefs.getString(prefsKey));
  }

  static Future<void> save(MandatoryUpdateSnapshot snapshot) async {
    if (!snapshot.mandatory) return;
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(prefsKey, jsonEncode(snapshot.toJson()));
  }

  static Future<void> clear() async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.remove(prefsKey);
  }

  static Future<void> syncFromServer(
    MobileUpdateInfo info, {
    required int installedVersionCode,
  }) async {
    if (MandatoryUpdatePolicy.shouldCache(info)) {
      await save(MandatoryUpdateSnapshot.fromMandatoryInfo(info));
      return;
    }
    await clearIfSatisfied(installedVersionCode);
    if (!info.mandatory) {
      await clear();
    }
  }

  static Future<void> clearIfSatisfied(int installedVersionCode) async {
    final cached = await read();
    if (cached == null) return;
    if (MandatoryUpdatePolicy.shouldClear(
      installedVersionCode: installedVersionCode,
      requiredVersionCode: cached.requiredVersionCode,
    )) {
      await clear();
    }
  }
}
