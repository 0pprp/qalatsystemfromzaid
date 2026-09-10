import 'dart:convert';

import 'package:follower_application/config/app_env.dart';
import 'package:follower_application/services/follower_tracking_api.dart';
import 'package:shared_preferences/shared_preferences.dart';

class FollowerTrackingSession {
  static const _shiftJsonKey = 'follower_shift_json';
  static const _shiftDateKey = 'follower_shift_date';
  static const _gpsStoppedKey = 'follower_gps_stopped';

  static Future<String?> asyncId() async {
    final prefs = await SharedPreferences.getInstance();
    return prefs.getString('AsyncId');
  }

  static Future<String> apiBase() async {
    final prefs = await SharedPreferences.getInstance();
    return FollowerTrackingApi.normalizeBase(
      AppEnv.apiBase(fallback: prefs.getString('LinkDelegate') ?? ''),
    );
  }

  static Future<Map<String, dynamic>?> get shift async {
    final prefs = await SharedPreferences.getInstance();
    final raw = prefs.getString(_shiftJsonKey);
    if (raw == null || raw.isEmpty) return null;
    return jsonDecode(raw) as Map<String, dynamic>;
  }

  static Future<String?> get shiftDateKey async {
    final prefs = await SharedPreferences.getInstance();
    return prefs.getString(_shiftDateKey);
  }

  static Future<bool> get gpsStoppedByUser async {
    final prefs = await SharedPreferences.getInstance();
    return prefs.getString(_gpsStoppedKey) == '1';
  }

  static Future<void> saveShift(
      Map<String, dynamic> data, String dateKey) async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(_shiftDateKey, dateKey);
    await prefs.setString(_shiftJsonKey, jsonEncode(data));
  }

  static Future<void> clearShift() async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.remove(_shiftDateKey);
    await prefs.remove(_shiftJsonKey);
  }

  static Future<void> setGpsStoppedByUser(bool value) async {
    final prefs = await SharedPreferences.getInstance();
    if (value) {
      await prefs.setString(_gpsStoppedKey, '1');
    } else {
      await prefs.remove(_gpsStoppedKey);
    }
  }
}
