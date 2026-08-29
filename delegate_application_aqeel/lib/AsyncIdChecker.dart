import 'dart:convert';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:http/http.dart' as http;

class AsyncIdChecker {
  // دالة واحدة للفحص - تنفذ فقط عند الاستدعاء
  static Future<bool> checkAsyncId() async {
    SharedPreferences prefs = await SharedPreferences.getInstance();
    String asyncId = prefs.getString('AsyncId') ?? '';
    String linkDelegate = prefs.getString('LinkDelegate') ?? '';

    if (asyncId.isEmpty || linkDelegate.isEmpty) {
      return false;
    }

    try {
      var uri = Uri.parse(
          '${linkDelegate}Delegates/GetDelegateLogin/asyncId=$asyncId');
      var response = await http.get(uri);

      if (response.statusCode == 200) {
        var data = json.decode(response.body);
        int delegateId = int.tryParse(data['delegateId'].toString()) ?? 0;
        return delegateId != 0;
      }
      return true; // Keep logged in on server error (not 200) to avoid disruption
    } catch (e) {
      return true; // Keep logged in on network error
    }
  }

  // دالة تسجيل الدخول - حفظ البيانات في SharedPreferences
  static Future<void> login({
    required String asyncId,
    required String linkDelegate,
    required String delegateId,
    required String delegateName,
    required String delegateCode,
  }) async {
    SharedPreferences prefs = await SharedPreferences.getInstance();
    await prefs.setString('AsyncId', asyncId);
    await prefs.setString('LinkDelegate', linkDelegate);
    await prefs.setString('DelegateID', delegateId);
    await prefs.setString('DelegateName', delegateName);
    await prefs.setString('DelegateCode', delegateCode);
  }

  // دالة تسجيل الخروج - مسح كل البيانات من SharedPreferences
  static Future<void> logout() async {
    SharedPreferences prefs = await SharedPreferences.getInstance();
    await prefs.remove('AsyncId');
    await prefs.remove('LinkDelegate');
    await prefs.remove('DelegateID');
    await prefs.remove('DelegateName');
    await prefs.remove('DelegateCode');
  }

  // دالة للحصول على بيانات المستخدم
  static Future<Map<String, String>> getUserData() async {
    SharedPreferences prefs = await SharedPreferences.getInstance();
    return {
      'AsyncId': prefs.getString('AsyncId') ?? '',
      'LinkDelegate': prefs.getString('LinkDelegate') ?? '',
      'DelegateID': prefs.getString('DelegateID') ?? '',
      'DelegateName': prefs.getString('DelegateName') ?? '',
      'DelegateCode': prefs.getString('DelegateCode') ?? '',
    };
  }

  // دالة للتحقق إذا كان المستخدم مسجل الدخول
  static Future<bool> isLoggedIn() async {
    SharedPreferences prefs = await SharedPreferences.getInstance();
    String asyncId = prefs.getString('AsyncId') ?? '';
    String linkDelegate = prefs.getString('LinkDelegate') ?? '';
    return asyncId.isNotEmpty && linkDelegate.isNotEmpty;
  }
}
