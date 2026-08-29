class ApiConfig {
  // Base URL comes from login response (stored in SharedPreferences)
  static String? _baseUrl;

  static String? get baseUrl => _baseUrl;

  static void setBaseUrl(String url) {
    _baseUrl = url.endsWith('/') ? url : '$url/';
  }

  static String buildUrl(String path) {
    return '${_baseUrl ?? ''}$path';
  }
}
