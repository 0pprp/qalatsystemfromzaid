import 'dart:async';

import 'package:geolocator/geolocator.dart';
import 'package:main_follower_application/data/shift_repository.dart';
import 'package:main_follower_application/domain/work_shift.dart';

/// Starts/ends shift and pings GPS every 10 minutes while on duty.
class ShiftTrackingController {
  ShiftTrackingController._();
  static final ShiftTrackingController instance = ShiftTrackingController._();

  static const pingInterval = Duration(minutes: 10);

  WorkShift? active;
  Timer? _timer;
  String? lastError;

  bool get isOnDuty => active != null && active!.isActive;

  Future<bool> ensurePermission() async {
    var permission = await Geolocator.checkPermission();
    if (permission == LocationPermission.denied) {
      permission = await Geolocator.requestPermission();
    }
    if (permission == LocationPermission.denied ||
        permission == LocationPermission.deniedForever) {
      lastError = 'يلزم السماح بالموقع لبدء الدوام.';
      return false;
    }
    final enabled = await Geolocator.isLocationServiceEnabled();
    if (!enabled) {
      lastError = 'فعّل خدمة الموقع من إعدادات الجهاز.';
      return false;
    }
    return true;
  }

  Future<Position?> _currentPosition() async {
    try {
      return await Geolocator.getCurrentPosition(
        locationSettings: const LocationSettings(
          accuracy: LocationAccuracy.high,
          timeLimit: Duration(seconds: 20),
        ),
      );
    } catch (_) {
      return null;
    }
  }

  Future<WorkShift?> refreshCurrent() async {
    active = await ShiftRepository.currentOrNull();
    if (isOnDuty) {
      _ensureTimer();
    } else {
      stopTimer();
    }
    return active;
  }

  Future<WorkShift?> startShift() async {
    lastError = null;
    if (!await ensurePermission()) return null;
    final pos = await _currentPosition();
    try {
      active = await ShiftRepository.start(
        latitude: pos?.latitude,
        longitude: pos?.longitude,
      );
      if (pos != null) {
        // First trail point immediately so DM map sees them.
        try {
          await ShiftRepository.pingLocation(
            latitude: pos.latitude,
            longitude: pos.longitude,
            accuracyMeters: pos.accuracy,
          );
        } catch (_) {}
      }
      _ensureTimer();
      return active;
    } catch (e) {
      lastError = e.toString();
      return null;
    }
  }

  Future<bool> endShift() async {
    lastError = null;
    final pos = await _currentPosition();
    try {
      await ShiftRepository.end(
        latitude: pos?.latitude,
        longitude: pos?.longitude,
      );
      active = null;
      stopTimer();
      return true;
    } catch (e) {
      lastError = e.toString();
      return false;
    }
  }

  void _ensureTimer() {
    _timer?.cancel();
    _timer = Timer.periodic(pingInterval, (_) => pingNow());
  }

  void stopTimer() {
    _timer?.cancel();
    _timer = null;
  }

  Future<void> pingNow() async {
    if (!isOnDuty) return;
    try {
      if (!await ensurePermission()) return;
      final pos = await _currentPosition();
      if (pos == null) return;
      await ShiftRepository.pingLocation(
        latitude: pos.latitude,
        longitude: pos.longitude,
        accuracyMeters: pos.accuracy,
      );
    } catch (_) {
      // Ignore transient GPS/network failures; next tick retries.
    }
  }
}
