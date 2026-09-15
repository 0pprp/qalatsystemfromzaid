import 'package:delegate_application/core/update/update_rules.dart';

class MobileUpdateInfo {
  const MobileUpdateInfo({
    required this.appKey,
    required this.latestVersionName,
    required this.latestVersionCode,
    required this.minimumSupportedVersionCode,
    required this.forceUpdate,
    required this.kind,
    this.apkUrl,
    this.sha256,
    this.releaseNotes,
  });

  final String appKey;
  final String latestVersionName;
  final int latestVersionCode;
  final int minimumSupportedVersionCode;
  final bool forceUpdate;
  final MobileUpdateKind kind;
  final String? apkUrl;
  final String? sha256;
  final String? releaseNotes;

  bool get updateAvailable => kind != MobileUpdateKind.none;
  bool get mandatory => kind == MobileUpdateKind.mandatory;
  bool get canDownload => (apkUrl ?? '').startsWith('http');

  factory MobileUpdateInfo.fromJson(
    Map<String, dynamic> json, {
    required int currentVersionCode,
  }) {
    final latest = _asInt(json['latestVersionCode']);
    final minimum = _asInt(json['minimumSupportedVersionCode']);
    final force = json['forceUpdate'] == true;
    return MobileUpdateInfo(
      appKey: '${json['appKey'] ?? ''}',
      latestVersionName: '${json['latestVersionName'] ?? ''}',
      latestVersionCode: latest,
      minimumSupportedVersionCode: minimum,
      forceUpdate: force,
      kind: MobileUpdateRules.evaluate(
        currentVersionCode: currentVersionCode,
        latestVersionCode: latest,
        minimumSupportedVersionCode: minimum,
        forceUpdate: force,
      ),
      apkUrl: _optional(json['apkUrl']),
      sha256: _optional(json['sha256'])?.toLowerCase(),
      releaseNotes: _optional(json['releaseNotes']),
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
