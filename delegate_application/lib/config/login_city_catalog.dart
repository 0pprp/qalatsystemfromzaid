import 'dart:convert';

import 'package:delegate_application/config/app_env.dart';

/// Governorate list for Login — demo is local-only; production uses GetHaider.
class LoginCityCatalog {
  static const String productionCityApiUrl =
      'http://defaultdata.alsaaeidy.com/GetHaider';

  static const String demoCacheKey = 'cached_city_data_demo';
  static const String productionCacheKey = 'cached_city_data_production';

  /// Legacy key used before env-separated caches (production fallback only).
  static const String legacyCacheKey = 'cached_city_data';

  /// Demo entry. [number] is unused by Login (only name+link are read);
  /// kept for map shape parity with GetHaider. نجف City id in GetHaider is "11".
  static const Map<String, String> demoNajafCity = {
    'name': 'النجف - DEMO',
    'link': AppEnv.demoApiBaseUrl,
    'number': '11',
  };

  static String cacheKeyForEnv([String? envName]) {
    final env = (envName ?? AppEnv.name).toLowerCase();
    return env == 'demo' ? demoCacheKey : productionCacheKey;
  }

  static bool shouldCallProductionCityApi([String? envName]) {
    final env = (envName ?? AppEnv.name).toLowerCase();
    return env != 'demo';
  }

  static List<Map<String, String>> demoCities() => [
        Map<String, String>.from(demoNajafCity),
      ];

  static List<Map<String, String>> parseCityJson(String jsonBody) {
    final List<dynamic> data = json.decode(jsonBody) as List<dynamic>;
    return data.map((item) {
      return <String, String>{
        'name': item['name']?.toString() ?? '',
        'link': item['link']?.toString() ?? '',
        'number': item['number']?.toString() ?? '',
      };
    }).toList();
  }

  /// Demo must never consume production / legacy caches.
  static String? resolveCacheJson({
    required String envName,
    required String? envSpecificCache,
    required String? legacyCache,
  }) {
    if (envName.toLowerCase() == 'demo') {
      return null; // demo is always the local single-city list
    }
    if (envSpecificCache != null && envSpecificCache.isNotEmpty) {
      return envSpecificCache;
    }
    if (legacyCache != null && legacyCache.isNotEmpty) {
      return legacyCache;
    }
    return null;
  }
}
