import 'dart:convert';
import 'package:follower_application/config/app_env.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:http/http.dart' as http;

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
      var uri = Uri.parse(
          '${linkDelegate}Delegates/GetDelegateLogin/asyncId=$asyncId');
      var response = await http.get(uri).timeout(const Duration(seconds: 10));

      if (response.statusCode == 200) {
        var data = json.decode(response.body);
        int delegateId = int.tryParse(data['delegateId'].toString()) ?? 0;
        return delegateId != 0;
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
  }) async {
    SharedPreferences prefs = await SharedPreferences.getInstance();
    await prefs.setString('AsyncId', asyncId);
    await prefs.setString('LinkDelegate', linkDelegate);
    await prefs.setString('DelegateID', delegateId);
    await prefs.setString('DelegateName', delegateName);
  }

  static Future<void> logout() async {
    SharedPreferences prefs = await SharedPreferences.getInstance();
    await prefs.remove('AsyncId');
    await prefs.remove('LinkDelegate');
    await prefs.remove('DelegateID');
    await prefs.remove('DelegateName');
    await prefs.remove('SelectedChildId');
  }

  static Future<bool> isLoggedIn() async {
    SharedPreferences prefs = await SharedPreferences.getInstance();
    String asyncId = prefs.getString('AsyncId') ?? '';
    String linkDelegate = prefs.getString('LinkDelegate') ?? '';
    return asyncId.isNotEmpty && linkDelegate.isNotEmpty;
  }
}
