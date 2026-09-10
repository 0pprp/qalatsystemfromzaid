import 'package:flutter/services.dart';
import 'package:follower_application/services/follower_tracking_session.dart';
import 'package:follower_application/tracking/follower_shift_debug.dart';
import 'package:follower_application/tracking/tracking_config.dart';

class TrackingChannel {
  static const _channel = MethodChannel('follower/location');

  static Future<bool> start({
    required int shiftId,
    required DateTime cutoffAtUtc,
    DateTime? startedAtUtc,
  }) async {
    try {
      final startedMs =
          (startedAtUtc ?? DateTime.now()).toUtc().millisecondsSinceEpoch;
      FollowerShiftDebug.log(
        'MethodChannel.invokeMethod start shiftId=$shiftId cutoffAtUtcMs=${cutoffAtUtc.toUtc().millisecondsSinceEpoch} startedAtUtcMs=$startedMs',
      );
      final apiBase = await FollowerTrackingSession.apiBase();
      final asyncId = await FollowerTrackingSession.asyncId();
      await _channel.invokeMethod('start', {
        'shiftId': shiftId,
        'cutoffAtUtcMs': cutoffAtUtc.toUtc().millisecondsSinceEpoch,
        'startedAtUtcMs': startedMs,
        'intervalMs': TrackingConfig.movingInterval.inMilliseconds,
        'officialIntervalMs': TrackingConfig.officialIntervalMs,
        'minDistance': TrackingConfig.minimumDistanceMeters,
        'stationaryIntervalMs': TrackingConfig.stationaryInterval.inMilliseconds,
        'apiBase': apiBase,
        'token': asyncId ?? '',
      });
      FollowerShiftDebug.log('MethodChannel.invokeMethod start returned');
      return true;
    } on MissingPluginException catch (e, st) {
      FollowerShiftDebug.logError('MethodChannel MissingPluginException', e, st);
      return false;
    } catch (e, st) {
      FollowerShiftDebug.logError('MethodChannel.invokeMethod', e, st);
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
}
