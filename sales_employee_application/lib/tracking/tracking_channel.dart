import 'package:flutter/services.dart';
import 'package:sales_employee_application/services/api_client.dart';
import 'package:sales_employee_application/services/geo_fix.dart';
import 'package:sales_employee_application/services/session.dart';
import 'package:sales_employee_application/tracking/shift_start_debug.dart';
import 'package:sales_employee_application/tracking/tracking_config.dart';

class TrackingChannel {
  static const _channel = MethodChannel('saleshaider/location');

  static Future<bool> start({required int shiftId, required DateTime cutoffAtUtc, DateTime? startedAtUtc}) async {
    try {
      final startedMs = (startedAtUtc ?? DateTime.now()).toUtc().millisecondsSinceEpoch;
      ShiftStartDebug.log(
        'MethodChannel.invokeMethod start shiftId=$shiftId cutoffAtUtcMs=${cutoffAtUtc.toUtc().millisecondsSinceEpoch} startedAtUtcMs=$startedMs',
      );
      await _channel.invokeMethod('start', {
        'shiftId': shiftId,
        'cutoffAtUtcMs': cutoffAtUtc.toUtc().millisecondsSinceEpoch,
        'startedAtUtcMs': startedMs,
        'intervalMs': TrackingConfig.movingInterval.inMilliseconds,
        'officialIntervalMs': TrackingConfig.officialIntervalMs,
        'minDistance': TrackingConfig.minimumDistanceMeters,
        'stationaryIntervalMs': TrackingConfig.stationaryInterval.inMilliseconds,
        'apiBase': ApiClient.resolveBase(),
        'token': Session.token ?? '',
      });
      ShiftStartDebug.log('MethodChannel.invokeMethod start returned');
      return true;
    } on MissingPluginException catch (e, st) {
      ShiftStartDebug.logError('MethodChannel MissingPluginException', e, st);
      return false;
    } catch (e, st) {
      ShiftStartDebug.logError('MethodChannel.invokeMethod', e, st);
      rethrow;
    }
  }

  static Future<void> stop() async {
    try {
      await _channel.invokeMethod('stop');
    } on MissingPluginException {
      return;
    }
  }

  static Future<void> flushThenStop() async {
    try {
      await _channel.invokeMethod('flushAndStop');
    } on MissingPluginException {
      await stop();
    }
  }

  static Future<bool> isRunning() async {
    try {
      return await _channel.invokeMethod<bool>('isRunning') ?? false;
    } on MissingPluginException {
      return false;
    }
  }

  static Future<GeoFix?> currentFix() async {
    try {
      final raw = await _channel.invokeMethod<dynamic>('currentFix');
      if (raw is! Map) return null;
      final map = Map<Object?, Object?>.from(raw);
      final lat = (map['latitude'] as num?)?.toDouble();
      final lng = (map['longitude'] as num?)?.toDouble();
      if (lat == null || lng == null) return null;
      return GeoFix(lat, lng, (map['accuracy'] as num?)?.toDouble());
    } on MissingPluginException {
      return null;
    } catch (_) {
      return null;
    }
  }

  static Future<bool> dial(String number) async {
    try {
      await _channel.invokeMethod<bool>('dial', {'number': number});
      return true;
    } on MissingPluginException {
      return false;
    } catch (_) {
      return false;
    }
  }
}
