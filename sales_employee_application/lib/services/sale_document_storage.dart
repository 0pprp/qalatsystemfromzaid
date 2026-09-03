import 'dart:io';

import 'package:path_provider/path_provider.dart';

class SaleDocumentStorage {
  static Future<Directory> resolveDirectory({Directory? override}) async {
    if (override != null) {
      return override;
    }
    Directory? dir;
    try {
      dir = await getDownloadsDirectory().timeout(const Duration(seconds: 2));
    } catch (_) {}
    try {
      dir ??= await getApplicationDocumentsDirectory().timeout(const Duration(seconds: 2));
    } catch (_) {}
    dir ??= Directory.systemTemp;
    final folder = Directory('${dir.path}/SalesHaider');
    if (!await folder.exists()) {
      await folder.create(recursive: true);
    }
    return folder;
  }

  static Future<File> savePdf(String fileName, List<int> bytes, {Directory? directory}) async {
    final dir = await resolveDirectory(override: directory);
    final safe = fileName.replaceAll(RegExp(r'[\\/:*?"<>|]'), '_');
    final file = File('${dir.path}/$safe');
    await file.writeAsBytes(bytes, flush: true);
    return file;
  }
}
