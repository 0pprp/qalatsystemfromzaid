import 'dart:convert';
import 'package:delegate_application/services/delegate_base_url_trust.dart';
import 'package:delegate_application/services/session_validation.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:http/http.dart' as http;

class AsyncIdChecker {
  static const Duration networkTimeout = Duration(seconds: 15);

  /// Explicit validation: network failure → [SessionValidation.unverified],
  /// never silently treated as Valid for destructive operations.
  static Future<SessionValidation> validateSession() async {
    SharedPreferences prefs = await SharedPreferences.getInstance();
    String asyncId = prefs.getString('AsyncId') ?? '';
    String linkDelegate = prefs.getString('LinkDelegate') ?? '';

    if (asyncId.isEmpty || linkDelegate.isEmpty) {
      return SessionValidation.invalid;
    }

    if (!DelegateBaseUrlTrust.isTrusted(linkDelegate)) {
      return SessionValidation.invalid;
    }

    final uri = Uri.tryParse(
        '${linkDelegate}Delegates/GetDelegateLogin/asyncId=$asyncId');
    if (uri == null) {
      return SessionValidation.invalid;
    }

    try {
      var response = await http.get(uri).timeout(networkTimeout);

      if (response.statusCode == 200) {
        final data = json.decode(response.body);
        if (data is! Map) {
          return SessionValidation.invalid;
        }
        final raw = data['delegateId'] ?? data['DelegateId'];
        final delegateId = int.tryParse(raw?.toString() ?? '') ?? 0;
        return delegateId != 0
            ? SessionValidation.valid
            : SessionValidation.invalid;
      }
      if (response.statusCode == 401 || response.statusCode == 403) {
        return SessionValidation.invalid;
      }
      // 404/5xx/etc.: do not wipe session — treat as unverified.
      return SessionValidation.unverified;
    } catch (_) {
      return SessionValidation.unverified;
    }
  }

  /// Legacy UI gate: keep session while [unverified] (offline).
  /// Only [invalid] forces logout. Destructive ops must call [validateSession].
  static Future<bool> checkAsyncId() async {
    final v = await validateSession();
    return v != SessionValidation.invalid;
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
