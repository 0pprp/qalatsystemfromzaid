/// Gateway targeting — same sales-gw hosts as المدير المفوض / فلترة.
class AppEnv {
  static const String name =
      String.fromEnvironment('APP_ENV', defaultValue: 'demo');

  static const String demoHostFallback =
      'http://169.58.236.52:8080/sales-gw/api/';
  static const String productionHostFallback =
      'http://admincompany.alsaaeidy.com/sales-gw/api/';

  static bool get isDemo => name.trim().toLowerCase() == 'demo';

  static String apiBase() {
    if (isDemo) return demoHostFallback;
    return productionHostFallback;
  }

  static String resolveApiBase({required String env}) {
    if (env.trim().toLowerCase() == 'demo') return demoHostFallback;
    return productionHostFallback;
  }
}
