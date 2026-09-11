import 'dart:convert';
import 'package:follower_application/config/app_env.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:http/http.dart' as http;

/// Session for User-based followers (Users.UserType = متابع).
/// Pref keys keep DelegateID name for older screens; value is UserId.
/// AsyncId is an internal session token from the server — never shown in UI.
class AsyncIdChecker {
  static const _cityIdKey = 'CityId';
  static const _cityNameKey = 'CityName';

  /// Validates stored session with Backend (GET Login by AsyncID).
  /// Returns false when UserType is no longer متابع or UserState inactive.
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
        final userType = (data['userType'] ?? '').toString();
        final isActive = data['isActive'] == true ||
            data['isActive']?.toString().toLowerCase() == 'true';
        if (userId == 0) return false;
        if (userType.isNotEmpty && !userType.startsWith('متابع')) {
          return false;
        }
        if (data.containsKey('isActive') && !isActive) {
          return false;
        }
        // Refresh identity fields when present.
        final name = (data['userName'] ?? data['delegateName'] ?? '').toString();
        if (name.isNotEmpty) {
          await prefs.setString('UserName', name);
          await prefs.setString('DelegateName', name);
        }
        await prefs.setString('UserId', userId.toString());
        await prefs.setString('DelegateID', userId.toString());
        final cityId = data['cityId'];
        if (cityId != null) {
          await prefs.setString(_cityIdKey, cityId.toString());
        }
        final cityName = data['cityName']?.toString();
        if (cityName != null && cityName.isNotEmpty) {
          await prefs.setString(_cityNameKey, cityName);
        }
        return true;
      }
      if (response.statusCode == 403 || response.statusCode == 401) {
        return false;
      }
      // Transient server errors: keep local session (offline-friendly).
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
    String? cityId,
    String? cityName,
  }) async {
    SharedPreferences prefs = await SharedPreferences.getInstance();
    await prefs.setString('AsyncId', asyncId);
    await prefs.setString('LinkDelegate', linkDelegate);
    final id = (userId != null && userId.isNotEmpty) ? userId : delegateId;
    await prefs.setString('DelegateID', id);
    await prefs.setString('UserId', id);
    await prefs.setString('DelegateName', delegateName);
    await prefs.setString('UserName', delegateName);
    if (cityId != null && cityId.isNotEmpty) {
      await prefs.setString(_cityIdKey, cityId);
    }
    if (cityName != null && cityName.isNotEmpty) {
      await prefs.setString(_cityNameKey, cityName);
    }
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
    await prefs.remove(_cityIdKey);
    await prefs.remove(_cityNameKey);
  }

  static Future<bool> isLoggedIn() async {
    SharedPreferences prefs = await SharedPreferences.getInstance();
    String asyncId = prefs.getString('AsyncId') ?? '';
    String linkDelegate = prefs.getString('LinkDelegate') ?? '';
    return asyncId.isNotEmpty && linkDelegate.isNotEmpty;
  }
}
