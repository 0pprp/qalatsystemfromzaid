import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/json.dart';
import '../../../core/providers.dart';

class RemoteConfig {
  const RemoteConfig({
    this.minSupportedVersion = '0.0.0',
    this.latestVersion = '0.0.0',
    this.supportPhone,
    this.supportWhatsapp,
    this.apkUrl,
  });

  final String minSupportedVersion;
  final String latestVersion;
  final String? supportPhone;
  final String? supportWhatsapp;

  /// Where the update buttons send the agent (the app is installed from an APK).
  final String? apkUrl;

  factory RemoteConfig.fromJson(Json j) => RemoteConfig(
        minSupportedVersion: asString(j['minSupportedVersion'], '0.0.0'),
        latestVersion: asString(j['latestVersion'], '0.0.0'),
        supportPhone: asStringOrNull(j['supportPhone']),
        supportWhatsapp: asStringOrNull(j['supportWhatsapp']),
        apkUrl: asStringOrNull(j['apkUrl']),
      );
}

/// Compares dotted versions (`1.2.10` > `1.2.9`). Build suffixes (`+3`, `-beta`) are ignored.
int compareVersions(String a, String b) {
  List<int> parse(String v) =>
      v.split(RegExp(r'[+\-]')).first.split('.').map((p) => int.tryParse(p.trim()) ?? 0).toList();
  final pa = parse(a);
  final pb = parse(b);
  final n = pa.length > pb.length ? pa.length : pb.length;
  for (var i = 0; i < n; i++) {
    final x = i < pa.length ? pa[i] : 0;
    final y = i < pb.length ? pb[i] : 0;
    if (x != y) return x.compareTo(y);
  }
  return 0;
}

/// `app/config`. Falls back to defaults when offline so the app stays usable.
final remoteConfigProvider = FutureProvider<RemoteConfig>((ref) async {
  try {
    final json = asJson(await ref.watch(apiClientProvider).get('app/config'));
    return RemoteConfig.fromJson(json);
  } catch (_) {
    return const RemoteConfig();
  }
});

/// The loaded config, or defaults while loading.
final configProvider = Provider<RemoteConfig>(
  (ref) => ref.watch(remoteConfigProvider).value ?? const RemoteConfig(),
);

final mustUpdateProvider = Provider<bool>((ref) {
  final cfg = ref.watch(remoteConfigProvider).value;
  if (cfg == null) return false;
  final current = ref.watch(packageInfoProvider).version;
  return compareVersions(current, cfg.minSupportedVersion) < 0;
});

final updateAvailableProvider = Provider<bool>((ref) {
  final cfg = ref.watch(remoteConfigProvider).value;
  if (cfg == null) return false;
  final current = ref.watch(packageInfoProvider).version;
  return compareVersions(current, cfg.latestVersion) < 0;
});
