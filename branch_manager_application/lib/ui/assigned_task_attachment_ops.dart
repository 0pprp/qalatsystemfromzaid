import 'dart:io';

import 'package:branch_manager_application/core/api.dart';
import 'package:branch_manager_application/ui/assigned_tasks_actions.dart';
import 'package:open_filex/open_filex.dart';
import 'package:path/path.dart' as p;
import 'package:path_provider/path_provider.dart';
import 'package:share_plus/share_plus.dart';

/// Download + open / share helpers for BM assigned-task attachments.
class BmAssignedTaskAttachmentOps {
  BmAssignedTaskAttachmentOps._();

  static String sanitizeFileName(String raw) {
    var name = raw.trim();
    if (name.isEmpty) name = 'attachment';
    name = name.replaceAll(RegExp(r'[\\/:*?"<>|]'), '_');
    if (name.length > 120) {
      final ext = p.extension(name);
      final base = p.basenameWithoutExtension(name);
      name = '${base.substring(0, (120 - ext.length).clamp(1, 120))}$ext';
    }
    return name;
  }

  static Future<Directory> _dir() async {
    final root = await getApplicationDocumentsDirectory();
    final dir = Directory(p.join(root.path, 'assigned-task-attachments'));
    if (!await dir.exists()) {
      await dir.create(recursive: true);
    }
    return dir;
  }

  static Future<File> ensureLocalFile({
    required String taskId,
    required BmAssignedTaskAttachment file,
  }) async {
    final downloaded = await BmApi.instance.downloadAssignedTaskAttachment(
      taskId: taskId,
      fileId: file.id,
      preferredFileName: file.fileName,
    );
    final safeName = sanitizeFileName(
      downloaded.fileName.isNotEmpty ? downloaded.fileName : file.fileName,
    );
    final dir = await _dir();
    final local = File(p.join(dir.path, '${taskId}_${file.id}_$safeName'));
    await local.writeAsBytes(downloaded.bytes, flush: true);
    return local;
  }

  static Future<OpenResult> downloadAndOpen({
    required String taskId,
    required BmAssignedTaskAttachment file,
  }) async {
    final local = await ensureLocalFile(taskId: taskId, file: file);
    return OpenFilex.open(
      local.path,
      type: (file.contentType ?? '').trim().isEmpty ? null : file.contentType,
    );
  }

  static Future<void> downloadAndShare({
    required String taskId,
    required BmAssignedTaskAttachment file,
  }) async {
    final local = await ensureLocalFile(taskId: taskId, file: file);
    await Share.shareXFiles(
      [
        XFile(
          local.path,
          mimeType: file.contentType,
          name: sanitizeFileName(file.fileName),
        ),
      ],
      subject: file.fileName,
    );
  }
}
