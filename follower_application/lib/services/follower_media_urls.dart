/// Builds follower media URLs from the same apiBase Flutter uses for API calls.
/// Avoids trusting absolute Host from backend when behind nginx (127.0.0.1:5402).
class FollowerMediaUrls {
  static String? resolve({
    required String apiBase,
    required int customerId,
    required String asyncId,
    required int listId,
    required Map<String, dynamic> image,
  }) {
    final kind = '${image['kind'] ?? image['Kind'] ?? ''}'.trim();
    final docId = int.tryParse('${image['documentId'] ?? image['DocumentId'] ?? 0}') ?? 0;
    final base = apiBase.endsWith('/') ? apiBase : '$apiBase/';

    if (docId > 0) {
      return Uri.parse('${base}Followers/Customers/$customerId/documents/$docId/file').replace(
        queryParameters: {
          'asyncId': asyncId,
          'listId': '$listId',
        },
      ).toString();
    }

    if (kind.toLowerCase() == 'shop') {
      return Uri.parse('${base}Followers/Customers/$customerId/shop-image').replace(
        queryParameters: {
          'asyncId': asyncId,
          'listId': '$listId',
        },
      ).toString();
    }

    final serverUrl = '${image['url'] ?? image['Url'] ?? ''}'.trim();
    if (serverUrl.isEmpty) return null;
    // Rewrite internal Kestrel hosts to the client apiBase host when present.
    try {
      final parsed = Uri.parse(serverUrl);
      if (parsed.host == '127.0.0.1' || parsed.host == 'localhost') {
        final api = Uri.parse(base);
        return parsed.replace(scheme: api.scheme, host: api.host, port: api.hasPort ? api.port : null).toString();
      }
    } catch (_) {}
    return serverUrl;
  }
}
