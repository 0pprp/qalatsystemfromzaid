import 'dart:convert';

import 'package:sales_employee_application/services/local_store.dart';

class PendingSalesStore {
  static const _key = 'pending_sales';

  static List<Map<String, dynamic>> list() {
    final raw = LocalStore.instance.getString(_key);
    if (raw == null || raw.isEmpty) return [];
    try {
      final data = jsonDecode(raw);
      if (data is! List) return [];
      return data.map((e) => Map<String, dynamic>.from(e as Map)).toList();
    } catch (_) {
      return [];
    }
  }

  static Future<void> add(Map<String, dynamic> sale) async {
    final items = list();
    items.insert(0, sale);
    await LocalStore.instance.setString(_key, jsonEncode(items));
  }

  static Future<void> remove(String id) async {
    final items = list().where((s) => '${s['pendingId']}' != id).toList();
    await LocalStore.instance.setString(_key, jsonEncode(items));
  }
}
