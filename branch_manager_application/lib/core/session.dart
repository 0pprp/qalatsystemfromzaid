import 'dart:convert';

import 'package:flutter/foundation.dart';
import 'package:shared_preferences/shared_preferences.dart';

/// One province the branch-manager account may open.
class BmAssignedCity {
  const BmAssignedCity({
    required this.name,
    required this.link,
    this.value,
    this.database,
    this.token,
  });

  final String name;
  final String link;
  final String? value;
  final String? database;

  /// Branch JWT for this city (set after successful login to that branch).
  final String? token;

  BmAssignedCity copyWith({String? token}) => BmAssignedCity(
        name: name,
        link: link,
        value: value,
        database: database,
        token: token ?? this.token,
      );

  Map<String, dynamic> toJson() => {
        'name': name,
        'link': link,
        if (value != null) 'value': value,
        if (database != null) 'database': database,
        if (token != null) 'token': token,
      };

  static BmAssignedCity fromJson(Map<String, dynamic> m) => BmAssignedCity(
        name: '${m['name'] ?? m['Name'] ?? ''}',
        link: '${m['link'] ?? m['Link'] ?? ''}',
        value: m['value']?.toString() ?? m['Value']?.toString(),
        database: m['database']?.toString() ?? m['Database']?.toString(),
        token: m['token']?.toString(),
      );
}

class BmSession {
  BmSession._();
  static final BmSession instance = BmSession._();

  /// Incremented when auth / city selection changes — root app listens to rebuild routes.
  static final ValueNotifier<int> revision = ValueNotifier(0);

  void _notify() => revision.value++;

  // Prefer SharedPreferences over FlutterSecureStorage: corrupted Android
  // Keystore/encrypted prefs caused a black screen before the first frame.
  static const _kToken = 'bm_token';
  static const _kGatewayToken = 'bm_gateway_token';
  static const _kApi = 'bm_api_base';
  static const _kCity = 'bm_city_name';
  static const _kUserName = 'bm_user_name';
  static const _kDisplayName = 'bm_display_name';
  static const _kUserType = 'bm_user_type';
  static const _kUserId = 'bm_user_id';
  static const _kPassword = 'bm_password';
  static const _kAssigned = 'bm_assigned_cities';
  static const _kActive = 'bm_active_city';
  static const _kVisitId = 'bm_active_visit_id';
  static const _kVisitEntryDone = 'bm_visit_entry_done';

  /// Active branch JWT (company API per province).
  String? token;

  /// Gateway JWT from `branch-manager-app/login` — required for assigned-tasks.
  String? gatewayToken;
  String? apiBase;
  String? cityName;

  /// Open branch-visit id (gateway) while working inside a province.
  String? activeVisitId;

  /// True after mandatory entry ratings were saved for [activeVisitId].
  bool visitEntryComplete = false;

  /// Login username (used for branch auth) — not the greeting name.
  String? userName;

  /// Display / welcome name from المدير المفوض.
  String? displayName;
  String? userType;
  String? userId;
  String? password;

  /// Name shown in UI greetings.
  String get greetingName {
    final d = (displayName ?? '').trim();
    if (d.isNotEmpty) return d;
    return (userName ?? '').trim();
  }

  /// Catalog cityValue for the currently open province (assigned-tasks filter).
  String? get activeCityValue {
    final name = (cityName ?? '').trim();
    final base = (apiBase ?? '').trim();
    BmAssignedCity? match;
    for (final c in assignedCities) {
      if (name.isNotEmpty && c.name == name) {
        match = c;
        break;
      }
      if (base.isNotEmpty && normalizeApiBase(c.link) == normalizeApiBase(base)) {
        match = c;
        break;
      }
    }
    if (match == null) return name.isEmpty ? null : name;
    final v = (match.value ?? '').trim();
    if (v.isNotEmpty) return v;
    final db = (match.database ?? '').trim();
    if (db.isNotEmpty) return db;
    return match.name.trim().isEmpty ? null : match.name.trim();
  }

