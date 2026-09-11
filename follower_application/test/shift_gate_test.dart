import 'package:follower_application/tracking/shift_gate_coordinator.dart';
import 'package:follower_application/tracking/work_shift.dart';
import 'package:follower_application/services/follower_tracking_repository.dart';
import 'package:flutter_test/flutter_test.dart';

class _FakeRepo implements FollowerTrackingRepository {
  _FakeRepo({this.current});
  WorkShift? current;
  bool endCalled = false;

  @override
  Future<WorkShift?> currentShift() async => current;

  @override
  Future<void> endShift() async {
    endCalled = true;
    current = null;
  }

  @override
  Future<WorkShift> startShift() async {
    current = WorkShift(
      shiftId: 9,
      status: 'Active',
      hasActiveShift: true,
      startedAtUtc: DateTime.now().toUtc(),
      cutoffAtUtc: DateTime.now().toUtc().add(const Duration(hours: 8)),
    );
    return current!;
  }

  @override
  Future<void> recordTrackingEvent(int? shiftId, String eventType) async {}

  @override
  Future<LocationBatchResult> uploadLocationBatch(
          int shiftId, List<LocalLocationPoint> points) async =>
      LocationBatchResult(accepted: 0);
}

void main() {
  test('operational routes are gated', () {
    expect(ShiftGateCoordinator.isOperationalRoute('/HomePage'), isTrue);
    expect(ShiftGateCoordinator.isOperationalRoute('/CustomerProfile'), isTrue);
    expect(ShiftGateCoordinator.isOperationalRoute('/FollowerSalesRequest'), isTrue);
    expect(ShiftGateCoordinator.isOperationalRoute('/StartShift'), isFalse);
    expect(ShiftGateCoordinator.isOperationalRoute('/Login'), isFalse);
  });

  test('routeFor maps destinations', () {
    expect(ShiftGateCoordinator.routeFor(ShiftGateDestination.login), '/Login');
    expect(ShiftGateCoordinator.routeFor(ShiftGateDestination.startShift),
        '/StartShift');
    expect(ShiftGateCoordinator.routeFor(ShiftGateDestination.home), '/HomePage');
  });

  test('no active shift object is not home-eligible', () async {
    final fake = _FakeRepo(current: null);
    expect(await fake.currentShift(), isNull);
    expect(ShiftGateCoordinator.routeFor(ShiftGateDestination.startShift),
        isNot('/HomePage'));
  });

  test('active shift object is home-eligible', () {
    final shift = WorkShift(
      shiftId: 3,
      status: 'Active',
      hasActiveShift: true,
      startedAtUtc: DateTime.now().toUtc(),
      cutoffAtUtc: DateTime.now().toUtc().add(const Duration(hours: 1)),
    );
    expect(shift.isActive, isTrue);
    expect(ShiftGateCoordinator.routeFor(ShiftGateDestination.home), '/HomePage');
  });

  test('end shift clears active and targets StartShift', () async {
    final repo = _FakeRepo(
      current: WorkShift(
        shiftId: 1,
        status: 'Active',
        hasActiveShift: true,
        startedAtUtc: DateTime.now().toUtc(),
        cutoffAtUtc: DateTime.now().toUtc().add(const Duration(hours: 1)),
      ),
    );
    await repo.endShift();
    expect(repo.endCalled, isTrue);
    expect(await repo.currentShift(), isNull);
    expect(ShiftGateCoordinator.routeFor(ShiftGateDestination.startShift),
        '/StartShift');
  });

  test('direct Home without shift must be redirected by gate', () {
    expect(ShiftGateCoordinator.isOperationalRoute('/HomePage'), isTrue);
    expect(ShiftGateCoordinator.routeFor(ShiftGateDestination.startShift),
        '/StartShift');
  });
}
