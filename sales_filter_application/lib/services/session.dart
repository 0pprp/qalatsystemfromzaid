import 'package:shared_preferences/shared_preferences.dart';

class Session {
  static String? token;
  static String? apiBase;
  static String? userName;
  static String? userType;
  static String? homeCityValue;

  static bool get isLoggedIn => (token ?? '').isNotEmpty && (apiBase ?? '').isNotEmpty;

  static Future<void> load() async {
    final p = await SharedPreferences.getInstance();
    token = p.getString('token');
    apiBase = p.getString('apiBase');
    userName = p.getString('userName');
    userType = p.getString('userType');
    homeCityValue = p.getString('homeCityValue');
  }

  static Future<void> save({
    required String tokenValue,
    required String base,
    required String name,
    required String type,
    String? homeCityValue,
  }) async {
    final p = await SharedPreferences.getInstance();
    token = tokenValue;
    apiBase = base;
    userName = name;
    userType = type;
    Session.homeCityValue = homeCityValue;
    await p.setString('token', tokenValue);
    await p.setString('apiBase', base);
    await p.setString('userName', name);
    await p.setString('userType', type);
    await p.setString('homeCityValue', homeCityValue ?? '');
  }

  static Future<void> clear() async {
    final p = await SharedPreferences.getInstance();
    await p.clear();
    token = apiBase = userName = userType = homeCityValue = null;
  }
}
