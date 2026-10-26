import 'dart:convert';

import 'package:shared_preferences/shared_preferences.dart';

/// Disk cache for Followers/ListCards so home paints instantly on cold start.
class FollowerListCardsCache {
  static const _key = 'follower_list_cards_v2';

  static Future<List<Map<String, dynamic>>?> load() async {
    try {
      final prefs = await SharedPreferences.getInstance();
      final raw = prefs.getString(_key);
      if (raw == null || raw.isEmpty) return null;
      final decoded = json.decode(raw);
      if (decoded is! List) return null;
      return decoded
          .map((e) => Map<String, dynamic>.from(e as Map))
          .toList();
    } catch (_) {
      return null;
    }
  }

  static Future<void> save(List<Map<String, dynamic>> cards) async {
    try {
      final prefs = await SharedPreferences.getInstance();
      await prefs.setString(_key, json.encode(cards));
    } catch (_) {}
  }
}
