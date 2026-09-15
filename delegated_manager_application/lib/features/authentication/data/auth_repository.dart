import 'package:delegated_manager_application/core/auth/session.dart';
import 'package:delegated_manager_application/core/config/app_env.dart';
import 'package:delegated_manager_application/core/network/api_client.dart';
import 'package:delegated_manager_application/core/utils/json_read.dart';

/// Result of `POST Auth/LoginDelegatedManager`. The account is central, so no
/// province is requested or stored.
class DelegatedManagerLogin {
  const DelegatedManagerLogin({
    required this.token,
    required this.userName,
    required this.userType,
    required this.central,
    this.expiration,
  });

  final String token;
  final String userName;
  final String userType;
  final bool central;
  final String? expiration;

  static const String expectedUserType = 'مدير مفوض';

  bool get isDelegatedManager => userType == expectedUserType;

  factory DelegatedManagerLogin.fromJson(Map<String, dynamic> json) =>
      DelegatedManagerLogin(
        token: JsonRead.text(json['token']),
        userName: JsonRead.text(json['userName']),
        userType: JsonRead.text(json['userType']),
        central: JsonRead.flag(json['central'], fallback: true),
        expiration: JsonRead.optionalText(json['expiration']),
      );
}

class AuthRepository {
  static Future<DelegatedManagerLogin> login({
    required String userName,
    required String password,
  }) async {
    final json = await ApiClient.loginDelegatedManager(
      userName: userName.trim(),
      password: password,
    );
    final login = DelegatedManagerLogin.fromJson(json);
    if (login.token.isEmpty) {
      throw ApiException('استجابة تسجيل الدخول غير مكتملة');
    }
    if (!login.isDelegatedManager) {
      throw ApiException('هذا الحساب ليس حساب مدير مفوض');
    }

    await Session.save(
      tokenValue: login.token,
      base: AppEnv.apiBase(),
      name: login.userName,
      type: login.userType,
      centralAccount: login.central,
    );
    return login;
  }

  static Future<void> logout() => Session.clear();
}
