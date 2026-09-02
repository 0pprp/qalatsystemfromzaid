import 'dart:async';

import 'package:connectivity_plus/connectivity_plus.dart';
import 'package:permission_handler/permission_handler.dart';
import 'package:sales_employee_application/data/sales_repository.dart';
import 'package:sales_employee_application/data/sales_repository_factory.dart';
import 'package:sales_employee_application/services/session.dart';
import 'package:flutter/foundation.dart';
import 'package:sales_employee_application/tracking/location_store.dart';
import 'package:sales_employee_application/tracking/sqlite_location_store.dart';
import 'package:sales_employee_application/tracking/location_sync_engine.dart';
import 'package:sales_employee_application/tracking/tracking_channel.dart';
import 'package:sales_employee_application/tracking/tracking_config.dart';
import 'package:sales_employee_application/tracking/work_shift.dart';

typedef PermissionFn = Future<bool> Function();

LocationPointStore defaultLocationStore() {
  if (kIsWeb) return MemoryLocationStore();
  if (defaultTargetPlatform == TargetPlatform.android) {
    return SqliteLocationStore();
  }
  return MemoryLocationStore();
}

class TrackingRuntime {
  static ShiftTrackingController? instance;
}

class ShiftTrackingController {
  ShiftTrackingController({
    SalesRepository? repository,
    LocationPointStore? store,
    PermissionFn? requestPermission,
    Future<bool> Function(WorkShift shift)? startNative,
    Future<void> Function()? stopNative,
    Stream<List<ConnectivityResult>>? connectivity,
    this.scheduleTimers = true,
  })  : _repo = repository ?? SalesRepositoryFactory.instance,
        _store = store ?? defaultLocationStore(),
        _requestPermission = requestPermission ?? _defaultPermission,
        _startNative = startNative ??
            ((shift) async => TrackingChannel.start(shiftId: shift.shiftId, cutoffAtUtc: shift.cutoffAtUtc)),
        _stopNative = stopNative ?? TrackingChannel.stop,
        _connectivity = connectivity ?? Connectivity().onConnectivityChanged {
    _sync = LocationSyncEngine(_store, _repo);
  }

  final SalesRepository _repo;
  final LocationPointStore _store;
  final PermissionFn _requestPermission;
  final Future<bool> Function(WorkShift shift) _startNative;
  final Future<void> Function() _stopNative;
  final Stream<List<ConnectivityResult>> _connectivity;
  final bool scheduleTimers;
  late final LocationSyncEngine _sync;

  StreamSubscription? _netSub;
  Timer? _syncTimer;
  Timer? _cutoffTimer;
  bool _collecting = true;
  bool _internet = true;
  WorkShift? activeShift;
  String? lastError;

  static Future<bool> _defaultPermission() async {
    var status = await Permission.locationWhenInUse.request();
    if (!status.isGranted) return false;
    await Permission.notification.request();
    await Permission.locationAlways.request();
    return true;
  }

  Future<WorkShift?> startShiftFlow() async {
    lastError = null;
    final allowed = await _requestPermission();
    if (!allowed) {
      await _repo.recordTrackingEvent(null, 'LOCATION_PERMISSION_DENIED');
      lastError = 'يلزم السماح بالموقع لتشغيل الدوام.';
      return null;
    }
    await _repo.recordTrackingEvent(null, 'LOCATION_PERMISSION_GRANTED');
    final shift = await _repo.startShift();
    if (!shift.isActive) {
      lastError = 'تعذر بدء الدوام';
      return null;
    }
    try {
      await Session.saveShift(shift.toJson(), shift.cutoffAtUtc.toIso8601String());
    } catch (_) {}
    await attach(shift);
    return shift;
  }

  Future<void> attach(WorkShift shift) async {
    activeShift = shift;
    _collecting = !shift.isPastCutoff();
    await _store.init();
    if (_collecting) {
      final started = await _startNative(shift);
      if (started) {
        await _repo.recordTrackingEvent(shift.shiftId, 'GPS_STARTED');
      }
    }
    _cutoffTimer?.cancel();
    final remain = shift.cutoffAtUtc.difference(DateTime.now().toUtc());
    if (remain.isNegative) {
      await _onCutoff();
    } else if (scheduleTimers) {
      _cutoffTimer = Timer(remain, _onCutoff);
    }
    _syncTimer?.cancel();
    if (scheduleTimers) {
      _syncTimer = Timer.periodic(TrackingConfig.syncInterval, (_) => _trySync());
    }
    _netSub?.cancel();
    if (scheduleTimers) {
      _netSub = _connectivity.listen((results) {
        final online = results.any((r) => r != ConnectivityResult.none);
        if (!online && _internet) {
          _internet = false;
          _repo.recordTrackingEvent(shift.shiftId, 'INTERNET_LOST');
        } else if (online && !_internet) {
          _internet = true;
          _repo.recordTrackingEvent(shift.shiftId, 'INTERNET_RESTORED');
          _trySync();
        }
      });
    }
    await _trySync();
  }

  Future<void> restoreIfNeeded() async {
    final current = await _repo.currentShift();
    if (current == null || !current.isActive) {
      try {
        await Session.clearShift();
      } catch (_) {}
      await _stopNative();
      return;
    }
    try {
      await Session.saveShift(current.toJson(), current.cutoffAtUtc.toIso8601String());
    } catch (_) {}
    await attach(current);
  }

  Future<void> _onCutoff() async {
    _collecting = false;
    await _stopNative();
    if (activeShift != null) {
      await _trySync();
    }
  }

  Future<void> _trySync() async {
    final shift = activeShift;
    if (shift == null) return;
    await _sync.sync(shift.shiftId);
  }

  bool get isCollecting => _collecting;

  Future<void> dispose() async {
    _syncTimer?.cancel();
    _cutoffTimer?.cancel();
    await _netSub?.cancel();
  }
}
