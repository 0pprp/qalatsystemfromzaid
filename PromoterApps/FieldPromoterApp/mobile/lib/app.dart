import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'core/push/push_service.dart';
import 'core/router/app_router.dart';
import 'core/theme/app_theme.dart';
import 'features/notifications/data/notifications_repository.dart';

class QalaaApp extends ConsumerStatefulWidget {
  const QalaaApp({super.key, this.enablePlatformServices = true});

  /// Disabled in widget tests (no push plugins).
  final bool enablePlatformServices;

  @override
  ConsumerState<QalaaApp> createState() => _QalaaAppState();
}

class _QalaaAppState extends ConsumerState<QalaaApp> {
  StreamSubscription<Object?>? _pushSub;

  @override
  void initState() {
    super.initState();
    if (widget.enablePlatformServices) {
      final router = ref.read(routerProvider);
      final push = ref.read(pushServiceProvider);
      push.onNavigate = (route) => openExternalRoute(router, route);
      final pending = push.takePendingRoute();
      if (pending != null) pendingLaunchRoute = pending;
      _pushSub = push.foregroundMessages.listen((_) => ref.read(unreadCountProvider.notifier).refresh());
    }
  }

  @override
  void dispose() {
    _pushSub?.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final router = ref.watch(routerProvider);
    return MaterialApp.router(
      title: 'قلعة الضمان',
      debugShowCheckedModeBanner: false,
      theme: AppTheme.light(),
      routerConfig: router,
      locale: const Locale('ar', 'IQ'),
      supportedLocales: const [Locale('ar', 'IQ'), Locale('ar')],
      localizationsDelegates: const [
        GlobalMaterialLocalizations.delegate,
        GlobalWidgetsLocalizations.delegate,
        GlobalCupertinoLocalizations.delegate,
      ],
      builder: (context, child) {
        // Arabic-first: force RTL and cap extreme text scaling to keep layouts intact.
        final mq = MediaQuery.of(context);
        return MediaQuery(
          data: mq.copyWith(textScaler: mq.textScaler.clamp(minScaleFactor: 0.9, maxScaleFactor: 1.3)),
          child: Directionality(textDirection: TextDirection.rtl, child: child ?? const SizedBox()),
        );
      },
    );
  }
}
