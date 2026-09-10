import 'package:follower_application/services/follower_tracking_api.dart';
import 'package:follower_application/tracking/work_shift.dart';

abstract class FollowerTrackingRepository {
  Future<WorkShift> startShift();
  Future<void> endShift();
  Future<WorkShift?> currentShift();
  Future<LocationBatchResult> uploadLocationBatch(
      int shiftId, List<LocalLocationPoint> points);
  Future<void> recordTrackingEvent(int? shiftId, String eventType);
}

class ApiFollowerTrackingRepository implements FollowerTrackingRepository {
  @override
  Future<WorkShift> startShift() async {
    final raw = await FollowerTrackingApi.post('Followers/shifts/start');
    return WorkShift.fromJson(Map<String, dynamic>.from(raw as Map));
  }

  @override
  Future<void> endShift() async {
    await FollowerTrackingApi.post('Followers/shifts/end');
  }

  @override
  Future<WorkShift?> currentShift() async {
    final raw = await FollowerTrackingApi.get('Followers/shifts/current');
    if (raw is Map &&
        (raw['hasActiveShift'] == false || raw['HasActiveShift'] == false)) {
      return null;
    }
    if (raw is Map) {
      final shift = WorkShift.fromJson(Map<String, dynamic>.from(raw));
      return shift.hasActiveShift && shift.shiftId > 0 ? shift : null;
    }
    return null;
  }

  @override
  Future<LocationBatchResult> uploadLocationBatch(
      int shiftId, List<LocalLocationPoint> points) async {
    final raw = await FollowerTrackingApi.post('Followers/location/batch', body: {
      'shiftId': shiftId,
      'points': points.map((e) => e.toBatchJson()).toList(),
    });
    return LocationBatchResult.fromJson(Map<String, dynamic>.from(raw as Map));
  }

  @override
  Future<void> recordTrackingEvent(int? shiftId, String eventType) async {
    try {
      await FollowerTrackingApi.post('Followers/tracking/events', body: {
        'shiftId': shiftId,
        'eventType': eventType,
        'occurredAtUtc': DateTime.now().toUtc().toIso8601String(),
      });
    } on FollowerApiException {
      return;
    }
  }
}
