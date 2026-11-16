import 'dart:async';
import 'dart:convert';
import 'dart:ui';

import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter/widgets.dart';
import 'package:flutter_background_service/flutter_background_service.dart';
import 'package:geolocator/geolocator.dart';
import 'package:shared_preferences/shared_preferences.dart';

const _kKey = 'bg_track.key';
const _kUrl = 'bg_track.url';
const _kPending = 'bg_track.pending';

/// Records the agent's location during «الدوام» in an Android foreground service that keeps running when the app is
/// in the background, swiped away from recent apps, or closed by the system. The service runs its own Dart isolate,
/// buffers points on the phone and sends them every minute to `work/track` with the day's tracking key (not the
/// sign-in tokens). It stops itself when the server says the day has ended.
class BackgroundTracker {
  BackgroundTracker._();

  static final _service = FlutterBackgroundService();
  static bool _configured = false;

  static bool get isSupported => !kIsWeb && defaultTargetPlatform == TargetPlatform.android;

  static Future<void> _configure() async {
    if (_configured) return;
    await _service.configure(
      androidConfiguration: AndroidConfiguration(
        onStart: trackingServiceMain,
        autoStart: false,
        autoStartOnBoot: false,
        isForegroundMode: true,
        initialNotificationTitle: 'الدوام مستمر',
        initialNotificationContent: 'يتم تسجيل موقعك أثناء الدوام',
        foregroundServiceNotificationId: 4401,
        foregroundServiceTypes: [AndroidForegroundType.location],
      ),
      iosConfiguration: IosConfiguration(autoStart: false, onForeground: trackingServiceMain),
    );
    _configured = true;
  }

  static Future<bool> isRunning() async {
    await _configure();
    return _service.isRunning();
  }

  /// Starts the service, or hands a new key to the running one.
  static Future<void> start({required String key, required String apiBaseUrl}) async {
    await _configure();
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(_kKey, key);
    await prefs.setString(_kUrl, apiBaseUrl);
    if (await _service.isRunning()) {
      _service.invoke('key', {'key': key});
    } else {
      await _service.startService();
    }
  }

  /// Sends what is buffered, then stops (end of the day). Waits a few seconds at most.
  static Future<void> finish() async {
    await _configure();
    if (!await _service.isRunning()) return;
    _service.invoke('finish');
    for (var i = 0; i < 16 && await _service.isRunning(); i++) {
      await Future<void>.delayed(const Duration(milliseconds: 500));
    }
  }

  /// Stops at once and forgets the key and buffer (sign-out).
  static Future<void> stop() async {
    await _configure();
    final prefs = await SharedPreferences.getInstance();
    await prefs.remove(_kKey);
    await prefs.remove(_kPending);
    if (await _service.isRunning()) _service.invoke('stop');
  }

  /// Each recorded point: {lat, lng, at, accuracy}.
  static Stream<Map<String, dynamic>?> get points => _service.on('point');

  /// After each send: {pending, sentAt}.
  static Stream<Map<String, dynamic>?> get status => _service.on('status');
}

/// Entry point of the background isolate.
@pragma('vm:entry-point')
Future<void> trackingServiceMain(ServiceInstance service) async {
  WidgetsFlutterBinding.ensureInitialized();
  DartPluginRegistrant.ensureInitialized();
  final prefs = await SharedPreferences.getInstance();
  await prefs.reload();
  var key = prefs.getString(_kKey);
  final url = prefs.getString(_kUrl);
  if (key == null || url == null) {
    await service.stopSelf();
    return;
  }
  if (service is AndroidServiceInstance) {
    await service.setForegroundNotificationInfo(title: 'الدوام مستمر', content: 'يتم تسجيل موقعك أثناء الدوام');
  }

  final dio = Dio(BaseOptions(
    baseUrl: url,
    connectTimeout: const Duration(seconds: 20),
    receiveTimeout: const Duration(seconds: 30),
    contentType: Headers.jsonContentType,
  ));

  var pending = <Map<String, dynamic>>[];
  try {
    pending = (jsonDecode(prefs.getString(_kPending) ?? '[]') as List).cast<Map<String, dynamic>>();
  } catch (_) {}

  StreamSubscription<Position>? positions;
  Timer? sender;
  var sending = false;
  ({double lat, double lng, DateTime at})? lastKept;

  Future<void> save() => prefs.setString(_kPending, jsonEncode(pending));

  Future<void> shutdown({bool forget = true}) async {
    await positions?.cancel();
    sender?.cancel();
    if (forget) {
      await prefs.remove(_kKey);
      await prefs.remove(_kPending);
    }
    await service.stopSelf();
  }

  /// Returns false when the server says the day has ended.
  Future<bool> flush() async {
    if (sending || pending.isEmpty) return true;
    sending = true;
    try {
      final batch = pending.take(500).toList();
      final res = await dio.post<dynamic>('work/track',
          data: {'points': batch}, options: Options(headers: {'X-Tracking-Key': key}));
      pending = pending.skip(batch.length).toList();
      await save();
      service.invoke('status', {'pending': pending.length, 'sentAt': DateTime.now().toUtc().toIso8601String()});
      final data = res.data;
      return !(data is Map && data['isWorking'] == false);
    } catch (_) {
      return true; // offline: keep the buffer and try again later
    } finally {
      sending = false;
    }
  }

  service.on('key').listen((e) {
    final k = e?['key'] as String?;
    if (k != null) key = k;
  });
  service.on('finish').listen((_) async {
    await flush();
    await shutdown();
  });
  service.on('stop').listen((_) => shutdown());

  positions = Geolocator.getPositionStream(
    locationSettings: AndroidSettings(
      accuracy: LocationAccuracy.high,
      distanceFilter: 0,
      intervalDuration: const Duration(seconds: 20),
    ),
  ).listen((p) {
    final at = p.timestamp.toUtc();
    final last = lastKept;
    // Keep a point when the agent moved 15 m, or every 2 minutes standing still (the dashboard's heartbeat).
    if (last != null &&
        Geolocator.distanceBetween(last.lat, last.lng, p.latitude, p.longitude) < 15 &&
        at.difference(last.at) < const Duration(minutes: 2)) {
      return;
    }
    lastKept = (lat: p.latitude, lng: p.longitude, at: at);
    pending.add({
      'latitude': double.parse(p.latitude.toStringAsFixed(6)),
      'longitude': double.parse(p.longitude.toStringAsFixed(6)),
      'accuracy': p.accuracy,
      'speed': p.speed,
      'recordedAt': at.toIso8601String(),
    });
    if (pending.length > 5000) pending = pending.sublist(pending.length - 5000);
    unawaited(save());
    service.invoke('point', {'lat': p.latitude, 'lng': p.longitude, 'at': at.toIso8601String(), 'accuracy': p.accuracy});
  }, onError: (_) {});

  sender = Timer.periodic(const Duration(seconds: 60), (_) async {
    if (!await flush()) await shutdown();
  });
  if (!await flush()) await shutdown();
}
