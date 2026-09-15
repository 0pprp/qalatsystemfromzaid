import 'package:shared_preferences/shared_preferences.dart';

/// Delegated manager session. Central account: no province is stored.
class Session {
  static String? token;
  static String? apiBase;
  static String? userName;
  static String? userType;
  static bool central = true;

  static bool get isLoggedIn =>
      (token ?? '').isNotEmpty && (apiBase ?? '').isNotEmpty;

  static Future<void> load() async {
    final p = await SharedPreferences.getInstance();
    token = p.getString('token');
    apiBase = p.getString('apiBase');
    userName = p.getString('userName');
    userType = p.getString('userType');
    central = p.getBool('central') ?? true;
  }

  static Future<void> save({
    required String tokenValue,
    required String base,
    required String name,
    required String type,
    bool centralAccount = true,
  }) async {
    final p = await SharedPreferences.getInstance();
    token = tokenValue;
    apiBase = base;
    userName = name;
    userType = type;
    central = centralAccount;
    await p.setString('token', tokenValue);
    await p.setString('apiBase', base);
    await p.setString('userName', name);
    await p.setString('userType', type);
    await p.setBool('central', centralAccount);
  }

  static Future<void> clear() async {
    final p = await SharedPreferences.getInstance();
    await p.clear();
    token = apiBase = userName = userType = null;
    central = true;
  }
}
