import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/config/app_config.dart';
import '../../../core/network/api_client.dart';
import '../../../core/network/json.dart';
import '../../../core/providers.dart';

class TrackPoint {
  const TrackPoint({required this.lat, required this.lng, required this.at, this.accuracy});

  final double lat;
  final double lng;
  final DateTime at;
  final double? accuracy;

  factory TrackPoint.fromJson(Json j) => TrackPoint(
        lat: asDoubleOrNull(j['lat']) ?? 0,
        lng: asDoubleOrNull(j['lng']) ?? 0,
        at: asDate(j['at']) ?? DateTime.now().toUtc(),
        accuracy: asDoubleOrNull(j['accuracy']),
      );
}

class WorkSession {
  const WorkSession({required this.id, required this.startedAt, this.endedAt, required this.minutes, this.endedBy});

  final int id;
  final DateTime startedAt;
  final DateTime? endedAt;
  final int minutes;

  /// `user` or `auto` (closed at midnight).
  final String? endedBy;

  bool get isOpen => endedAt == null;

  /// Live length of the session (an open session keeps growing).
  Duration get duration => (endedAt ?? DateTime.now().toUtc()).difference(startedAt);

  factory WorkSession.fromJson(Json j) => WorkSession(
        id: asInt(j['id']),
        startedAt: asDate(j['startedAt']) ?? DateTime.now().toUtc(),
        endedAt: asDate(j['endedAt']),
        minutes: asInt(j['minutes']),
        endedBy: asStringOrNull(j['endedBy']),
      );
}

/// One agent's working day: sessions, totals and the location trail.
class WorkDay {
  const WorkDay({
    required this.date,
    required this.isWorking,
    this.sessions = const [],
    this.track = const [],
    this.firstStartedAt,
    this.lastEndedAt,
  });

  final String date;
  final bool isWorking;
  final List<WorkSession> sessions;
  final List<TrackPoint> track;
  final DateTime? firstStartedAt;
  final DateTime? lastEndedAt;

  WorkSession? get current => sessions.where((s) => s.isOpen).firstOrNull;
  bool get startedToday => sessions.isNotEmpty;

  /// Total worked time today, counting the open session up to now.
  Duration get total => sessions.fold(Duration.zero, (sum, s) => sum + s.duration);

  factory WorkDay.fromJson(Json j) => WorkDay(
        date: asString(j['date']),
        isWorking: asBool(j['isWorking']),
        sessions: asList(j['sessions'], WorkSession.fromJson),
        track: asList(j['track'], TrackPoint.fromJson),
        firstStartedAt: asDate(j['firstStartedAt']),
        lastEndedAt: asDate(j['lastEndedAt']),
      );
}

class WorkRepository {
  WorkRepository(this._api);

  final ApiClient _api;

  /// [near] is only used by the demo backend to draw a sample morning route around the phone's position.
  Future<WorkDay> today({({double lat, double lng})? near}) async => WorkDay.fromJson(asJson(await _api.get('work/today', query: {
        if (AppConfig.useFakeApi && near != null) 'nearLat': near.lat,
        if (AppConfig.useFakeApi && near != null) 'nearLng': near.lng,
      })));

  Future<WorkDay> start({double? lat, double? lng}) async =>
      WorkDay.fromJson(asJson(await _api.post('work/start', body: {'latitude': lat, 'longitude': lng})));

  Future<WorkDay> end({double? lat, double? lng}) async =>
      WorkDay.fromJson(asJson(await _api.post('work/end', body: {'latitude': lat, 'longitude': lng})));

  /// Key for the background tracking service of the open working day.
  Future<String> trackingKey() async => asString(asJson(await _api.post('work/tracking-key'))['key']);

  /// Returns whether the server still considers the day started (false → stop tracking).
  Future<bool> sendLocations(List<Json> points) async {
    final j = asJson(await _api.post('work/locations', body: {'points': points}));
    return asBool(j['isWorking'], true);
  }
}

final workRepositoryProvider = Provider((ref) => WorkRepository(ref.watch(apiClientProvider)));
