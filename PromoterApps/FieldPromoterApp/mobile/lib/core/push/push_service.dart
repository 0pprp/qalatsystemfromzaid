import 'dart:async';
import 'dart:convert';
import 'dart:io' show Platform;

import 'package:firebase_core/firebase_core.dart';
import 'package:firebase_messaging/firebase_messaging.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter/painting.dart' show Color;
import 'package:flutter_local_notifications/flutter_local_notifications.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../features/auth/data/auth_repository.dart';

/// Background FCM handler (must be a top-level function).
@pragma('vm:entry-point')
Future<void> firebaseMessagingBackgroundHandler(RemoteMessage message) async {
  try {
    await Firebase.initializeApp();
  } catch (_) {}
  // Notification messages are displayed by the OS; nothing else to do here.
}

/// Maps the push `data` payload (`{ type, notificationId, orderId? }`) to an app route.
String routeForPushData(Map<String, dynamic> data) {
  final orderId = data['orderId']?.toString();
  if (orderId != null && int.tryParse(orderId) != null) return '/orders/$orderId';
  // Daily-target messages open the working-day tab (the «حققت هدفك» summary pops up by itself).
  final kind = data['kind']?.toString();
  if (kind == 'target_reached' || kind == 'target_idle') return '/work';
  return '/notifications';
}

/// Push notifications. Fully tolerant of missing Firebase configuration:
/// without `google-services.json` / `GoogleService-Info.plist` it silently disables itself.
class PushService {
  PushService(this._ref);

  final Ref _ref;
  bool _available = false;
  bool _initialized = false;
  String? _token;
  StreamSubscription<String>? _tokenSub;
  final _local = FlutterLocalNotificationsPlugin();

  /// Emits whenever a push arrives in the foreground (used to refresh the unread badge).
  final _foreground = StreamController<RemoteMessage>.broadcast();
  Stream<RemoteMessage> get foregroundMessages => _foreground.stream;

  /// Set by the app once the router exists; receives a route to open.
  void Function(String location)? onNavigate;
  String? _pendingRoute;

  bool get isAvailable => _available;
  String? get currentToken => _token;

  static const _channel = AndroidNotificationChannel(
    'qalaa_default',
    'إشعارات قلعة الضمان',
    description: 'تحديثات الطلبات ورسائل الإدارة',
    importance: Importance.high,
  );

  Future<void> init() async {
    if (_initialized) return;
    _initialized = true;
    try {
      await Firebase.initializeApp();
      _available = true;
    } catch (e) {
      // No Firebase config files: push is disabled, everything else works.
      debugPrint('Push disabled (Firebase not configured): $e');
      _available = false;
      return;
    }

    try {
      FirebaseMessaging.onBackgroundMessage(firebaseMessagingBackgroundHandler);

      await _local.initialize(
        settings: const InitializationSettings(
          android: AndroidInitializationSettings('ic_stat_notify'),
          iOS: DarwinInitializationSettings(
            requestAlertPermission: false,
            requestBadgePermission: false,
            requestSoundPermission: false,
          ),
        ),
        onDidReceiveNotificationResponse: (resp) {
          final payload = resp.payload;
          if (payload == null) return;
          try {
            _open(Map<String, dynamic>.from(jsonDecode(payload) as Map));
          } catch (_) {}
        },
      );
      await _local
          .resolvePlatformSpecificImplementation<AndroidFlutterLocalNotificationsPlugin>()
          ?.createNotificationChannel(_channel);

      final messaging = FirebaseMessaging.instance;
      await messaging.setForegroundNotificationPresentationOptions(alert: true, badge: true, sound: true);

      FirebaseMessaging.onMessage.listen((m) {
        _foreground.add(m);
        // iOS shows foreground notifications natively (options above); Android needs a local one.
        if (!kIsWeb && Platform.isAndroid && m.notification != null) {
          _local.show(
            id: m.hashCode,
            title: m.notification!.title,
            body: m.notification!.body,
            notificationDetails: NotificationDetails(
              android: AndroidNotificationDetails(
                _channel.id,
                _channel.name,
                channelDescription: _channel.description,
                importance: Importance.high,
                priority: Priority.high,
                icon: 'ic_stat_notify',
                color: Color(0xFF006A64),
              ),
            ),
            payload: jsonEncode(m.data),
          );
        }
      });
      FirebaseMessaging.onMessageOpenedApp.listen((m) => _open(m.data));
      final initial = await messaging.getInitialMessage();
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

  /// Called by the app when the router is ready; returns a route from a cold-start tap, if any.
  String? takePendingRoute() {
    final r = _pendingRoute;
    _pendingRoute = null;
    return r;
  }

  /// After login: optionally ask for permission (sensible moment), then register the token.
  Future<void> onLoggedIn({required bool requestPermission}) async {
    if (!_available) return;
    try {
      final messaging = FirebaseMessaging.instance;
      if (requestPermission) {
        await messaging.requestPermission(alert: true, badge: true, sound: true);
      }
      final settings = await messaging.getNotificationSettings();
      if (settings.authorizationStatus == AuthorizationStatus.denied) return;
      // The backend (contract §3 `me/devices`) expects a classic FCM registration token.
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
      await _ref.read(authRepositoryProvider).registerDevice(token);
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
