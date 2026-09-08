import 'dart:async';

import 'package:connectivity_plus/connectivity_plus.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sales_employee_application/data/mock_sales_repository.dart';
import 'package:sales_employee_application/data/sales_repository_factory.dart';
import 'package:sales_employee_application/screens/home_screen.dart';
import 'package:sales_employee_application/screens/shift_screen.dart';
import 'package:sales_employee_application/tracking/location_store.dart';
import 'package:sales_employee_application/tracking/location_sync_engine.dart';
import 'package:sales_employee_application/tracking/shift_tracking_controller.dart';
import 'package:sales_employee_application/tracking/official_slot.dart';
import 'package:sales_employee_application/tracking/tracking_config.dart';
import 'package:sales_employee_application/tracking/work_shift.dart';
import 'package:sales_employee_application/utils/app_theme.dart';

class _ShiftRepo extends MockSalesRepository {
  bool failStart = false;
  @override
  Future<WorkShift> startShift() async {
    if (failStart) throw Exception('fail');
    final now = DateTime.now().toUtc();
    return WorkShift(
      shiftId: 1,
      status: 'Active',
      startedAtUtc: now,
      cutoffAtUtc: now.add(const Duration(hours: 18)),
      isNew: true,
    );
  }

  @override
  Future<void> endShift() async {}
}

WorkShift _shift({DateTime? cutoff}) {
  final now = DateTime.utc(2026, 9, 2, 5, 15);
  return WorkShift(
    shiftId: 7,
    status: 'Active',
    startedAtUtc: now,
    cutoffAtUtc: cutoff ?? now.add(const Duration(hours: 18)),
    isNew: true,
  );
}

Widget _app(Widget home) => MaterialApp(
      theme: AppTheme.themeData,
      routes: {'/home': (_) => const HomeScreen()},
      home: home,
    );

