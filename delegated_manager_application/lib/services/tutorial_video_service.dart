import 'dart:convert';

import 'package:delegated_manager_application/core/config/app_env.dart';
import 'package:delegated_manager_application/core/update/app_version.dart';
import 'package:http/http.dart' as http;

/// Resolves the educational video URL from the Sales Gateway (manager-editable).
class TutorialVideoService {
  static Future<String?> fetchUrl({String? appKey}) async {
    final key = (appKey ?? AppVersion.appKey).trim();
    if (key.isEmpty) return null;
    try {
      final base = AppEnv.apiBase();
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
    final raw = (await fetchUrl() ?? '').trim();
    if (raw.isEmpty) return null;
    return Uri.tryParse(raw);
  }
}
