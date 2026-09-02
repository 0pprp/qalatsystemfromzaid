import 'dart:async';
import 'dart:math';

import 'package:sales_employee_application/services/api_client.dart';
import 'package:sales_employee_application/services/geo_fix.dart';
import 'package:sales_employee_application/services/gps_queue.dart';
import 'package:sales_employee_application/services/location.dart';
import 'package:sales_employee_application/services/session.dart';
import 'package:sales_employee_application/utils/iraq_time.dart';

class Tracker {
  Tracker._();
  static final Tracker instance = Tracker._();

  Timer? _pollTimer;
  Timer? _syncTimer;
  Timer? _midnightTimer;
  GeoFix? _lastPosition;
  bool running = false;
  String status = 'متوقف';

  Future<void> start() async {
    if (running) return;
    if (!IraqTime.isSameBusinessDay(Session.shiftDateKey)) {
      status = 'يلزم بدء الدوام';
      return;
    }

    running = true;
    status = 'يتتبع المسار';
    _pollTimer = Timer.periodic(const Duration(seconds: 40), (_) => _poll());
    _syncTimer = Timer.periodic(const Duration(seconds: 45), (_) => sync());
    _midnightTimer =
        Timer.periodic(const Duration(seconds: 30), (_) => _checkReset());
    await _poll();
    await sync();
  }

  Future<void> stop() async {
    running = false;
    status = 'متوقف';
    _pollTimer?.cancel();
    _syncTimer?.cancel();
    _midnightTimer?.cancel();
  }

  Future<void> _checkReset() async {
    if (!IraqTime.isSameBusinessDay(Session.shiftDateKey)) {
      await Session.clearShift();
      await stop();
      status = 'انتهى الدوام — اضغط بدء الدوام';
    }
  }

  Future<void> _poll() async {
    final fix = await readLocation();
    if (fix == null) {
      status = 'الدوام شغال — اسمح بالموقع لتسجيل المسار';
      return;
    }
    final moved = _lastPosition == null ||
        _distance(_lastPosition!, fix) >= 12;
    if (!moved) return;
    _lastPosition = fix;
    final now = DateTime.now().toUtc();
    await GpsQueue.instance.enqueue(
      clientKey: '${Session.userId}_${now.millisecondsSinceEpoch}',
      recordedAt: now,
      latitude: fix.latitude,
      longitude: fix.longitude,
      accuracy: fix.accuracy,
    );
    await sync();
  }

  double _distance(GeoFix a, GeoFix b) {
    const earth = 6371000.0;
    final dLat = _rad(b.latitude - a.latitude);
    final dLng = _rad(b.longitude - a.longitude);
    final h = sin(dLat / 2) * sin(dLat / 2) +
        cos(_rad(a.latitude)) *
            cos(_rad(b.latitude)) *
            sin(dLng / 2) *
            sin(dLng / 2);
    return 2 * earth * atan2(sqrt(h), sqrt(1 - h));
  }

  double _rad(double deg) => deg * pi / 180;

  Future<void> sync() async {
    final pending = await GpsQueue.instance.pending();
    if (pending.isEmpty) return;
    try {
      final shift = Session.shift;
      final res = await ApiClient.post('Track/Sync', body: {
        'shiftID': shift?['shiftID'] ?? shift?['ShiftID'],
        'points': pending
            .map((row) => {
                  'clientKey': row['client_key'],
                  'recordedAt': row['recorded_at'],
                  'latitude': row['latitude'],
                  'longitude': row['longitude'],
                  'accuracy': row['accuracy'],
                })
            .toList(),
      });
      final deferred = res is Map && (res['deferred'] == true);
      if (!deferred) {
        await GpsQueue.instance.markSynced(
          pending.map((row) => row['client_key'].toString()).toList(),
        );
      }
      final left = await GpsQueue.instance.pendingCount();
      status = left == 0 ? 'يتتبع المسار' : 'تبقى $left نقطة للمزامنة';
    } catch (_) {
      status = 'تعذر المزامنة — النقاط محفوظة محلياً';
    }
  }
}
