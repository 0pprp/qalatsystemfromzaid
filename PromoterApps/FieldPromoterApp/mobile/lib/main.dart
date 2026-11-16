import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:package_info_plus/package_info_plus.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'app.dart';
import 'core/providers.dart';
import 'core/push/push_service.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  await SystemChrome.setPreferredOrientations([DeviceOrientation.portraitUp]);
  SystemChrome.setSystemUIOverlayStyle(const SystemUiOverlayStyle(
    statusBarColor: Colors.transparent,
    statusBarIconBrightness: Brightness.dark,
  ));

  final prefs = await SharedPreferences.getInstance();
  PackageInfo? info;
  try {
    info = await PackageInfo.fromPlatform();
  } catch (_) {}

  final container = ProviderContainer(
    overrides: [
      sharedPrefsProvider.overrideWithValue(prefs),
      if (info != null) packageInfoProvider.overrideWithValue(info),
    ],
    // Screens show explicit error states with a retry button; no silent automatic retries.
    retry: (_, _) => null,
  );

  // Firebase is optional: missing google-services.json / GoogleService-Info.plist only disables push.
  try {
    await container.read(pushServiceProvider).init().timeout(const Duration(seconds: 6));
  } catch (_) {}

  runApp(UncontrolledProviderScope(container: container, child: const QalaaApp()));
}
