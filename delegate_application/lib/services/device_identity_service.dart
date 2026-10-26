import 'package:package_info_plus/package_info_plus.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:uuid/uuid.dart';

/// Stable per-install device id for payment audit (no IMEI required).
/// Sent to BE as DeviceId when syncing collections.
class DeviceIdentityService {
  DeviceIdentityService._();
  static final DeviceIdentityService instance = DeviceIdentityService._();

  static const _prefsKey = 'delegate_device_install_id_v1';

  String? _cachedId;
  String? _cachedModel;
  String? _cachedPlatform;
  String? _cachedAppVersion;

  Future<Map<String, String>> payloadFields() async {
    await _ensure();
    return {
      'DeviceId': _cachedId ?? '',
      'DeviceModel': _cachedModel ?? '',
      'DevicePlatform': _cachedPlatform ?? 'android',
      'AppVersion': _cachedAppVersion ?? '',
    };
  }

  Future<void> _ensure() async {
    if (_cachedId != null && _cachedId!.isNotEmpty) return;
    final prefs = await SharedPreferences.getInstance();
    var id = prefs.getString(_prefsKey);
    if (id == null || id.isEmpty) {
      id = const Uuid().v4();
      await prefs.setString(_prefsKey, id);
    }
    _cachedId = id;
    _cachedPlatform = 'android';
    try {
      final info = await PackageInfo.fromPlatform();
      _cachedAppVersion = '${info.version}+${info.buildNumber}';
      _cachedModel = info.appName;
    } catch (_) {
      _cachedAppVersion = '';
      _cachedModel = 'delegate_application';
    }
  }
}
