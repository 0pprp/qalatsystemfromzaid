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

/// Today's daily-target state, asked before «إنهاء الدوام». The target itself is never sent to the app.
class TargetStatus {
  const TargetStatus({required this.hasTarget, required this.reached, required this.workedText});

  final bool hasTarget;
  final bool reached;

  /// Time worked so far, ready to show: «ساعتين و5 دقائق».
  final String workedText;

  /// The day would be closed before the target.
  bool get endsEarly => hasTarget && !reached;

  factory TargetStatus.fromJson(Json j) => TargetStatus(
        hasTarget: asBool(j['hasTarget']),
        reached: asBool(j['reached']),
        workedText: asString(j['workedText']),
      );
}

/// «حققت هدفك اليومي»: the numbers of the day the target was reached on.
class TargetSummary {
  const TargetSummary({
    required this.id,
    required this.date,
    required this.isToday,
    required this.isYesterday,
    required this.stores,
    required this.orders,
    required this.sales,
  });

  final int id;

  /// Iraq day, `yyyy-MM-dd`.
  final String date;
  final bool isToday;
  final bool isYesterday;
  final int stores;
  final int orders;
  final int sales;

  /// Noon (Iraq) of [date], so date formatting lands on the right day.
  DateTime? get day {
    final d = DateTime.tryParse('${date}T09:00:00Z');
    return d;
  }

  factory TargetSummary.fromJson(Json j) => TargetSummary(
        id: asInt(j['id']),
        date: asString(j['date']),
        isToday: asBool(j['isToday']),
        isYesterday: asBool(j['isYesterday']),
        stores: asInt(j['stores']),
        orders: asInt(j['orders']),
        sales: asInt(j['sales']),
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

  Future<TargetStatus> targetStatus() async => TargetStatus.fromJson(asJson(await _api.get('work/target-status')));

  /// The «حققت هدفك اليومي» summary not closed yet, or null.
  Future<TargetSummary?> targetSummary() async {
    final s = asJson(await _api.get('work/target-summary'))['summary'];
    return s is Map ? TargetSummary.fromJson(asJson(s)) : null;
  }

  Future<void> markTargetSummarySeen(int id) => _api.post('work/target-summary/$id/seen');

  /// Key for the background tracking service of the open working day.
  Future<String> trackingKey() async => asString(asJson(await _api.post('work/tracking-key'))['key']);

  /// Returns whether the server still considers the day started (false → stop tracking).
  Future<bool> sendLocations(List<Json> points) async {
    final j = asJson(await _api.post('work/locations', body: {'points': points}));
    return asBool(j['isWorking'], true);
  }
}

final workRepositoryProvider = Provider((ref) => WorkRepository(ref.watch(apiClientProvider)));
