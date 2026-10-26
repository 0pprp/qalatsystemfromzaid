import 'dart:convert';

import 'package:follower_application/config/app_env.dart';
import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';

class FollowerDayRatingMissing {
  const FollowerDayRatingMissing({
    required this.listId,
    required this.listName,
  });

  final int listId;
  final String listName;

  factory FollowerDayRatingMissing.fromJson(Map<String, dynamic> m) =>
      FollowerDayRatingMissing(
        listId: int.tryParse('${m['listId'] ?? m['ListId'] ?? 0}') ?? 0,
        listName: '${m['listName'] ?? m['ListName'] ?? 'مندوب'}'.trim(),
      );
}

class FollowerDayRatingService {
  static Future<Map<String, String>> _session() async {
    final prefs = await SharedPreferences.getInstance();
    return {
      'asyncId': prefs.getString('AsyncId') ?? '',
      'link': AppEnv.apiBase(fallback: prefs.getString('LinkDelegate') ?? ''),
    };
  }

  static Future<List<FollowerDayRatingMissing>> fetchMissing() async {
    try {
      final s = await _session();
      if ((s['asyncId'] ?? '').isEmpty || (s['link'] ?? '').isEmpty) {
        return const [];
      }
      final uri = Uri.parse('${s['link']}Followers/DayRatings/Missing')
          .replace(queryParameters: {'asyncId': s['asyncId']});
      final res = await http.get(uri).timeout(const Duration(seconds: 15));
      if (res.statusCode != 200) return const [];
      final decoded = jsonDecode(res.body);
      if (decoded is! Map) return const [];
      final raw = decoded['items'] ?? decoded['Items'] ?? [];
      if (raw is! List) return const [];
      return raw
          .whereType<Map>()
          .map((e) =>
              FollowerDayRatingMissing.fromJson(Map<String, dynamic>.from(e)))
          .where((e) => e.listId > 0)
          .toList();
    } catch (_) {
      return const [];
    }
  }

  static Future<String?> submit({
    required int listId,
    required int score,
    String? note,
  }) async {
    try {
      final s = await _session();
      if ((s['asyncId'] ?? '').isEmpty || (s['link'] ?? '').isEmpty) {
        return 'جلسة غير صالحة';
      }
      final uri = Uri.parse('${s['link']}Followers/DayRatings')
          .replace(queryParameters: {'asyncId': s['asyncId']});
      final res = await http
          .post(
            uri,
            headers: {'Content-Type': 'application/json'},
            body: jsonEncode({
              'listId': listId,
              'score': score,
              if (note != null && note.trim().isNotEmpty) 'note': note.trim(),
            }),
          )
          .timeout(const Duration(seconds: 15));
      if (res.statusCode >= 200 && res.statusCode < 300) return null;
      try {
        final decoded = jsonDecode(res.body);
        if (decoded is Map) {
          final msg = decoded['message'] ?? decoded['Message'];
          if (msg != null && '$msg'.trim().isNotEmpty) return '$msg'.trim();
        }
      } catch (_) {}
      return 'تعذر حفظ التقييم';
    } catch (_) {
      return 'تعذر الاتصال بالسيرفر';
    }
  }
}
