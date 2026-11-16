import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:package_info_plus/package_info_plus.dart';
import 'package:shared_preferences/shared_preferences.dart';

import '../features/auth/data/auth_controller.dart';
import 'config/app_config.dart';
import 'fake/fake_backend.dart';
import 'network/api_client.dart';
import 'storage/token_storage.dart';

/// Overridden in `main()` after `SharedPreferences.getInstance()`.
final sharedPrefsProvider = Provider<SharedPreferences>(
  (ref) => throw UnimplementedError('sharedPrefsProvider must be overridden in main()'),
);

/// Overridden in `main()` after `PackageInfo.fromPlatform()`.
final packageInfoProvider = Provider<PackageInfo>(
  (ref) => PackageInfo(appName: 'قلعة الضمان', packageName: AppConfig.androidPackageId, version: '1.0.0', buildNumber: '1'),
);

final tokenStorageProvider = Provider<TokenStorage>((ref) => TokenStorage());

final fakeBackendProvider = Provider<FakeBackend?>((ref) => AppConfig.useFakeApi ? FakeBackend() : null);

final apiClientProvider = Provider<ApiClient>((ref) {
  return ApiClient(
    tokens: ref.watch(tokenStorageProvider),
    fakeBackend: ref.watch(fakeBackendProvider),
    appVersion: ref.watch(packageInfoProvider).version,
    onSessionExpired: () => ref.read(authControllerProvider.notifier).onSessionExpired(),
    onTokensRefreshed: (user) => ref.read(authControllerProvider.notifier).onUserRefreshed(user),
  );
});
