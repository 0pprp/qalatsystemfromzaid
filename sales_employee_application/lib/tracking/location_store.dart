import 'package:sales_employee_application/tracking/work_shift.dart';

abstract class LocationPointStore {
  Future<void> init();
  Future<int> nextSequence(int shiftId);
  Future<void> insert(LocalLocationPoint point);
  Future<void> insertEvent(int? shiftId, String eventType);
  Future<List<LocalLocationPoint>> pending({int limit = 200});
  Future<void> markStatus(List<int> sequences, int shiftId, String status);
  Future<void> markFailed(List<int> sequences, int shiftId);
  Future<int> pendingCount();
  Future<List<LocalTrackingEvent>> pendingEvents();
  Future<void> markEventsSynced(List<int> ids);
}

class MemoryLocationStore implements LocationPointStore {
  final List<LocalLocationPoint> points = [];
  final Map<int, int> sequences = {};

  @override
  Future<void> init() async {}

  @override
  Future<int> nextSequence(int shiftId) async {
    final next = (sequences[shiftId] ?? 0) + 1;
    sequences[shiftId] = next;
    return next;
  }

  @override
  Future<void> insert(LocalLocationPoint point) async {
    if (points.any((p) => p.shiftId == point.shiftId && p.deviceSequence == point.deviceSequence)) {
      return;
    }
    points.add(point);
  }

  @override
  Future<void> insertEvent(int? shiftId, String eventType) async {}

  @override
  Future<List<LocalLocationPoint>> pending({int limit = 200}) async {
    return points.where((p) => p.syncStatus == 'Pending' || p.syncStatus == 'Failed').take(limit).toList();
  }

  @override
  Future<void> markStatus(List<int> sequences, int shiftId, String status) async {
    for (final p in points.where((p) => p.shiftId == shiftId && sequences.contains(p.deviceSequence))) {
      p.syncStatus = status;
    }
  }

  @override
  Future<void> markFailed(List<int> sequences, int shiftId) async {
    for (final p in points.where((p) => p.shiftId == shiftId && sequences.contains(p.deviceSequence))) {
      p.syncStatus = 'Failed';
      p.retryCount++;
    }
  }

  @override
  Future<int> pendingCount() async =>
      points.where((p) => p.syncStatus != 'Synced').length;

  @override
  Future<List<LocalTrackingEvent>> pendingEvents() async => [];

  @override
  Future<void> markEventsSynced(List<int> ids) async {}
}
