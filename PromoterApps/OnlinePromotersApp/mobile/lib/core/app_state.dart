import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../data/models.dart';
import '../data/repository.dart';
import 'providers.dart';

/// `app/config` (support numbers, forced update). Defaults when offline.
final remoteConfigProvider = FutureProvider<RemoteConfig>((ref) async {
  try {
    return await ref.watch(repositoryProvider).config();
  } catch (_) {
    return const RemoteConfig();
  }
});

final configProvider = Provider<RemoteConfig>((ref) => ref.watch(remoteConfigProvider).value ?? const RemoteConfig());

int compareVersions(String a, String b) {
  List<int> parse(String v) => v.split(RegExp(r'[+\-]')).first.split('.').map((p) => int.tryParse(p.trim()) ?? 0).toList();
  final pa = parse(a), pb = parse(b);
  final n = pa.length > pb.length ? pa.length : pb.length;
  for (var i = 0; i < n; i++) {
    final x = i < pa.length ? pa[i] : 0, y = i < pb.length ? pb[i] : 0;
    if (x != y) return x.compareTo(y);
  }
  return 0;
}

final mustUpdateProvider = Provider<bool>((ref) {
  final cfg = ref.watch(remoteConfigProvider).value;
  if (cfg == null) return false;
  return compareVersions(ref.watch(packageInfoProvider).version, cfg.minSupportedVersion) < 0;
});

/// Bumped after anything that changes balances or orders, so the home, orders and wallet tabs reload.
final dataVersionProvider = NotifierProvider<DataVersion, int>(DataVersion.new);

class DataVersion extends Notifier<int> {
  @override
  int build() => 0;

  void bump() => state++;
}

final overviewProvider = FutureProvider.autoDispose<Overview>((ref) {
  ref.watch(dataVersionProvider);
  return ref.watch(repositoryProvider).overview();
});

final productsProvider = FutureProvider.autoDispose<List<Product>>((ref) {
  ref.watch(dataVersionProvider);
  return ref.watch(repositoryProvider).products();
});

final branchesProvider = FutureProvider<List<Branch>>((ref) => ref.watch(repositoryProvider).branches());

final walletProvider = FutureProvider.autoDispose<WalletSummary>((ref) {
  ref.watch(dataVersionProvider);
  return ref.watch(repositoryProvider).wallet();
});

final teamProvider = FutureProvider.autoDispose<Team>((ref) {
  ref.watch(dataVersionProvider);
  return ref.watch(repositoryProvider).team();
});

final materialsProvider = FutureProvider.autoDispose<List<MarketingMaterial>>((ref) => ref.watch(repositoryProvider).materials());

/// Unread notifications (the bell). Refreshed on resume and every minute (no push in this app).
final unreadCountProvider = NotifierProvider<UnreadCount, int>(UnreadCount.new);

class UnreadCount extends Notifier<int> {
  @override
  int build() => 0;

  Future<void> refresh() async {
    try {
      state = await ref.read(repositoryProvider).unreadCount();
    } catch (_) {}
  }

  void set(int value) => state = value;
}
