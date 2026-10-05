import 'package:shared_preferences/shared_preferences.dart';

class Session {
  static const _kToken = 'mf_token';
  static const _kUser = 'mf_user';
  static const _kDisplay = 'mf_display';

  static String? token;
  static String? userName;
  static String? displayName;

  static bool get isLoggedIn => token != null && token!.isNotEmpty;

  static Future<void> load() async {
    final p = await SharedPreferences.getInstance();
    token = p.getString(_kToken);
    userName = p.getString(_kUser);
    displayName = p.getString(_kDisplay);
  }

  static Future<void> save({
    required String token,
    required String userName,
    String? displayName,
  }) async {
    final p = await SharedPreferences.getInstance();
    await p.setString(_kToken, token);
    await p.setString(_kUser, userName);
    if (displayName != null) await p.setString(_kDisplay, displayName);
    Session.token = token;
    Session.userName = userName;
    Session.displayName = displayName ?? userName;
  }

  static Future<void> clear() async {
    final p = await SharedPreferences.getInstance();
    await p.remove(_kToken);
    await p.remove(_kUser);
    await p.remove(_kDisplay);
    token = null;
    userName = null;
    displayName = null;
  }
}
