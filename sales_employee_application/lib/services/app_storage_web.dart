// ignore_for_file: avoid_web_libraries_in_flutter, deprecated_member_use
import 'dart:convert';
import 'dart:html' as html;
import 'dart:typed_data';

import 'package:sales_employee_application/services/app_storage_base.dart';

AppStorage createPlatformStorage() => WebAppStorage();

class WebAppStorage implements AppStorage {
  static const _blobKey = 'qalaat_se_blob';
  Map<String, String> _memory = {};

  @override
  Future<void> init() async {
    final raw = html.window.localStorage[_blobKey];
    if (raw == null || raw.isEmpty) return;
    try {
      final data = jsonDecode(raw);
      if (data is Map) {
        _memory = data.map((k, v) => MapEntry('$k', '$v'));
      }
    } catch (_) {
      _memory = {};
    }
  }

  Future<void> _persist() async {
    html.window.localStorage[_blobKey] = jsonEncode(_memory);
  }

  @override
  String? getString(String key) => _memory[key];

  @override
  Future<void> setString(String key, String value) async {
    _memory[key] = value;
    await _persist();
  }

  @override
  Future<void> remove(String key) async {
    _memory.remove(key);
    await _persist();
  }

  @override
  Future<void> writeBytes(String name, List<int> bytes) async {
    final blob = html.Blob([Uint8List.fromList(bytes)], 'application/pdf');
    final url = html.Url.createObjectUrlFromBlob(blob);
    html.AnchorElement(href: url)
      ..setAttribute('download', name)
      ..click();
    html.Url.revokeObjectUrl(url);
  }

  @override
  Future<void> openNamedFile(String name) async {}
}
