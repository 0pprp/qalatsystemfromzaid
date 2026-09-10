import 'dart:convert';
import 'package:follower_application/config/app_env.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:http/http.dart' as http;

/// Session for User-based followers (Users.UserType = متابع).
/// Pref keys keep DelegateID name for older screens; value is UserId.
class AsyncIdChecker {
  static Future<bool> checkAsyncId() async {
    SharedPreferences prefs = await SharedPreferences.getInstance();
    String asyncId = prefs.getString('AsyncId') ?? '';
    String linkDelegate = AppEnv.apiBase(
      fallback: prefs.getString('LinkDelegate') ?? '',
    );

    if (asyncId.isEmpty || linkDelegate.isEmpty) {
      return false;
    }

    try {
      var uri = Uri.parse('${linkDelegate}Followers/Login')
          .replace(queryParameters: {'asyncId': asyncId});
      var response = await http.get(uri).timeout(const Duration(seconds: 10));

      if (response.statusCode == 200) {
        var data = json.decode(response.body);
        int userId = int.tryParse(
                (data['userId'] ?? data['delegateId'] ?? '0').toString()) ??
            0;
        return userId != 0;
      }
      if (response.statusCode == 403 || response.statusCode == 401) {
        return false;
      }
      return response.statusCode >= 500;
    } catch (e) {
      return true;
    }
  }

  static Future<void> login({
    required String asyncId,
    required String linkDelegate,
    required String delegateId,
    required String delegateName,
    String? userId,
  }) async {
    SharedPreferences prefs = await SharedPreferences.getInstance();
    await prefs.setString('AsyncId', asyncId);
    await prefs.setString('LinkDelegate', linkDelegate);
    final id = (userId != null && userId.isNotEmpty) ? userId : delegateId;
    await prefs.setString('DelegateID', id);
    await prefs.setString('UserId', id);
    await prefs.setString('DelegateName', delegateName);
    await prefs.setString('UserName', delegateName);
  }

  static Future<void> logout() async {
    SharedPreferences prefs = await SharedPreferences.getInstance();
    await prefs.remove('AsyncId');
    await prefs.remove('LinkDelegate');
    await prefs.remove('DelegateID');
    await prefs.remove('DelegateName');
    await prefs.remove('UserId');
    await prefs.remove('UserName');
    await prefs.remove('SelectedChildId');
  }

  static Future<bool> isLoggedIn() async {
    SharedPreferences prefs = await SharedPreferences.getInstance();
    String asyncId = prefs.getString('AsyncId') ?? '';
    String linkDelegate = prefs.getString('LinkDelegate') ?? '';
    return asyncId.isNotEmpty && linkDelegate.isNotEmpty;
  }
}
