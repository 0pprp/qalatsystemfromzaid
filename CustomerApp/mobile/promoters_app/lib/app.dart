import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'core/app_state.dart';
import 'core/push_service.dart';
import 'core/router.dart';
import 'core/theme/app_theme.dart';
import 'features/auth/auth_controller.dart';

class PromotersApp extends ConsumerStatefulWidget {
  const PromotersApp({super.key});

  @override
  ConsumerState<PromotersApp> createState() => _PromotersAppState();
}

class _PromotersAppState extends ConsumerState<PromotersApp> with WidgetsBindingObserver {
  Timer? _timer;
  StreamSubscription<Object?>? _pushSub;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
    final router = ref.read(routerProvider);
    final push = ref.read(pushServiceProvider);
    push.onNavigate = (route) => openFromNotification(router, route);
    final pending = push.takePendingRoute();
    if (pending != null) pendingLaunchRoute = pending;
    // A push while the app is open: refresh the bell, balances and orders.
    _pushSub = push.foregroundMessages.listen((_) {
      _refresh();
      ref.read(dataVersionProvider.notifier).bump();
    });
    // Also without push (no Google Play services): the bell refreshes every minute and when the app comes back.
    _timer = Timer.periodic(const Duration(minutes: 1), (_) => _refresh());
  }

  @override
  void dispose() {
    _timer?.cancel();
    _pushSub?.cancel();
    WidgetsBinding.instance.removeObserver(this);
    super.dispose();
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed) {
      // Re-check the published version: a promoter who keeps the app open in the background still gets the
      // mandatory update screen as soon as a new version is published.
      ref.invalidate(remoteConfigProvider);
      _refresh();
      ref.read(dataVersionProvider.notifier).bump();
      ref.read(authControllerProvider.notifier).refreshMe();
    }
  }

  void _refresh() {
    if (ref.read(authControllerProvider).isLoggedIn) ref.read(unreadCountProvider.notifier).refresh();
  }

  @override
  Widget build(BuildContext context) {
    ref.listen(authControllerProvider, (prev, next) {
      if (next.isLoggedIn && prev?.isLoggedIn != true) _refresh();
    });
    return MaterialApp.router(
      title: 'مروّجو قلعة الضمان',
      debugShowCheckedModeBanner: false,
      theme: AppTheme.light(),
      routerConfig: ref.watch(routerProvider),
      locale: const Locale('ar', 'IQ'),
      supportedLocales: const [Locale('ar', 'IQ'), Locale('ar')],
      localizationsDelegates: const [
        GlobalMaterialLocalizations.delegate,
        GlobalWidgetsLocalizations.delegate,
        GlobalCupertinoLocalizations.delegate,
      ],
      builder: (context, child) {
        final mq = MediaQuery.of(context);
        return MediaQuery(
          data: mq.copyWith(textScaler: mq.textScaler.clamp(minScaleFactor: 0.9, maxScaleFactor: 1.3)),
          child: Directionality(textDirection: TextDirection.rtl, child: child ?? const SizedBox()),
        );
      },
    );
  }
}
