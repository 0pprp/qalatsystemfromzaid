import 'dart:convert';

import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:shared_preferences/shared_preferences.dart';

class BmSession {
  BmSession._();
  static final BmSession instance = BmSession._();

  static const _secure = FlutterSecureStorage(
    aOptions: AndroidOptions(encryptedSharedPreferences: true),
  );

  static const _kToken = 'bm_token';
  static const _kApi = 'bm_api_base';
  static const _kCity = 'bm_city_name';
  static const _kUserName = 'bm_user_name';
  static const _kUserType = 'bm_user_type';
  static const _kUserId = 'bm_user_id';

  String? token;
  String? apiBase;
  String? cityName;
  String? userName;
  String? userType;
  String? userId;

  bool get isLoggedIn =>
      (token ?? '').isNotEmpty &&
      (apiBase ?? '').isNotEmpty &&
      userType == 'مدير فرع';

  Future<void> load() async {
    token = await _secure.read(key: _kToken);
    final prefs = await SharedPreferences.getInstance();
    apiBase = prefs.getString(_kApi);
    cityName = prefs.getString(_kCity);
    userName = prefs.getString(_kUserName);
    userType = prefs.getString(_kUserType);
    userId = prefs.getString(_kUserId);
  }

  Future<void> save({
    required String token,
    required String apiBase,
    required String cityName,
    required String userName,
    required String userType,
    required String userId,
  }) async {
    if (userType != 'مدير فرع') {
      throw StateError('هذا التطبيق مخصص لمدير الفرع فقط');
    }
    this.token = token;
    this.apiBase = normalizeApiBase(apiBase);
    this.cityName = cityName;
    this.userName = userName;
    this.userType = userType;
    this.userId = userId;

    await _secure.write(key: _kToken, value: token);
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(_kApi, this.apiBase!);
    await prefs.setString(_kCity, cityName);
    await prefs.setString(_kUserName, userName);
    await prefs.setString(_kUserType, userType);
    await prefs.setString(_kUserId, userId);
  }

  Future<void> clear() async {
    token = null;
    apiBase = null;
    cityName = null;
    userName = null;
    userType = null;
    userId = null;
    await _secure.delete(key: _kToken);
    final prefs = await SharedPreferences.getInstance();
    await prefs.remove(_kApi);
    await prefs.remove(_kCity);
    await prefs.remove(_kUserName);
    await prefs.remove(_kUserType);
    await prefs.remove(_kUserId);
  }

  static String normalizeApiBase(String url) {
    var value = url.trim().replaceAll('\\', '/');
    if (value.isEmpty) return value;
    if (!value.endsWith('/')) value = '$value/';
    return value;
  }

  /// Decode JWT payload claims without verifying signature (server already issued it).
  static Map<String, dynamic> decodeJwtPayload(String jwt) {
    final parts = jwt.split('.');
    if (parts.length < 2) return {};
    var payload = parts[1].replaceAll('-', '+').replaceAll('_', '/');
    switch (payload.length % 4) {
      case 2:
        payload += '==';
        break;
      case 3:
        payload += '=';
        break;
    }
    final decoded = utf8.decode(base64.decode(payload));
    final map = jsonDecode(decoded);
    return map is Map<String, dynamic> ? map : Map<String, dynamic>.from(map as Map);
  }
}
