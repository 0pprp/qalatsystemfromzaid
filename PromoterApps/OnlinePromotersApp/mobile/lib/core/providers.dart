import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:package_info_plus/package_info_plus.dart';
import 'package:shared_preferences/shared_preferences.dart';

import '../features/auth/auth_controller.dart';
import 'config/app_config.dart';
import 'network/api_client.dart';
import 'storage/token_storage.dart';

/// Overridden in `main()`.
final sharedPrefsProvider = Provider<SharedPreferences>((ref) => throw UnimplementedError('override in main()'));

/// Overridden in `main()`.
final packageInfoProvider = Provider<PackageInfo>(
  (ref) => PackageInfo(appName: 'مروّجو قلعة الضمان', packageName: AppConfig.androidPackageId, version: '1.0.0', buildNumber: '1'),
);

final tokenStorageProvider = Provider<TokenStorage>((ref) => TokenStorage());

final apiClientProvider = Provider<ApiClient>((ref) => ApiClient(
      tokens: ref.watch(tokenStorageProvider),
      onSessionExpired: () => ref.read(authControllerProvider.notifier).onSessionExpired(),
      onTokensRefreshed: (user) => ref.read(authControllerProvider.notifier).onUserRefreshed(user),
    ));
