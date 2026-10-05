import 'package:main_follower_application/core/network/api_client.dart';
import 'package:main_follower_application/domain/work_shift.dart';

class ShiftRepository {
  static Future<WorkShift?> currentOrNull() async {
    try {
      final res = await ApiClient.getJsonNullable('main-follower/shift/current');
      if (res == null) return null;
      final shift = WorkShift.fromJson(res);
      return shift.id > 0 && shift.isActive ? shift : null;
    } catch (_) {
      return null;
    }
  }

  static Future<WorkShift> start({
    double? latitude,
    double? longitude,
  }) async {
    final json = await ApiClient.postJson('main-follower/shift/start', {
      if (latitude != null) 'latitude': latitude,
      if (longitude != null) 'longitude': longitude,
    });
    return WorkShift.fromJson(json);
  }

  static Future<WorkShift> end({
    double? latitude,
    double? longitude,
  }) async {
    final json = await ApiClient.postJson('main-follower/shift/end', {
      if (latitude != null) 'latitude': latitude,
      if (longitude != null) 'longitude': longitude,
    });
    return WorkShift.fromJson(json);
  }

  static Future<void> pingLocation({
    required double latitude,
    required double longitude,
    double? accuracyMeters,
  }) async {
    await ApiClient.postJson('main-follower/shift/location', {
      'latitude': latitude,
      'longitude': longitude,
      if (accuracyMeters != null) 'accuracyMeters': accuracyMeters,
      'recordedAtUtc': DateTime.now().toUtc().toIso8601String(),
    });
  }
}
