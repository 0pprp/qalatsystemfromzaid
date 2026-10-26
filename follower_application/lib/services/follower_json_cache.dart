import 'dart:convert';

import 'package:shared_preferences/shared_preferences.dart';

/// Tiny JSON disk cache for follower screens (paint-first, refresh in background).
class FollowerJsonCache {
  static Future<Map<String, dynamic>?> loadMap(String key) async {
    try {
      final prefs = await SharedPreferences.getInstance();
      final raw = prefs.getString(key);
      if (raw == null || raw.isEmpty) return null;
      final decoded = json.decode(raw);
      if (decoded is Map) return Map<String, dynamic>.from(decoded);
    } catch (_) {}
    return null;
  }

  static Future<List<Map<String, dynamic>>?> loadList(String key) async {
    try {
      final prefs = await SharedPreferences.getInstance();
      final raw = prefs.getString(key);
      if (raw == null || raw.isEmpty) return null;
      final decoded = json.decode(raw);
      if (decoded is! List) return null;
      return decoded
          .map((e) => Map<String, dynamic>.from(e as Map))
          .toList();
    } catch (_) {}
    return null;
  }

  static Future<void> save(String key, Object value) async {
    try {
      final prefs = await SharedPreferences.getInstance();
      await prefs.setString(key, json.encode(value));
    } catch (_) {}
  }

  static String listStatsAll() => 'follower_list_stats_v2';

  /// v3: remainder bands are exclusive (0–100k vs 100–200k).
  static String listDetailStats(int listId) =>
      'follower_list_detail_stats_v3_$listId';

  static String listDetailCustomers(int listId) =>
      'follower_list_detail_customers_$listId';

  static String listDetailDaily(int listId, String showType, String ymd) =>
      'follower_list_daily_${listId}_${showType}_$ymd';
}
