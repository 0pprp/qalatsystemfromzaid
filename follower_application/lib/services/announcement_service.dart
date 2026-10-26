import 'dart:convert';

import 'package:follower_application/config/app_env.dart';
import 'package:follower_application/core/update/app_version.dart';
import 'package:http/http.dart' as http;

class AppAnnouncement {
  const AppAnnouncement({
    required this.id,
    required this.contentType,
    required this.textBody,
    required this.imageUrl,
    required this.zoomImageUrl,
    required this.hasLink,
    required this.linkUrl,
  });

  final int id;
  final String contentType;
  final String textBody;
  final String imageUrl;
  final String zoomImageUrl;
  final bool hasLink;
  final String linkUrl;

  bool get isImage => contentType.toLowerCase() == 'image';

  /// Prefer dedicated zoom image; fall back to banner.
  String get previewImageUrl =>
      zoomImageUrl.isNotEmpty ? zoomImageUrl : imageUrl;
}

class AnnouncementService {
  static String _abs(String base, String path) {
    var image = path.trim();
    if (image.startsWith('/')) {
      final root = base.replaceAll(RegExp(r'/api/?$'), '');
      image = '$root$image';
    }
    return image;
  }

  static Future<List<AppAnnouncement>> fetch({String? appKey}) async {
    final key = (appKey ?? AppVersion.appKey).trim();
    if (key.isEmpty) return const [];
    try {
      final base = AppEnv.gatewayApiBase();
      final uri = Uri.parse('${base}mobile-announcements/$key');
      final response = await http.get(uri).timeout(const Duration(seconds: 12));
      if (response.statusCode != 200) return const [];
      final decoded = jsonDecode(response.body);
      if (decoded is! List) return const [];
      final out = <AppAnnouncement>[];
      for (final raw in decoded) {
        if (raw is! Map) continue;
        final id = int.tryParse('${raw['id'] ?? raw['Id'] ?? 0}') ?? 0;
        final type =
            '${raw['contentType'] ?? raw['ContentType'] ?? 'text'}'.trim();
        final text = '${raw['textBody'] ?? raw['TextBody'] ?? ''}'.trim();
        final image = _abs(
            base, '${raw['imageUrl'] ?? raw['ImageUrl'] ?? ''}');
        var zoom = _abs(
            base, '${raw['zoomImageUrl'] ?? raw['ZoomImageUrl'] ?? ''}');
        if (zoom.isEmpty) zoom = image;
        final hasLink = raw['hasLink'] == true ||
            raw['HasLink'] == true ||
            '${raw['hasLink'] ?? raw['HasLink']}' == 'true';
        final link = '${raw['linkUrl'] ?? raw['LinkUrl'] ?? ''}'.trim();
        if (type.toLowerCase() == 'text' && text.isEmpty) continue;
        if (type.toLowerCase() == 'image' && image.isEmpty) continue;
        out.add(AppAnnouncement(
          id: id,
          contentType: type.isEmpty ? 'text' : type,
          textBody: text,
          imageUrl: image,
          zoomImageUrl: zoom,
          hasLink: hasLink && link.isNotEmpty,
          linkUrl: link,
        ));
      }
      return out;
    } catch (_) {
      return const [];
    }
  }
}
