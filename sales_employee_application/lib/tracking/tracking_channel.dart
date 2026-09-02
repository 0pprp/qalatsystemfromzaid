import 'package:flutter/services.dart';
import 'package:sales_employee_application/tracking/tracking_config.dart';

class TrackingChannel {
  static const _channel = MethodChannel('saleshaider/location');

  static Future<bool> start({required int shiftId, required DateTime cutoffAtUtc}) async {
    try {
      await _channel.invokeMethod('start', {
        'shiftId': shiftId,
        'cutoffAtUtcMs': cutoffAtUtc.millisecondsSinceEpoch,
        'intervalMs': TrackingConfig.movingInterval.inMilliseconds,
        'minDistance': TrackingConfig.minimumDistanceMeters,
        'stationaryIntervalMs': TrackingConfig.stationaryInterval.inMilliseconds,
      });
      return true;
    } on MissingPluginException {
      return false;
    } on PlatformException {
      return false;
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
