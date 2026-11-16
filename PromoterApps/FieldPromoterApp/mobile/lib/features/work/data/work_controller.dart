import 'dart:async';
import 'dart:convert';

import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:geolocator/geolocator.dart';

import '../../../core/config/app_config.dart';
import '../../../core/network/json.dart';
import '../../../core/providers.dart';
import '../../auth/data/auth_controller.dart';
import 'background_tracker.dart';
import 'work_repository.dart';

class WorkState {
  const WorkState({
    this.day,
    this.loading = false,
    this.busy = false,
    this.tracking = false,
    this.position,
    this.localTrack = const [],
    this.pending = 0,
    this.lastSentAt,
    this.error,
  });

  final WorkDay? day;
  final bool loading;

  /// Starting or finishing the day.
  final bool busy;

  /// The location stream is running.
  final bool tracking;
  final TrackPoint? position;

  /// Points recorded on this phone since the day was loaded (drawn after the server trail).
  final List<TrackPoint> localTrack;

  /// Points waiting to be sent (offline, or between sends).
  final int pending;
  final DateTime? lastSentAt;
  final String? error;

  bool get isWorking => day?.isWorking ?? false;

  /// Server trail + points recorded since, oldest first.
  List<TrackPoint> get fullTrack {
    final server = day?.track ?? const <TrackPoint>[];
    final after = server.isEmpty ? null : server.last.at;
    return [...server, ...localTrack.where((p) => after == null || p.at.isAfter(after))];
  }

  WorkState copyWith({
    WorkDay? day,
    bool? loading,
    bool? busy,
    bool? tracking,
    TrackPoint? position,
    List<TrackPoint>? localTrack,
    int? pending,
    DateTime? lastSentAt,
    String? error,
    bool clearError = false,
  }) =>
      WorkState(
        day: day ?? this.day,
        loading: loading ?? this.loading,
        busy: busy ?? this.busy,
        tracking: tracking ?? this.tracking,
        position: position ?? this.position,
        localTrack: localTrack ?? this.localTrack,
        pending: pending ?? this.pending,
        lastSentAt: lastSentAt ?? this.lastSentAt,
        error: clearError ? null : (error ?? this.error),
      );
}

/// «الدوام»: start/finish the working day and stream the phone's location to the server while it is started.
///
/// On Android, tracking runs in [BackgroundTracker]: a foreground service (persistent notification) with its own
/// isolate, so it keeps going while the app is in the background, swiped away or closed by the system. Elsewhere
/// (and in the demo/tests) the app records points itself while it is open. Points are buffered on the phone until
/// the server accepts them.
class WorkController extends Notifier<WorkState> {
  static const _kPending = 'work.pendingPoints';
  static const _kPrompted = 'work.promptedDate';
  static const _sendEvery = Duration(seconds: 45);
  static const _maxBuffered = 3000;

  /// A point is kept when the agent moved this far, or after [_keepAliveEvery] standing still. The heartbeat lets the
  /// dashboard tell «standing in one place» from «phone stopped sending».
  static const _minMoveMeters = 15.0;
  static const _keepAliveEvery = Duration(minutes: 2);
  TrackPoint? _lastKept;

  StreamSubscription<Position>? _positions;
  Timer? _sender;

  /// True while the background service does the tracking (this isolate only shows its points).
  bool _background = false;
  StreamSubscription<Map<String, dynamic>?>? _bgPoints;
  StreamSubscription<Map<String, dynamic>?>? _bgStatus;
  Timer? _retry;
  bool _sending = false;

  @override
  WorkState build() {
    ref.listen(isLoggedInProvider, (was, now) {
      if (now && was != true) load();
      if (!now) _stopTracking(clearBuffer: true);
    });
    ref.onDispose(_cancelStreams);
    if (ref.read(isLoggedInProvider)) Future.microtask(load);
    return WorkState(pending: _readPending().length);
  }

  WorkRepository get _repo => ref.read(workRepositoryProvider);

  Future<void> load() async {
    if (!ref.read(isLoggedInProvider)) return;
    state = state.copyWith(loading: true, clearError: true);
    try {
      // The demo draws a sample morning route around the phone, so it waits for a fix if none is cached.
      final near = state.position ?? await _lastKnown() ?? (AppConfig.useFakeApi ? await _currentPosition() : null);
      final day = await _repo.today(near: near == null ? null : (lat: near.lat, lng: near.lng));
      state = state.copyWith(day: day, loading: false, localTrack: const []);
      if (day.isWorking) {
        await _startTracking();
      } else {
        _stopTracking();
      }
    } catch (e) {
      state = state.copyWith(loading: false, error: 'تعذّر تحميل الدوام');
    }
  }

  Future<String?>? _ensuring;

  /// Starts «الدوام» if it is not running (a sale request or a shop pin means the agent is working). Returns null
  /// when the day is running (or was already), otherwise the reason it could not start. Concurrent calls share one try.
  Future<String?> ensureWorking() => _ensuring ??= _ensureWorking().whenComplete(() => _ensuring = null);

