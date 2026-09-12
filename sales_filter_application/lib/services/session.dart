import 'package:shared_preferences/shared_preferences.dart';

class Session {
  static String? token;
  static String? apiBase;
  static String? userName;
  static String? userType;
  static String? cityLink;
  static String? cityValue;
  static String? cityName;

  static bool get isLoggedIn => (token ?? '').isNotEmpty && (apiBase ?? '').isNotEmpty;

  static Future<void> load() async {
    final p = await SharedPreferences.getInstance();
    token = p.getString('token');
    apiBase = p.getString('apiBase');
    userName = p.getString('userName');
    userType = p.getString('userType');
    cityLink = p.getString('cityLink');
    cityValue = p.getString('cityValue');
    cityName = p.getString('cityName');
  }

  static Future<void> save({
    required String tokenValue,
    required String base,
    required String name,
    required String type,
    String? link,
    String? branchValue,
    String? branchName,
  }) async {
    final p = await SharedPreferences.getInstance();
    token = tokenValue;
    apiBase = base;
    userName = name;
    userType = type;
    cityLink = link;
    cityValue = branchValue;
    cityName = branchName;
    await p.setString('token', tokenValue);
    await p.setString('apiBase', base);
    await p.setString('userName', name);
    await p.setString('userType', type);
    await p.setString('cityLink', link ?? '');
    await p.setString('cityValue', branchValue ?? '');
    await p.setString('cityName', branchName ?? '');
  }

  static Future<void> clear() async {
    final p = await SharedPreferences.getInstance();
    await p.clear();
    token = apiBase = userName = userType = cityLink = cityValue = cityName = null;
  }
}