  /// Provinces this account can open (after credential discovery or assignment).
  List<BmAssignedCity> assignedCities = [];

  /// True when logged in but no province selected yet (show cards).
  bool get needsCityPick =>
      isAuthenticated &&
      ((apiBase ?? '').isEmpty || (token ?? '').isEmpty);

  bool get isAuthenticated =>
      (userName ?? '').isNotEmpty &&
      userType == 'مدير فرع' &&
      assignedCities.isNotEmpty;

  bool get isLoggedIn =>
      isAuthenticated &&
      (token ?? '').isNotEmpty &&
      (apiBase ?? '').isNotEmpty;

  Future<void> load() async {
    final prefs = await SharedPreferences.getInstance();
    token = prefs.getString(_kToken);
    gatewayToken = prefs.getString(_kGatewayToken);
    password = prefs.getString(_kPassword);
    apiBase = prefs.getString(_kApi);
    cityName = prefs.getString(_kCity);
    userName = prefs.getString(_kUserName);
    displayName = prefs.getString(_kDisplayName);
    userType = prefs.getString(_kUserType);
    userId = prefs.getString(_kUserId);
    activeVisitId = prefs.getString(_kVisitId);
    visitEntryComplete = prefs.getBool(_kVisitEntryDone) ?? false;
    final raw = prefs.getString(_kAssigned);
    assignedCities = [];
    if (raw != null && raw.isNotEmpty) {
      try {
        final list = jsonDecode(raw) as List<dynamic>;
        assignedCities = list
            .map((e) =>
                BmAssignedCity.fromJson(Map<String, dynamic>.from(e as Map)))
            .where((c) => c.name.isNotEmpty && c.link.isNotEmpty)
            .toList();
      } catch (_) {}
    }
    await repairAfterLoad();
  }

  /// Fixes legacy installs that had branch JWT but no assigned-cities list (avoids stuck routes).
  Future<void> repairAfterLoad() async {
    final hasBranch =
        (token ?? '').isNotEmpty && (apiBase ?? '').isNotEmpty;
    final hasAccount =
        (userName ?? '').isNotEmpty && userType == 'مدير فرع';

    if (assignedCities.isEmpty && hasBranch && hasAccount) {
      assignedCities = [
        BmAssignedCity(
          name: (cityName ?? '').isNotEmpty ? cityName! : 'المحافظة',
          link: apiBase!,
          token: token,
        ),
      ];
      final prefs = await SharedPreferences.getInstance();
      await prefs.setString(
        _kAssigned,
        jsonEncode(assignedCities.map((c) => c.toJson()).toList()),
      );
      _notify();
      return;
    }

    if (!isAuthenticated &&
        !isLoggedIn &&
        ((userName ?? '').isNotEmpty ||
            (token ?? '').isNotEmpty ||
            assignedCities.isNotEmpty)) {
      await clear(notify: false);
      _notify();
    }
  }

  Future<void> saveCredentials({
    required String userName,
    required String userType,
    required String userId,
    required String password,
    required List<BmAssignedCity> cities,
    String? displayName,
    String? gatewayToken,
  }) async {
    if (userType != 'مدير فرع') {
      throw StateError('هذا التطبيق مخصص لإدارة الفرع فقط');
    }
    this.userName = userName.trim();
    this.displayName = (displayName ?? '').trim().isNotEmpty
        ? displayName!.trim()
        : this.userName;
    this.userType = userType;
    this.userId = userId;
    this.password = password;
    assignedCities = List.of(cities);
    // Clear active branch until user picks a card.
    token = null;
    apiBase = null;
    cityName = null;
    if ((gatewayToken ?? '').trim().isNotEmpty) {
      this.gatewayToken = gatewayToken!.trim();
    }

    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(_kPassword, password);
    await prefs.remove(_kToken);
    await prefs.setString(_kUserName, this.userName!);
    await prefs.setString(_kDisplayName, this.displayName!);
    await prefs.setString(_kUserType, userType);
    await prefs.setString(_kUserId, userId);
    await prefs.setString(
      _kAssigned,
      jsonEncode(assignedCities.map((c) => c.toJson()).toList()),
    );
    await prefs.remove(_kApi);
    await prefs.remove(_kCity);
    await prefs.remove(_kActive);
    if ((this.gatewayToken ?? '').isNotEmpty) {
      await prefs.setString(_kGatewayToken, this.gatewayToken!);
    }
    _notify();
  }