  Future<String?> _ensureWorking() async {
    if (state.day == null) await load();
    if (state.isWorking) return null;
    return startDay();
  }

  /// Returns an error message to show, or null on success.
  Future<String?> startDay() async {
    final permissionError = await _ensurePermission();
    if (permissionError != null) return permissionError;
    state = state.copyWith(busy: true, clearError: true);
    try {
      final pos = await _currentPosition();
      final day = await _repo.start(lat: pos?.lat, lng: pos?.lng);
      _markPrompted();
      state = state.copyWith(day: day, busy: false, position: pos, localTrack: const []);
      await _startTracking(newKey: true);
      return null;
    } catch (e) {
      state = state.copyWith(busy: false);
      return 'تعذّر بدء الدوام. تحقق من الاتصال وحاول مجدداً.';
    }
  }

  Future<String?> endDay() async {
    state = state.copyWith(busy: true, clearError: true);
    try {
      await _flush();
      // The background service sends what it has buffered before the day is closed.
      if (_background) await BackgroundTracker.finish();
      final pos = state.position ?? await _lastKnown();
      final day = await _repo.end(lat: pos?.lat, lng: pos?.lng);
      _stopTracking();
      state = state.copyWith(day: day, busy: false, localTrack: const []);
      return null;
    } catch (e) {
      state = state.copyWith(busy: false);
      return 'تعذّر إنهاء الدوام. تحقق من الاتصال وحاول مجدداً.';
    }
  }

  /// True once a day, on the first open of the app while the agent is not working.
  bool get shouldPromptToday {
    final day = state.day;
    if (day == null || day.isWorking) return false;
    return ref.read(sharedPrefsProvider).getString(_kPrompted) != _todayKey();
  }

  void _markPrompted() => ref.read(sharedPrefsProvider).setString(_kPrompted, _todayKey());

  void dismissPrompt() => _markPrompted();

  static String _todayKey() {
    final now = DateTime.now();
    return '${now.year}-${now.month}-${now.day}';
  }

  // ---------------------------------------------------------------- tracking

  Future<void> _startTracking({bool newKey = false}) async {
    if (_positions != null || (_background && !newKey)) return;
    if (await _ensurePermission() != null) {
      state = state.copyWith(error: 'إذن الموقع غير ممنوح: لا يتم تسجيل موقعك');
      return;
    }
    if (await _startBackground(newKey: newKey)) return;
    final settings = defaultTargetPlatform == TargetPlatform.android
        ? AndroidSettings(
            accuracy: LocationAccuracy.high,
            distanceFilter: 0,
            intervalDuration: const Duration(seconds: 20),
            foregroundNotificationConfig: const ForegroundNotificationConfig(
              notificationTitle: 'الدوام مستمر',
              notificationText: 'يتم تسجيل موقعك أثناء الدوام',
              enableWakeLock: true,
              setOngoing: true,
            ),
          )
        : const LocationSettings(accuracy: LocationAccuracy.high, distanceFilter: 0);
    _positions = Geolocator.getPositionStream(locationSettings: settings).listen(
      _onPosition,
      onError: (_) {
        // GPS switched off, or the system location dialog was open: retry shortly instead of giving up.
        _positions?.cancel();
        _positions = null;
        state = state.copyWith(error: 'تعذّر قراءة الموقع (GPS) — إعادة المحاولة…');
        _retry?.cancel();
        _retry = Timer(const Duration(seconds: 15), () {
          if (state.isWorking) _startTracking();
        });
      },
    );
    _sender ??= Timer.periodic(_sendEvery, (_) => _flush());
    state = state.copyWith(tracking: true, clearError: true);
    unawaited(_flush());
  }

  /// Hands tracking to the background service (keeps running with the app closed). False when it is not available
  /// (not Android, the demo, tests) and the app must record points itself.
  Future<bool> _startBackground({required bool newKey}) async {
    if (!BackgroundTracker.isSupported || AppConfig.useFakeApi) return false;
    try {
      if (newKey || !await BackgroundTracker.isRunning()) {
        final key = await _repo.trackingKey();
        await BackgroundTracker.start(key: key, apiBaseUrl: AppConfig.apiBaseUrl);
      }
      _background = true;
      _bgPoints ??= BackgroundTracker.points.listen((e) {
        if (e == null) return;
        final point = TrackPoint(
          lat: (e['lat'] as num).toDouble(),
          lng: (e['lng'] as num).toDouble(),
          at: DateTime.tryParse('${e['at']}') ?? DateTime.now().toUtc(),
          accuracy: (e['accuracy'] as num?)?.toDouble(),
        );
        final track = [...state.localTrack, point];
        state = state.copyWith(position: point, localTrack: track.length > 2000 ? track.sublist(track.length - 2000) : track);
      });
      _bgStatus ??= BackgroundTracker.status.listen((e) {
        if (e == null) return;
        state = state.copyWith(pending: (e['pending'] as num?)?.toInt(), lastSentAt: DateTime.tryParse('${e['sentAt']}'));
      });
      state = state.copyWith(tracking: true, clearError: true);
      unawaited(_flush()); // points recorded by an older app version
      return true;
    } catch (_) {
      return false;
    }
  }

