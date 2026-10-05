import 'package:main_follower_application/core/auth/session.dart';
import 'package:main_follower_application/core/network/api_client.dart';

class AuthRepository {
  static const expectedUserType = 'متابع رئيسي';

  static Future<void> login({
    required String userName,
    required String password,
  }) async {
    final json = await ApiClient.postJson('Auth/LoginMainFollower', {
      'userName': userName.trim(),
      'password': password,
    });
    final token = (json['token'] ?? '').toString();
    final type = (json['userType'] ?? '').toString();
    final loginName = (json['userName'] ?? userName).toString();
    final display = (json['displayName'] ?? loginName).toString();
    if (token.isEmpty) throw ApiException('استجابة تسجيل الدخول غير مكتملة');
    if (type != expectedUserType) {
      throw ApiException('هذا الحساب ليس متابعاً رئيسياً');
    }
    await Session.save(token: token, userName: loginName, displayName: display);
  }

  static Future<void> logout() => Session.clear();
}