  Future<void> setGatewayToken(String? value) async {
    gatewayToken = (value ?? '').trim().isEmpty ? null : value!.trim();
    final prefs = await SharedPreferences.getInstance();
    if (gatewayToken == null) {
      await prefs.remove(_kGatewayToken);
    } else {
      await prefs.setString(_kGatewayToken, gatewayToken!);
    }
    _notify();
  }

  Future<void> selectCity(BmAssignedCity city, {required String token}) async {
    final normalized = normalizeApiBase(city.link);
    this.token = token;
    apiBase = normalized;
    cityName = city.name;
    assignedCities = assignedCities
        .map((c) => c.name == city.name && c.link == city.link
            ? c.copyWith(token: token)
            : c)
        .toList();

    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(_kToken, token);
    await prefs.setString(_kApi, normalized);
    await prefs.setString(_kCity, city.name);
    await prefs.setString(_kActive, city.name);
    await prefs.setString(
      _kAssigned,
      jsonEncode(assignedCities.map((c) => c.toJson()).toList()),
    );
    _notify();
  }

  /// Leave active branch data but keep account + assigned cities (back to cards).
  Future<void> clearActiveCity() async {
    token = null;
    apiBase = null;
    cityName = null;
    activeVisitId = null;
    visitEntryComplete = false;
    final prefs = await SharedPreferences.getInstance();
    await prefs.remove(_kToken);
    await prefs.remove(_kApi);
    await prefs.remove(_kCity);
    await prefs.remove(_kActive);
    await prefs.remove(_kVisitId);
    await prefs.remove(_kVisitEntryDone);
    _notify();
  }

  Future<void> setActiveVisitId(String? id, {bool entryComplete = false}) async {
    activeVisitId = (id ?? '').trim().isEmpty ? null : id!.trim();
    visitEntryComplete = activeVisitId != null && entryComplete;
    final prefs = await SharedPreferences.getInstance();
    if (activeVisitId == null) {
      await prefs.remove(_kVisitId);
      await prefs.remove(_kVisitEntryDone);
    } else {
      await prefs.setString(_kVisitId, activeVisitId!);
      await prefs.setBool(_kVisitEntryDone, visitEntryComplete);
    }
    _notify();
  }

  Future<void> markVisitEntryComplete() async {
    visitEntryComplete = true;
    final prefs = await SharedPreferences.getInstance();
    await prefs.setBool(_kVisitEntryDone, true);
    _notify();
  }

  Future<void> clear({bool notify = true}) async {
    token = null;
    gatewayToken = null;
    apiBase = null;
    cityName = null;
    userName = null;
    displayName = null;
    userType = null;
    userId = null;
    password = null;
    assignedCities = [];
    final prefs = await SharedPreferences.getInstance();
    await prefs.remove(_kToken);
    await prefs.remove(_kGatewayToken);
    await prefs.remove(_kPassword);
    await prefs.remove(_kApi);
    await prefs.remove(_kCity);
    await prefs.remove(_kUserName);
    await prefs.remove(_kDisplayName);
    await prefs.remove(_kUserType);
    await prefs.remove(_kUserId);
    await prefs.remove(_kAssigned);
    await prefs.remove(_kActive);
    await prefs.remove(_kVisitId);
    await prefs.remove(_kVisitEntryDone);
    activeVisitId = null;
    visitEntryComplete = false;
    if (notify) _notify();
  }

  static String normalizeApiBase(String url) {
    var value = url.trim().replaceAll('\\', '/');
    if (value.isEmpty) return value;
    if (!value.endsWith('/')) value = '$value/';
    return value;
  }

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
    return map is Map<String, dynamic>
        ? map
        : Map<String, dynamic>.from(map as Map);
  }
}
