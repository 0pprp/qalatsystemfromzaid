import 'dart:convert';

import 'package:sales_employee_application/services/local_store.dart';

class Session {
  static const _tokenKey = 'se_token';
  static const _userKey = 'se_user';
  static const _shiftKey = 'se_shift_date';
  static const _shiftJsonKey = 'se_shift_json';
  static const _lastSaleKey = 'se_last_sale';

  static Future<void> init() async {
    await LocalStore.instance.init();
  }

  static String? get token => LocalStore.instance.getString(_tokenKey);

  static bool get isLoggedIn => (token ?? '').isNotEmpty;

  static Map<String, dynamic> get user {
    final raw = LocalStore.instance.getString(_userKey);
    if (raw == null || raw.isEmpty) return {};
    return jsonDecode(raw) as Map<String, dynamic>;
  }

  static String get userName => '${user['userName'] ?? user['UserName'] ?? ''}';
  static String get cityName => '${user['cityName'] ?? user['CityName'] ?? ''}';
  static String get userId => '${user['userId'] ?? user['UserID'] ?? ''}';

  static Future<void> saveLogin(Map<String, dynamic> data) async {
    await LocalStore.instance.setString(_tokenKey, '${data['token'] ?? ''}');
    await LocalStore.instance.setString(_userKey, jsonEncode(data));
  }

  static Future<void> logout() async {
    await LocalStore.instance.remove(_tokenKey);
    await LocalStore.instance.remove(_userKey);
    await LocalStore.instance.remove(_shiftKey);
    await LocalStore.instance.remove(_shiftJsonKey);
  }

  static String? get shiftDateKey => LocalStore.instance.getString(_shiftKey);

  static Map<String, dynamic>? get shift {
    final raw = LocalStore.instance.getString(_shiftJsonKey);
    if (raw == null || raw.isEmpty) return null;
    return jsonDecode(raw) as Map<String, dynamic>;
  }

  static Future<void> saveShift(Map<String, dynamic> data, String dateKey) async {
    await LocalStore.instance.setString(_shiftKey, dateKey);
    await LocalStore.instance.setString(_shiftJsonKey, jsonEncode(data));
  }

  static Future<void> clearShift() async {
    await LocalStore.instance.remove(_shiftKey);
    await LocalStore.instance.remove(_shiftJsonKey);
  }

  static Map<String, dynamic>? get lastSale {
    final raw = LocalStore.instance.getString(_lastSaleKey);
    if (raw == null || raw.isEmpty) return null;
    return jsonDecode(raw) as Map<String, dynamic>;
  }

  static Future<void> saveLastSale(Map<String, dynamic> data) async {
    await LocalStore.instance.setString(_lastSaleKey, jsonEncode(data));
  }
}
