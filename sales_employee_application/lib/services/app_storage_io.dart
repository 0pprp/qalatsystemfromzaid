import 'dart:convert';
import 'dart:io';

import 'package:sales_employee_application/services/app_storage_base.dart';

AppStorage createPlatformStorage() => IoAppStorage();

class IoAppStorage implements AppStorage {
  Map<String, dynamic> _data = {};
  late Directory _dir;
  late File _file;

  @override
  Future<void> init() async {
    final root = Platform.environment['APPDATA'] ??
        Platform.environment['HOME'] ??
        Directory.systemTemp.path;
    _dir = Directory('$root${Platform.pathSeparator}QalaatSalesEmployee');
    if (!await _dir.exists()) {
      await _dir.create(recursive: true);
    }
    _file = File('${_dir.path}${Platform.pathSeparator}session.json');
    if (await _file.exists()) {
      try {
        _data = jsonDecode(await _file.readAsString()) as Map<String, dynamic>;
      } catch (_) {
        _data = {};
      }
    }
  }

  @override
  String? getString(String key) => _data[key]?.toString();

  @override
  Future<void> setString(String key, String value) async {
    _data[key] = value;
    await _file.writeAsString(jsonEncode(_data));
  }

  @override
  Future<void> remove(String key) async {
    _data.remove(key);
    await _file.writeAsString(jsonEncode(_data));
  }

  @override
  Future<void> writeBytes(String name, List<int> bytes) async {
    await File('${_dir.path}${Platform.pathSeparator}$name').writeAsBytes(bytes);
  }

  @override
  Future<void> openNamedFile(String name) async {
    final path = '${_dir.path}${Platform.pathSeparator}$name';
    if (Platform.isWindows) {
      await Process.start('cmd', ['/c', 'start', '', path], runInShell: true);
    } else {
      await Process.start('xdg-open', [path]);
    }
  }
}
