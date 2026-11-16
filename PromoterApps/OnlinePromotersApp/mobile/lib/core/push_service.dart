import 'dart:async';
import 'dart:convert';

import 'package:firebase_core/firebase_core.dart';
import 'package:firebase_messaging/firebase_messaging.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter/painting.dart' show Color;
import 'package:flutter_local_notifications/flutter_local_notifications.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'providers.dart';

/// Background handler (must be top level). Android shows notification messages by itself.
@pragma('vm:entry-point')
Future<void> firebaseMessagingBackgroundHandler(RemoteMessage message) async {
  try {
    await Firebase.initializeApp();
  } catch (_) {}
}

/// Where a tapped notification opens: the order, the withdrawal chat, the wallet, or the notifications list.
String routeForPushData(Map<String, dynamic> data) {
  final orderId = int.tryParse(data['orderId']?.toString() ?? '');
  if (orderId != null) return '/orders/$orderId';
  final payoutId = int.tryParse(data['payoutId']?.toString() ?? '');
  if (payoutId != null) return '/payouts/$payoutId';
  if (data['type'] == 'Wallet') return '/wallet';
  return '/notifications';
}

/// Phone notifications through Firebase (project qalaa-agents, app iq.qalaa.promoters_app).
/// Any Firebase problem only disables push: the in-app bell keeps working.
class PushService {
  PushService(this._ref);

  final Ref _ref;
  bool _available = false;
  bool _initialized = false;
  String? _token;
  StreamSubscription<String>? _tokenSub;
  final _local = FlutterLocalNotificationsPlugin();

  final _foreground = StreamController<RemoteMessage>.broadcast();

  /// A push arrived while the app was open (refresh the bell and the numbers).
  Stream<RemoteMessage> get foregroundMessages => _foreground.stream;

  /// Set by the app once the router exists.
  void Function(String location)? onNavigate;
  String? _pendingRoute;

  String? get currentToken => _token;

  static const _channel = AndroidNotificationChannel(
    'qalaa_default',
    'إشعارات المروّجين',
    description: 'المبيعات والعمولات وطلبات السحب ورسائل الإدارة',
    importance: Importance.high,
  );

  Future<void> init() async {
    if (_initialized) return;
    _initialized = true;
    try {
      await Firebase.initializeApp();
      _available = true;
    } catch (e) {
      debugPrint('Push disabled: $e');
      return;
    }
    try {
      FirebaseMessaging.onBackgroundMessage(firebaseMessagingBackgroundHandler);
      await _local.initialize(
        settings: const InitializationSettings(android: AndroidInitializationSettings('ic_stat_notify')),
        onDidReceiveNotificationResponse: (resp) {
          final payload = resp.payload;
          if (payload == null) return;
          try {
            _open(Map<String, dynamic>.from(jsonDecode(payload) as Map));
          } catch (_) {}
        },
      );
      await _local.resolvePlatformSpecificImplementation<AndroidFlutterLocalNotificationsPlugin>()?.createNotificationChannel(_channel);

      // With the app open Android shows nothing by itself, so a local notification is shown.
      FirebaseMessaging.onMessage.listen((m) {
        _foreground.add(m);
        final n = m.notification;
        if (n == null) return;
        _local.show(
          id: m.hashCode,
          title: n.title,
          body: n.body,
          notificationDetails: NotificationDetails(
            android: AndroidNotificationDetails(
              _channel.id,
              _channel.name,
              channelDescription: _channel.description,
              importance: Importance.high,
              priority: Priority.high,
              icon: 'ic_stat_notify',
              color: const Color(0xFF006A64),
            ),
          ),
          payload: jsonEncode(m.data),
        );
      });
      FirebaseMessaging.onMessageOpenedApp.listen((m) => _open(m.data));
      final initial = await FirebaseMessaging.instance.getInitialMessage();
      if (initial != null) _open(initial.data);
    } catch (e) {
      debugPrint('Push init error: $e');
    }
  }

  void _open(Map<String, dynamic> data) {
    final route = routeForPushData(data);
    if (onNavigate != null) {
      onNavigate!(route);
    } else {
      _pendingRoute = route;
    }
  }

  /// A route from a notification tapped while the app was closed.
  String? takePendingRoute() {
    final r = _pendingRoute;
    _pendingRoute = null;
    return r;
  }

  /// The token to send with the sign-in request (null when push is unavailable).
  Future<String?> tokenForLogin() async {
    if (!_available) return null;
    try {
      // ignore: deprecated_member_use
      return _token = await FirebaseMessaging.instance.getToken().timeout(const Duration(seconds: 6));
    } catch (_) {
      return null;
    }
  }

  /// When signed in: ask for permission (Android 13+ shows it only while undecided) and keep the token registered.
  Future<void> onLoggedIn() async {
    if (!_available) return;
    try {
      final messaging = FirebaseMessaging.instance;
      await messaging.requestPermission(alert: true, badge: true, sound: true);
      // ignore: deprecated_member_use
      final token = await messaging.getToken();
      if (token != null) await _register(token);
      await _tokenSub?.cancel();
      // ignore: deprecated_member_use
      _tokenSub = messaging.onTokenRefresh.listen(_register);
    } catch (e) {
      debugPrint('Push registration error: $e');
    }
  }

  Future<void> _register(String token) async {
    _token = token;
    try {
      await _ref.read(apiClientProvider).post('me/devices', body: {
        'platform': 'android',
        'pushToken': token,
        'appVersion': _ref.read(packageInfoProvider).version,
      });
    } catch (e) {
      debugPrint('me/devices failed: $e');
    }
  }

  void onLoggedOut() {
    _tokenSub?.cancel();
    _tokenSub = null;
    _token = null;
  }
}

final pushServiceProvider = Provider<PushService>((ref) => PushService(ref));