void main() {
  final live = <ShiftTrackingController>[];
  tearDown(() async {
    for (final c in live) {
      await c.dispose();
    }
    live.clear();
    SalesRepositoryFactory.reset();
    TrackingRuntime.instance = null;
  });

  testWidgets('Start shift API success -> Home', (tester) async {
    final repo = _ShiftRepo();
    SalesRepositoryFactory.setInstance(repo);
    final controller = ShiftTrackingController(
      repository: repo,
      store: MemoryLocationStore(),
      requestPermission: () async => true,
      startNative: (_) async => true,
      stopNative: () async {},
      connectivity: Stream<List<ConnectivityResult>>.empty(),
      scheduleTimers: false,
    );
    live.add(controller);
    await tester.pumpWidget(_app(ShiftScreen(controller: controller)));
    await tester.tap(find.text('بدء الدوام'));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 50));
    expect(find.byType(HomeScreen), findsOneWidget);
    await controller.dispose();
    live.remove(controller);
  });

  testWidgets('failure -> stays locked', (tester) async {
    final repo = _ShiftRepo()..failStart = true;
    SalesRepositoryFactory.setInstance(repo);
    final controller = ShiftTrackingController(
      repository: repo,
      store: MemoryLocationStore(),
      requestPermission: () async => true,
      startNative: (_) async => true,
      stopNative: () async {},
      connectivity: Stream<List<ConnectivityResult>>.empty(),
      scheduleTimers: false,
    );
    live.add(controller);
    await tester.pumpWidget(_app(ShiftScreen(controller: controller)));
    await tester.tap(find.text('بدء الدوام'));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 50));
    expect(find.text('يجب بدء الدوام لاستخدام تطبيق المبيعات'), findsOneWidget);
    expect(find.text('بدء الدوام'), findsOneWidget);
  });

  testWidgets('permission denied -> no tracking start', (tester) async {
    var nativeStarted = false;
    final repo = _ShiftRepo();
    final controller = ShiftTrackingController(
      repository: repo,
      store: MemoryLocationStore(),
      requestPermission: () async => false,
      startNative: (_) async {
        nativeStarted = true;
        return true;
      },
      stopNative: () async {},
      connectivity: Stream<List<ConnectivityResult>>.empty(),
      scheduleTimers: false,
    );
    live.add(controller);
    await tester.pumpWidget(_app(ShiftScreen(controller: controller)));
    await tester.tap(find.text('بدء الدوام'));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 50));
    expect(nativeStarted, isFalse);
    expect(find.textContaining('يلزم السماح بالموقع'), findsOneWidget);
  });

  test('offline point stored', () async {
    final store = MemoryLocationStore();
    await store.insert(LocalLocationPoint(
      shiftId: 1,
      latitude: 32,
      longitude: 44,
      capturedAtUtc: DateTime.now().toUtc(),
      deviceSequence: await store.nextSequence(1),
    ));
    expect(await store.pendingCount(), 1);
  });

  test('sync success marks point synced', () async {
    final store = MemoryLocationStore();
    final repo = _ShiftRepo();
    await repo.startShift();
    final seq = await store.nextSequence(1);
    await store.insert(LocalLocationPoint(
      shiftId: 1,
      latitude: 32,
      longitude: 44,
      capturedAtUtc: DateTime.now().toUtc(),
      deviceSequence: seq,
    ));
    final ok = await LocationSyncEngine(store, repo).sync(1);
    expect(ok, isTrue);
    expect(store.points.first.syncStatus, 'Synced');
  });

  test('sync failure preserves point', () async {
    final store = MemoryLocationStore();
    final repo = _FailSyncRepo();
    await store.insert(LocalLocationPoint(
      shiftId: 1,
      latitude: 32,
      longitude: 44,
      capturedAtUtc: DateTime.now().toUtc(),
      deviceSequence: 1,
    ));
    final ok = await LocationSyncEngine(store, repo).sync(1);
    expect(ok, isFalse);
    expect(store.points.first.syncStatus, isNot('Synced'));
    expect(await store.pendingCount(), 1);
  });

  test('cutoff stops collection but preserves unsynced', () async {
    final store = MemoryLocationStore();
    await store.insert(LocalLocationPoint(
      shiftId: 1,
      latitude: 32,
      longitude: 44,
      capturedAtUtc: DateTime.now().toUtc(),
      deviceSequence: 1,
    ));
    final repo = _ShiftRepo();
    var stopped = false;
    final controller = ShiftTrackingController(
      repository: repo,
      store: store,
      requestPermission: () async => true,
      startNative: (_) async => true,
      stopNative: () async { stopped = true; },
      connectivity: Stream<List<ConnectivityResult>>.empty(),
      scheduleTimers: false,
    );
    live.add(controller);
    await controller.attach(_shift(cutoff: DateTime.now().toUtc().subtract(const Duration(seconds: 1))));
    await Future<void>.delayed(const Duration(milliseconds: 20));
    expect(controller.isCollecting, isFalse);
    expect(stopped, isTrue);
    expect(await store.pendingCount(), 1);
    await controller.dispose();
    live.remove(controller);
  });

  test('end shift stops GPS and clears local shift', () async {
    var stopped = 0;
    final controller = ShiftTrackingController(
      repository: _ShiftRepo(),
      store: MemoryLocationStore(),
      requestPermission: () async => true,
      startNative: (_) async => true,
      stopNative: () async { stopped++; },
      connectivity: Stream<List<ConnectivityResult>>.empty(),
      scheduleTimers: false,
    );
    live.add(controller);
    await controller.attach(_shift(cutoff: DateTime.now().toUtc().add(const Duration(hours: 8))));
    expect(controller.isCollecting, isTrue);
    await controller.endShiftFlow();
    expect(controller.isCollecting, isFalse);
    expect(controller.activeShift, isNull);
    expect(stopped, greaterThanOrEqualTo(1));
    await controller.dispose();
    live.remove(controller);
  });

  testWidgets('no GPS Map/Route UI', (tester) async {
    SalesRepositoryFactory.setInstance(MockSalesRepository());
    await tester.pumpWidget(_app(const HomeScreen()));
    expect(find.textContaining('Map'), findsNothing);
    expect(find.textContaining('المسار'), findsNothing);
    expect(find.textContaining('الإحداثيات'), findsNothing);
  });

  test('official tracking interval is 10 minutes', () {
    expect(TrackingConfig.officialInterval, const Duration(minutes: 10));
    expect(TrackingConfig.officialIntervalMs, 600000);
    expect(TrackingConfig.maxAcceptedAccuracyMeters, greaterThanOrEqualTo(50));
    expect(TrackingConfig.minimumDistanceMeters, 0);
    expect(TrackingConfig.debugIntervalMs, 0);
  });

  test('start shift due first route point at shift start not clock floor', () {
    final start = DateTime.utc(2026, 9, 2, 17, 32); // Iraq 20:32
    final due = OfficialSlot.dueSlots(
      shiftStartUtc: start,
      lastOfficialSlotUtc: null,
      nowUtc: start,
      cutoffUtc: start.add(const Duration(hours: 18)),
    );
    expect(due, [start]);
    expect(due.first, isNot(OfficialSlot.floorUtc(start)));
  });

  test('twenty-five minute shift yields three route points from start', () {
    final start = DateTime.utc(2026, 9, 2, 17, 32);
    final due = OfficialSlot.dueSlots(
      shiftStartUtc: start,
      lastOfficialSlotUtc: null,
      nowUtc: start.add(const Duration(minutes: 25)),
      cutoffUtc: start.add(const Duration(hours: 18)),
    );
    expect(due, [
      start,
      start.add(const Duration(minutes: 10)),
      start.add(const Duration(minutes: 20)),
    ]);
    expect(due, isNot(contains(DateTime.utc(2026, 9, 2, 17, 40))));
    expect(due, isNot(contains(DateTime.utc(2026, 9, 2, 17, 50))));
  });

  test('ten minutes later a new route point is due without movement', () {
    final start = DateTime.utc(2026, 9, 2, 8, 0);
    final due = OfficialSlot.dueSlots(
      shiftStartUtc: start,
      lastOfficialSlotUtc: start,
      nowUtc: start.add(const Duration(minutes: 10)),
      cutoffUtc: start.add(const Duration(hours: 18)),
    );
    expect(due, [DateTime.utc(2026, 9, 2, 8, 10)]);
  });

  test('Iraq display UTC 18:40 is Baghdad 21:40', () {
    final utc = DateTime.utc(2026, 9, 2, 18, 40);
    final iraq = utc.add(OfficialSlot.iraqOffset);
    expect(iraq.hour, 21);
    expect(iraq.minute, 40);
  });

  test('network lookup failure does not stop native tracking', () {
    expect(
      TrackingShiftPolicy.shouldStopNative(
        gpsStoppedByUser: false,
        remoteLookupFailed: true,
        remoteShift: null,
      ),
      isFalse,
    );
    expect(
      TrackingShiftPolicy.shouldStopNative(
        gpsStoppedByUser: true,
        remoteLookupFailed: true,
        remoteShift: null,
      ),
      isTrue,
    );
  });

  test('end shift flushes then stops then ends the shift', () async {
    final calls = <String>[];
    final repo = _OrderedEndRepo(calls);
    final controller = ShiftTrackingController(
      repository: repo,
      store: MemoryLocationStore(),
      requestPermission: () async => true,
      startNative: (_) async => true,
      stopNative: () async { calls.add('stop'); },
      flushThenStopNative: () async { calls.add('flush'); },
      connectivity: Stream<List<ConnectivityResult>>.empty(),
      scheduleTimers: false,
    );
    live.add(controller);
    await controller.attach(_shift(cutoff: DateTime.now().toUtc().add(const Duration(hours: 8))));
    await controller.endShiftFlow();
    expect(calls, ['flush', 'end']);
    expect(controller.isCollecting, isFalse);
    await controller.dispose();
    live.remove(controller);
  });

  test('retry after failed upload does not duplicate the local point', () async {
    final store = MemoryLocationStore();
    final repo = _FlakySyncRepo();
    final seq = OfficialSlot.sequence(OfficialSlot.floorUtc(DateTime.utc(2026, 9, 2, 8, 0)));
    await store.insert(LocalLocationPoint(
      shiftId: 1,
      latitude: 32,
      longitude: 44,
      capturedAtUtc: DateTime.utc(2026, 9, 2, 8, 0),
      deviceSequence: seq,
    ));
    expect(await LocationSyncEngine(store, repo).sync(1), isFalse);
    expect(await store.pendingCount(), 1);
    expect(await LocationSyncEngine(store, repo).sync(1), isTrue);
    expect(store.points, hasLength(1));
    expect(store.points.single.syncStatus, 'Synced');
    expect(repo.uploads, 2);
  });

  test('rejected batch stays pending for retry', () async {
    final store = MemoryLocationStore();
    final repo = _RejectSyncRepo();
    await store.insert(LocalLocationPoint(
      shiftId: 1,
      latitude: 32,
      longitude: 44,
      capturedAtUtc: DateTime.now().toUtc(),
      deviceSequence: 1,
    ));
    expect(await LocationSyncEngine(store, repo).sync(1), isFalse);
    expect(store.points.single.syncStatus, isNot('Synced'));
    expect(await store.pendingCount(), 1);
  });

  test('official slots are 10 minutes and catch-up fills gaps from first capture', () {
    final start = DateTime.utc(2026, 9, 2, 22, 0);
    final due = OfficialSlot.dueSlots(
      shiftStartUtc: start,
      lastOfficialSlotUtc: start,
      nowUtc: DateTime.utc(2026, 9, 2, 22, 34),
      cutoffUtc: DateTime.utc(2026, 9, 3, 0, 0),
    );
    expect(due, [
      DateTime.utc(2026, 9, 2, 22, 10),
      DateTime.utc(2026, 9, 2, 22, 20),
      DateTime.utc(2026, 9, 2, 22, 30),
    ]);
    expect(OfficialSlot.sequence(DateTime.utc(2026, 9, 2, 22, 10)), OfficialSlot.sequence(DateTime.utc(2026, 9, 2, 22, 10)));
    expect(
      OfficialSlot.sequence(DateTime.utc(2026, 9, 2, 22, 10)),
      isNot(OfficialSlot.sequence(DateTime.utc(2026, 9, 2, 22, 20))),
    );
  });

  test('offline sync does not duplicate the same device sequence', () async {
    final store = MemoryLocationStore();
    final repo = _ShiftRepo();
    await repo.startShift();
    final captured = DateTime.utc(2026, 9, 2, 17, 32);
    final seq = OfficialSlot.sequence(captured);
    await store.insert(LocalLocationPoint(
      shiftId: 1,
      latitude: 32,
      longitude: 44,
      capturedAtUtc: captured,
      deviceSequence: seq,
    ));
    await store.insert(LocalLocationPoint(
      shiftId: 1,
      latitude: 32.1,
      longitude: 44.1,
      capturedAtUtc: captured,
      deviceSequence: seq,
    ));
    expect(store.points, hasLength(1));
    final ok = await LocationSyncEngine(store, repo).sync(1);
    expect(ok, isTrue);
    expect(store.points.where((p) => p.deviceSequence == seq), hasLength(1));
  });

  test('live interval is 20s and official route interval is 10 minutes', () {
    expect(TrackingConfig.movingInterval, const Duration(seconds: 20));
    expect(TrackingConfig.officialInterval, const Duration(minutes: 10));
    expect(TrackingConfig.officialIntervalMs, 600000);
    expect(TrackingConfig.minimumDistanceMeters, 0);
  });
}

class _FailSyncRepo extends MockSalesRepository {
  @override
  Future<LocationBatchResult> uploadLocationBatch(int shiftId, List<LocalLocationPoint> points) async {
    throw Exception('offline');
  }
}

class _RejectSyncRepo extends MockSalesRepository {
  @override
  Future<LocationBatchResult> uploadLocationBatch(int shiftId, List<LocalLocationPoint> points) async {
    return LocationBatchResult(rejected: points.length);
  }
}

class _FlakySyncRepo extends MockSalesRepository {
  int uploads = 0;

  @override
  Future<LocationBatchResult> uploadLocationBatch(int shiftId, List<LocalLocationPoint> points) async {
    uploads++;
    if (uploads == 1) throw Exception('offline');
    return LocationBatchResult(accepted: points.length);
  }
}

class _OrderedEndRepo extends MockSalesRepository {
  _OrderedEndRepo(this.calls);
  final List<String> calls;

  @override
  Future<void> endShift() async {
    calls.add('end');
  }
}
