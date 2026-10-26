import 'dart:convert';

import 'package:delegate_application/config/app_env.dart';
import 'package:delegate_application/config/delegate_app_links.dart';
import 'package:delegate_application/core/update/app_version.dart';
import 'package:http/http.dart' as http;

class TutorialQuestion {
  const TutorialQuestion({
    required this.id,
    required this.questionAr,
    required this.videoUrl,
  });

  final int id;
  final String questionAr;
  final String videoUrl;
}

/// Resolves educational tutorial Q&A videos from the Sales Gateway.
class TutorialVideoService {
  static Future<List<TutorialQuestion>> fetchQuestions({String? appKey}) async {
    final key = (appKey ?? AppVersion.appKey).trim();
    if (key.isEmpty) return const [];
    try {
      final base = AppEnv.gatewayApiBase();
      final uri = Uri.parse('${base}mobile-tutorial-videos/$key');
      final response = await http.get(uri).timeout(const Duration(seconds: 12));
      if (response.statusCode != 200) return const [];
      final decoded = jsonDecode(response.body);
      if (decoded is! List) return const [];
      final out = <TutorialQuestion>[];
      for (final raw in decoded) {
        if (raw is! Map) continue;
        final q = (raw['questionAr'] ?? raw['QuestionAr'] ?? '').toString().trim();
        final url = (raw['videoUrl'] ?? raw['VideoUrl'] ?? '').toString().trim();
        final id = int.tryParse('${raw['id'] ?? raw['Id'] ?? 0}') ?? 0;
        if (q.isEmpty || url.isEmpty) continue;
        final parsed = Uri.tryParse(url);
        if (parsed == null ||
            !(parsed.isScheme('http') || parsed.isScheme('https'))) {
          continue;
        }
        out.add(TutorialQuestion(id: id, questionAr: q, videoUrl: url));
      }
      return out;
    } catch (_) {
      return const [];
    }
  }

  /// Legacy single-URL API (accountant / old one-link-per-app).
  static Future<String?> fetchUrl({String? appKey}) async {
    final key = (appKey ?? AppVersion.appKey).trim();
    if (key.isEmpty) return null;
    try {
      final base = AppEnv.gatewayApiBase();
      final uri = Uri.parse('${base}mobile-tutorial-links/$key');
      final response = await http.get(uri).timeout(const Duration(seconds: 12));
      if (response.statusCode != 200) return null;
      final decoded = jsonDecode(response.body);
      if (decoded is! Map) return null;
      final url =
          (decoded['videoUrl'] ?? decoded['VideoUrl'] ?? '').toString().trim();
      if (url.isEmpty) return null;
      final parsed = Uri.tryParse(url);
      if (parsed == null ||
          !(parsed.isScheme('http') || parsed.isScheme('https'))) {
        return null;
      }
      return url;
    } catch (_) {
      return null;
    }
  }

  static Future<Uri?> resolveLaunchUri() async {
    final live = await fetchUrl();
    final raw = (live ?? DelegateAppLinks.tutorialVideoUrl).trim();
    if (raw.isEmpty) return null;
    return Uri.tryParse(raw);
  }
}
