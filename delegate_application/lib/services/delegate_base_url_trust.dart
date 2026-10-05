/// Trusted LinkDelegate / session base URLs for the collector app.
/// Production catalogs historically serve branch APIs over HTTP on *.alsaaeidy.com.
class DelegateBaseUrlTrust {
  static const trustedHostSuffixes = <String>[
    '.alsaaeidy.com',
  ];

  static const trustedExactHosts = <String>{
    'localhost',
    '127.0.0.1',
    '169.58.236.52', // known demo/runtime host
  };

  /// Returns true when [baseUrl] may be stored and used for session checks.
  static bool isTrusted(String? baseUrl) {
    final uri = Uri.tryParse((baseUrl ?? '').trim());
    if (uri == null || uri.host.isEmpty) return false;
    if (uri.scheme != 'http' && uri.scheme != 'https') return false;

    final host = uri.host.toLowerCase();
    if (trustedExactHosts.contains(host)) return true;
    for (final suffix in trustedHostSuffixes) {
      if (host == suffix.substring(1) || host.endsWith(suffix)) {
        return true;
      }
    }
    // HTTPS to any host is allowed (TLS authenticity); HTTP must be allow-listed.
    return uri.scheme == 'https';
  }
}
