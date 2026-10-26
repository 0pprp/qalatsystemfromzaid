import 'dart:convert';
import 'package:delegate_application/services/delegate_base_url_trust.dart';
import 'package:delegate_application/services/session_validation.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:http/http.dart' as http;

class AsyncIdChecker {
  static const Duration networkTimeout = Duration(seconds: 15);

  /// Last server message when session became invalid (e.g. follower collection lock).
  static String? lastInvalidMessage;

  /// Explicit validation: network failure → [SessionValidation.unverified],
  /// never silently treated as Valid for destructive operations.
  static Future<SessionValidation> validateSession() async {
    SharedPreferences prefs = await SharedPreferences.getInstance();
    String asyncId = prefs.getString('AsyncId') ?? '';
    String linkDelegate = prefs.getString('LinkDelegate') ?? '';

    if (asyncId.isEmpty || linkDelegate.isEmpty) {
      lastInvalidMessage = null;
      return SessionValidation.invalid;
    }

    if (!DelegateBaseUrlTrust.isTrusted(linkDelegate)) {
      lastInvalidMessage = 'رابط الدخول غير موثوق';
      return SessionValidation.invalid;
    }

    final uri = Uri.tryParse(
        '${linkDelegate}Delegates/GetDelegateLogin/asyncId=$asyncId');
    if (uri == null) {
      lastInvalidMessage = null;
      return SessionValidation.invalid;
    }

    try {
      var response = await http.get(uri).timeout(networkTimeout);

      if (response.statusCode == 200) {
        final data = json.decode(response.body);
        if (data is! Map) {
          lastInvalidMessage = null;
          return SessionValidation.invalid;
        }
        // Defense: if BE ever returns 200 with the flag still true, kick anyway.
        final locked = data['followerCollectionEnabled'] == true ||
            data['FollowerCollectionEnabled'] == true;
        if (locked) {
          lastInvalidMessage =
              'الحساب غير فعال للجباية — اليوم جباية المتابع على هذه القائمة';
          return SessionValidation.invalid;
        }
        final raw = data['delegateId'] ?? data['DelegateId'];
        final delegateId = int.tryParse(raw?.toString() ?? '') ?? 0;
        if (delegateId != 0) {
          lastInvalidMessage = null;
          return SessionValidation.valid;
        }
        lastInvalidMessage = null;
        return SessionValidation.invalid;
      }
      if (response.statusCode == 401 || response.statusCode == 403) {
        lastInvalidMessage = _messageFromBody(response.body) ??
            'الحساب غير فعال للجباية — اليوم جباية المتابع على هذه القائمة';
        return SessionValidation.invalid;
      }
      // 404/5xx/etc.: do not wipe session — treat as unverified.
      return SessionValidation.unverified;
    } catch (_) {
      return SessionValidation.unverified;
    }
  }

  static String? _messageFromBody(String body) {
    try {
      final data = json.decode(body);
      if (data is Map) {
        final m = '${data['message'] ?? data['Message'] ?? ''}'.trim();
        if (m.isNotEmpty) return m;
      }
      if (data is String && data.trim().isNotEmpty) return data.trim();
    } catch (_) {
      final t = body.trim();
      if (t.isNotEmpty && t.length < 200) return t;
    }
    return null;
  }

  /// Legacy UI gate: keep session while [unverified] (offline).
  /// Only [invalid] forces logout. Destructive ops must call [validateSession].
  static Future<bool> checkAsyncId() async {
    final v = await validateSession();
    return v != SessionValidation.invalid;
  }

  /// Re-validate online; if invalid, logout and return the kick message (or null if still ok/offline).
  static Future<String?> enforceSessionKickMessage() async {
    final v = await validateSession();
    if (v != SessionValidation.invalid) return null;
    final msg = lastInvalidMessage ??
        'الحساب غير فعال للجباية — اليوم جباية المتابع على هذه القائمة';
    await logout();
    return msg;
  }

  static Future<void> login({
    required String asyncId,
    required String linkDelegate,
    required String delegateId,
    required String delegateName,
    required String delegateCode,
  }) async {
    if (!DelegateBaseUrlTrust.isTrusted(linkDelegate)) {
      throw ArgumentError('رابط الدخول غير موثوق');
    }
    SharedPreferences prefs = await SharedPreferences.getInstance();
    await prefs.setString('AsyncId', asyncId);
    await prefs.setString('LinkDelegate', linkDelegate);
    await prefs.setString('DelegateID', delegateId);
    await prefs.setString('DelegateName', delegateName);
    await prefs.setString('DelegateCode', delegateCode);
    lastInvalidMessage = null;
  }

  static Future<void> logout() async {
    SharedPreferences prefs = await SharedPreferences.getInstance();
    await prefs.remove('AsyncId');
    await prefs.remove('LinkDelegate');
    await prefs.remove('DelegateID');
    await prefs.remove('DelegateName');
    await prefs.remove('DelegateCode');
  }

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

  static Future<bool> isLoggedIn() async {
    SharedPreferences prefs = await SharedPreferences.getInstance();
    String asyncId = prefs.getString('AsyncId') ?? '';
    String linkDelegate = prefs.getString('LinkDelegate') ?? '';
    return asyncId.isNotEmpty && linkDelegate.isNotEmpty;
  }
}
