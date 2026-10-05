import 'package:delegate_application/config/app_env.dart';
import 'package:delegate_application/config/delegate_app_links.dart';
import 'package:delegate_application/core/update/app_version.dart';
import 'package:http/http.dart' as http;
import 'dart:convert';

/// Resolves the educational video URL from the Sales Gateway (manager-editable).
class TutorialVideoService {
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
      final url = (decoded['videoUrl'] ?? decoded['VideoUrl'] ?? '').toString().trim();
      if (url.isEmpty) return null;
      final parsed = Uri.tryParse(url);
      if (parsed == null || !(parsed.isScheme('http') || parsed.isScheme('https'))) {
        return null;
      }
      return url;
    } catch (_) {
      return null;
    }
  }

  /// Prefer live manager URL; fall back to compile-time default if present.
  static Future<Uri?> resolveLaunchUri() async {
    final live = await fetchUrl();
    final raw = (live ?? DelegateAppLinks.tutorialVideoUrl).trim();
    if (raw.isEmpty) return null;
    return Uri.tryParse(raw);
  }
}
