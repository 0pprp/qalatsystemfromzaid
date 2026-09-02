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
    expect(find.text('حالة الدوام: بدأ الدوام'), findsOneWidget);
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

  testWidgets('no GPS Map/Route UI', (tester) async {
    await tester.pumpWidget(_app(const HomeScreen()));
    expect(find.textContaining('Map'), findsNothing);
    expect(find.textContaining('المسار'), findsNothing);
    expect(find.textContaining('الإحداثيات'), findsNothing);
  });
}

class _FailSyncRepo extends MockSalesRepository {
  @override
  Future<LocationBatchResult> uploadLocationBatch(int shiftId, List<LocalLocationPoint> points) async {
    throw Exception('offline');
  }
}
