import 'dart:async';

import 'package:connectivity_plus/connectivity_plus.dart';
import 'package:permission_handler/permission_handler.dart';
import 'package:sales_employee_application/data/sales_repository.dart';
import 'package:sales_employee_application/data/sales_repository_factory.dart';
import 'package:sales_employee_application/services/api_client.dart';
import 'package:sales_employee_application/services/session.dart';
import 'package:flutter/foundation.dart';
import 'package:sales_employee_application/tracking/location_store.dart';
import 'package:sales_employee_application/tracking/sqlite_location_store.dart';
import 'package:sales_employee_application/tracking/location_sync_engine.dart';
import 'package:sales_employee_application/tracking/shift_start_debug.dart';
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
            ((shift) async => TrackingChannel.start(
                  shiftId: shift.shiftId,
                  cutoffAtUtc: shift.cutoffAtUtc,
                  startedAtUtc: shift.startedAtUtc,
                )),
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
  Object? lastNativeError;

  static Future<bool> _defaultPermission() async {
    var status = await Permission.locationWhenInUse.request();
    if (!status.isGranted) return false;
    await Permission.notification.request();
    await Permission.locationAlways.request();
    return true;
  }

  Future<WorkShift?> startShiftFlow() async {
    lastError = null;
    lastNativeError = null;
    try {
      await Session.setGpsStoppedByUser(false);
    } catch (_) {}
    final allowed = await _requestPermission();
    if (!allowed) {
      await _repo.recordTrackingEvent(null, 'LOCATION_PERMISSION_DENIED');
      lastError = 'يلزم السماح بالموقع لتشغيل الدوام.';
      return null;
    }
    ShiftStartDebug.log('Permission granted');
    await _repo.recordTrackingEvent(null, 'LOCATION_PERMISSION_GRANTED');
    ShiftStartDebug.log('POST shifts/start started');
    late final WorkShift shift;
    try {
      shift = await _repo.startShift();
      ShiftStartDebug.log('POST shifts/start response');
    } catch (e, st) {
      if (e is ApiException) {
        ShiftStartDebug.log(
          'POST shifts/start response ${e.statusCode} ${e.message}',
        );
      } else {
        ShiftStartDebug.log('POST shifts/start response ${e.runtimeType}');
      }
      ShiftStartDebug.logError('POST shifts/start', e, st);
      lastError = ShiftStartDebug.apiFailure(e);
      rethrow;
    }
    ShiftStartDebug.log('shiftId received ${shift.shiftId}');
    ShiftStartDebug.log(
      'parsed status=${shift.status} hasActiveShift=${shift.hasActiveShift} '
      'startedAtUtc=${shift.startedAtUtc.toIso8601String()} '
      'cutoffAtUtc=${shift.cutoffAtUtc.toIso8601String()} '
      'isPastCutoff=${shift.isPastCutoff()} isActive=${shift.isActive}',
    );
    if (!shift.isActive) {
      lastError = ShiftStartDebug.showDetail
          ? '${ShiftStartDebug.generic}: shift not active status=${shift.status} hasActive=${shift.hasActiveShift}'
          : ShiftStartDebug.generic;
      ShiftStartDebug.log('abort before native: shift not active');
      return null;
    }
    try {
      ShiftStartDebug.log('saving WorkShift locally');
      await Session.saveShift(shift.toJson(), shift.cutoffAtUtc.toIso8601String());
      ShiftStartDebug.log('local shift saved');
    } catch (e, st) {
      ShiftStartDebug.logError('Session.saveShift', e, st);
    }
    bool nativeOk;
    try {
      ShiftStartDebug.log('invoking native tracking');
      nativeOk = await attach(shift);
    } catch (e, st) {
      ShiftStartDebug.logError('attach/native tracking', e, st);
      lastError = ShiftStartDebug.trackingFailure(e);
      rethrow;
    }
    if (!nativeOk && ShiftStartDebug.isAndroidDevice) {
      lastError = ShiftStartDebug.trackingFailure(lastNativeError);
      return null;
    }
    return shift;
  }

  Future<bool> attach(WorkShift shift) async {
    activeShift = shift;
    _collecting = !shift.isPastCutoff();
    ShiftStartDebug.log('attach collecting=$_collecting');
    try {
      ShiftStartDebug.log('init location store');
      await _store.init();
      ShiftStartDebug.log('location store ready');
    } catch (e, st) {
      ShiftStartDebug.logError('location store init', e, st);
      rethrow;
    }
    var nativeOk = true;
    if (_collecting) {
      ShiftStartDebug.log('startForegroundService called');
      try {
        nativeOk = await _startNative(shift);
        ShiftStartDebug.log('native service ${nativeOk ? 'success' : 'failure'}');
        if (nativeOk) {
          ShiftStartDebug.log('native tracking started');
          await _repo.recordTrackingEvent(shift.shiftId, 'GPS_STARTED');
        } else {
          lastNativeError ??= Exception('startForegroundService returned false');
        }
      } catch (e, st) {
        nativeOk = false;
        lastNativeError = e;
        ShiftStartDebug.logError('TrackingChannel.start / startForegroundService', e, st);
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
      _netSub = _connectivity.listen((results) async {
        final online = results.any((r) => r != ConnectivityResult.none);
        if (!online && _internet) {
          _internet = false;
          await _store.insertEvent(shift.shiftId, 'INTERNET_LOST');
        } else if (online && !_internet) {
          _internet = true;
          await _store.insertEvent(shift.shiftId, 'INTERNET_RESTORED');
          _trySync();
        }
      });
    }
    await _trySync();
    return nativeOk;
  }

  Future<void> restoreIfNeeded() async {
    if (Session.gpsStoppedByUser) {
      _collecting = false;
      activeShift = null;
      await _stopNative();
      try {
        await Session.clearShift();
      } catch (_) {}
      return;
    }
    final current = await _repo.currentShift();
    if (current == null || !current.isActive) {
      _collecting = false;
      activeShift = null;
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

  Future<void> endShiftFlow() async {
    await _repo.endShift();
    _collecting = false;
    _syncTimer?.cancel();
    _cutoffTimer?.cancel();
    await _netSub?.cancel();
    _netSub = null;
    try {
      await _stopNative();
    } catch (_) {}
    activeShift = null;
    try {
      await Session.setGpsStoppedByUser(true);
    } catch (_) {}
    try {
      await Session.clearShift();
    } catch (_) {}
  }

  Future<void> dispose() async {
    _syncTimer?.cancel();
    _cutoffTimer?.cancel();
    await _netSub?.cancel();
  }
}
