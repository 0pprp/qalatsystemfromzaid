import 'dart:convert';

import 'package:sales_employee_application/services/local_store.dart';

class GpsQueue {
  GpsQueue._();
  static final GpsQueue instance = GpsQueue._();
  static const _key = 'gps_queue';
  List<Map<String, dynamic>> _rows = [];
  bool _loaded = false;

  Future<void> _load() async {
    if (_loaded) return;
    _loaded = true;
    final raw = LocalStore.instance.getString(_key);
    if (raw == null || raw.isEmpty) return;
    try {
      final data = jsonDecode(raw);
      if (data is List) {
        _rows = data.map((e) => Map<String, dynamic>.from(e as Map)).toList();
      }
    } catch (_) {
      _rows = [];
    }
  }

  Future<void> _save() async {
    await LocalStore.instance.setString(_key, jsonEncode(_rows));
  }

  Future<void> enqueue({
    required String clientKey,
    required DateTime recordedAt,
    required double latitude,
    required double longitude,
    double? accuracy,
  }) async {
    await _load();
    if (_rows.any((row) => row['client_key'] == clientKey)) return;
    _rows.add({
      'client_key': clientKey,
      'recorded_at': recordedAt.toIso8601String(),
      'latitude': latitude,
      'longitude': longitude,
      'accuracy': accuracy,
      'synced': 0,
    });
    await _save();
  }

  Future<List<Map<String, dynamic>>> pending({int limit = 200}) async {
    await _load();
    return _rows.where((row) => row['synced'] != 1).take(limit).toList();
  }

  Future<int> pendingCount() async {
    await _load();
    return _rows.where((row) => row['synced'] != 1).length;
  }

  Future<void> markSynced(List<String> clientKeys) async {
    if (clientKeys.isEmpty) return;
    await _load();
    final keys = clientKeys.toSet();
    for (final row in _rows) {
      if (keys.contains('${row['client_key']}')) {
        row['synced'] = 1;
      }
    }
    await _save();
  }
}
