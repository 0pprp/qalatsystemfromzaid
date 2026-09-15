import 'package:delegated_manager_application/core/update/update_rules.dart';
import 'package:delegated_manager_application/core/utils/date_format.dart';
import 'package:delegated_manager_application/core/utils/json_read.dart';

/// Response of `GET mobile-updates/{appKey}`.
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
    this.updatedAtUtc,
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
  final DateTime? updatedAtUtc;

  bool get updateAvailable => kind != MobileUpdateKind.none;
  bool get mandatory => kind == MobileUpdateKind.mandatory;
  bool get canDownload => (apkUrl ?? '').startsWith('http');

  /// [currentVersionCode] lets the client re-derive the decision locally
  /// instead of trusting `updateKind` alone.
  factory MobileUpdateInfo.fromJson(
    Map<String, dynamic> json, {
    required int currentVersionCode,
  }) {
    final latest = JsonRead.number(json['latestVersionCode']);
    final minimum = JsonRead.number(json['minimumSupportedVersionCode']);
    final force = JsonRead.flag(json['forceUpdate']);
    return MobileUpdateInfo(
      appKey: JsonRead.text(json['appKey']),
      latestVersionName: JsonRead.text(json['latestVersionName']),
      latestVersionCode: latest,
      minimumSupportedVersionCode: minimum,
      forceUpdate: force,
      kind: MobileUpdateRules.evaluate(
        currentVersionCode: currentVersionCode,
        latestVersionCode: latest,
        minimumSupportedVersionCode: minimum,
        forceUpdate: force,
      ),
      apkUrl: JsonRead.optionalText(json['apkUrl']),
      sha256: JsonRead.optionalText(json['sha256'])?.toLowerCase(),
      releaseNotes: JsonRead.optionalText(json['releaseNotes']),
      updatedAtUtc: AppDate.parseUtc(json['updatedAtUtc']),
    );
  }
}