  /// Stops the location stream and timers (no state change: also used while the provider is disposed).
  /// The background service is left running: it must outlive the app.
  void _cancelStreams() {
    _bgPoints?.cancel();
    _bgPoints = null;
    _bgStatus?.cancel();
    _bgStatus = null;
    _positions?.cancel();
    _positions = null;
    _lastKept = null;
    _sender?.cancel();
    _sender = null;
    _retry?.cancel();
    _retry = null;
  }

  void _stopTracking({bool clearBuffer = false}) {
    _cancelStreams();
    if (_background || clearBuffer) {
      _background = false;
      if (BackgroundTracker.isSupported && !AppConfig.useFakeApi) unawaited(BackgroundTracker.stop().catchError((_) {}));
    }
    if (clearBuffer) ref.read(sharedPrefsProvider).remove(_kPending);
    state = state.copyWith(tracking: false, pending: clearBuffer ? 0 : null);
  }

  void _onPosition(Position p) {
    final point = TrackPoint(lat: p.latitude, lng: p.longitude, at: p.timestamp.toUtc(), accuracy: p.accuracy);
    final last = _lastKept;
    if (last != null &&
        Geolocator.distanceBetween(last.lat, last.lng, point.lat, point.lng) < _minMoveMeters &&
        point.at.difference(last.at) < _keepAliveEvery) {
      // Same place, too soon: only refresh the dot on the map.
      state = state.copyWith(position: point, clearError: true);
      return;
    }
    _lastKept = point;
    final buffer = _readPending()
      ..add({
        'latitude': double.parse(p.latitude.toStringAsFixed(6)),
        'longitude': double.parse(p.longitude.toStringAsFixed(6)),
        'accuracy': p.accuracy,
        'speed': p.speed,
        'recordedAt': point.at.toIso8601String(),
      });
    _writePending(buffer.length > _maxBuffered ? buffer.sublist(buffer.length - _maxBuffered) : buffer);
    state = state.copyWith(position: point, localTrack: [...state.localTrack, point], pending: buffer.length, clearError: true);
    if (buffer.length >= 20) unawaited(_flush());
  }

  Future<void> _flush() async {
    if (_sending) return;
    final buffer = _readPending();
    if (buffer.isEmpty) return;
    _sending = true;
    try {
      final batch = buffer.take(500).toList();
      final stillWorking = await _repo.sendLocations(batch);
      final rest = _readPending().skip(batch.length).toList();
      _writePending(rest);
      state = state.copyWith(pending: rest.length, lastSentAt: DateTime.now());
      if (!stillWorking) {
        // Finished elsewhere (dashboard / auto-close at midnight): stop and refresh.
        _stopTracking(clearBuffer: true);
        unawaited(load());
      }
    } catch (_) {
      // Offline: keep the buffer and retry on the next tick.
    } finally {
      _sending = false;
    }
  }

  List<Json> _readPending() {
    final raw = ref.read(sharedPrefsProvider).getString(_kPending);
    if (raw == null) return [];
    try {
      return (jsonDecode(raw) as List).whereType<Map>().map((e) => Map<String, dynamic>.from(e)).toList();
    } catch (_) {
      return [];
    }
  }

  void _writePending(List<Json> points) => ref.read(sharedPrefsProvider).setString(_kPending, jsonEncode(points));

  // ---------------------------------------------------------------- location helpers

  Future<String?> _ensurePermission() async {
    try {
      if (!await Geolocator.isLocationServiceEnabled()) return 'خدمة الموقع (GPS) متوقفة. فعّلها من إعدادات الهاتف.';
      var perm = await Geolocator.checkPermission();
      if (perm == LocationPermission.denied) perm = await Geolocator.requestPermission();
      if (perm == LocationPermission.deniedForever) {
        await Geolocator.openAppSettings();
        return 'إذن الموقع مرفوض. فعّله من إعدادات التطبيق ثم حاول مجدداً.';
      }
      if (perm == LocationPermission.denied) return 'يجب السماح بالوصول إلى الموقع لبدء الدوام.';
      return null;
    } catch (_) {
      return 'تعذّر الوصول إلى الموقع';
    }
  }

  Future<TrackPoint?> _currentPosition() async {
    try {
      final p = await Geolocator.getCurrentPosition(
        locationSettings: const LocationSettings(accuracy: LocationAccuracy.high, timeLimit: Duration(seconds: 15)),
      );
      return TrackPoint(lat: p.latitude, lng: p.longitude, at: p.timestamp.toUtc(), accuracy: p.accuracy);
    } catch (_) {
      return _lastKnown();
    }
  }

  Future<TrackPoint?> _lastKnown() async {
    try {
      final p = await Geolocator.getLastKnownPosition();
      return p == null ? null : TrackPoint(lat: p.latitude, lng: p.longitude, at: p.timestamp.toUtc(), accuracy: p.accuracy);
    } catch (_) {
      return null;
    }
  }
}

final workControllerProvider = NotifierProvider<WorkController, WorkState>(WorkController.new);
