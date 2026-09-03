import 'package:flutter/services.dart';
import 'package:sales_employee_application/config/app_env.dart';
import 'package:sales_employee_application/services/session.dart';
import 'package:sales_employee_application/tracking/shift_start_debug.dart';
import 'package:sales_employee_application/tracking/tracking_config.dart';

class TrackingChannel {
  static const _channel = MethodChannel('saleshaider/location');

  static Future<bool> start({required int shiftId, required DateTime cutoffAtUtc}) async {
    try {
      ShiftStartDebug.log(
        'MethodChannel.invokeMethod start shiftId=$shiftId cutoffAtUtcMs=${cutoffAtUtc.toUtc().millisecondsSinceEpoch}',
      );
      await _channel.invokeMethod('start', {
        'shiftId': shiftId,
        'cutoffAtUtcMs': cutoffAtUtc.toUtc().millisecondsSinceEpoch,
        'intervalMs': TrackingConfig.movingInterval.inMilliseconds,
        'minDistance': TrackingConfig.minimumDistanceMeters,
        'stationaryIntervalMs': TrackingConfig.stationaryInterval.inMilliseconds,
        'apiBase': Session.apiBase ?? AppEnv.apiBase(),
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

  static Future<bool> isRunning() async {
    try {
      return await _channel.invokeMethod<bool>('isRunning') ?? false;
    } on MissingPluginException {
      return false;
    }
  }
}
