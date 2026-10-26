import 'dart:io';

import 'package:delegated_manager_application/core/auth/session.dart';
import 'package:delegated_manager_application/core/network/api_client.dart';
import 'package:delegated_manager_application/core/utils/json_read.dart';
import 'package:delegated_manager_application/features/assigned_tasks/domain/assigned_task.dart';
import 'package:http/http.dart' as http;
import 'package:open_filex/open_filex.dart';
import 'package:path_provider/path_provider.dart';

class AssignedTasksRepository {
  static const String _base = 'delegated-manager/assigned-tasks';

  static Future<List<AssignedTask>> inbox() async {
    final json = await ApiClient.get('$_base/inbox');
    final rows = JsonRead.list(json);
    return sortAssignedTasks(rows.map(AssignedTask.fromJson));
  }

  static Future<AssignedTask> accept(String id) async {
    final json = await ApiClient.post('$_base/$id/accept');
    return AssignedTask.fromJson(JsonRead.map(json));
  }

  /// Reject requires a non-empty note (`NoteBody.note` on the gateway).
  static Future<AssignedTask> reject(String id, {required String note}) async {
    final trimmed = note.trim();
    if (trimmed.isEmpty) {
      throw ApiException('ملاحظة الرفض مطلوبة');
    }
    final json = await ApiClient.post(
      '$_base/$id/reject',
      body: {'note': trimmed},
    );
    return AssignedTask.fromJson(JsonRead.map(json));
  }

  /// Transfer body matches `TransferBody`: targetUserId and/or targetKey,
  /// targetDisplayName, optional note.
  static Future<AssignedTask> transfer(
    String id, {
    String? targetUserId,
    String? targetKey,
    required String targetDisplayName,
    String? note,
  }) async {
    final body = <String, dynamic>{
      'targetDisplayName': targetDisplayName.trim(),
    };
    if ((targetUserId ?? '').trim().isNotEmpty) {
      body['targetUserId'] = targetUserId!.trim();
    }
    if ((targetKey ?? '').trim().isNotEmpty) {
      body['targetKey'] = targetKey!.trim();
    }
    if ((note ?? '').trim().isNotEmpty) {
      body['note'] = note!.trim();
    }
    final json = await ApiClient.post('$_base/$id/transfer', body: body);
    return AssignedTask.fromJson(JsonRead.map(json));
  }

  /// Self-complete, or confirm when status is بانتظار_تصديق_المفوض.
  static Future<AssignedTask> complete(String id) async {
    final json = await ApiClient.post('$_base/$id/complete');
    return AssignedTask.fromJson(JsonRead.map(json));
  }

  static Future<AssignedTask> confirm(String id) async {
    final json = await ApiClient.post('$_base/$id/confirm');
    return AssignedTask.fromJson(JsonRead.map(json));
  }

  /// Active BM app users for the transfer picker.
  static Future<List<BranchManagerPickOption>> listBranchManagers() async {
    final data = await ApiClient.get('delegated-manager/branch-manager-users');
    final list = data is List ? data : <dynamic>[];
    return list
        .map((e) => BranchManagerPickOption.fromJson(JsonRead.map(e)))
        .where((u) => u.id.isNotEmpty && u.isActive)
        .toList();
  }

  /// Opens an attachment via downloadUrl, or authenticated GET
  /// `.../assigned-tasks/{taskId}/attachments/{fileId}` when the gateway
  /// exposes that route (forward-compatible with director shape).
  static Future<void> openAttachment({
    required String taskId,
    required AssignedTaskAttachment attachment,
  }) async {
    final bytes = await _downloadBytes(taskId: taskId, attachment: attachment);
    final directory = await getTemporaryDirectory();
    final safeName = attachment.fileName.replaceAll(RegExp(r'[\\/:*?"<>|]'), '_');
    final file = File(
      '${directory.path}${Platform.pathSeparator}dm-task-$taskId-$safeName',
    );
    await file.writeAsBytes(bytes, flush: true);
    final opened = await OpenFilex.open(
      file.path,
      type: attachment.contentType,
    );
    if (opened.type != ResultType.done) {
      throw ApiException('تعذر فتح المرفق: ${opened.message}');
    }
  }

  static Future<List<int>> _downloadBytes({
    required String taskId,
    required AssignedTaskAttachment attachment,
  }) async {
    final url = attachment.downloadUrl?.trim();
    final Uri uri;
    if (url != null &&
        url.isNotEmpty &&
        (url.startsWith('http://') || url.startsWith('https://'))) {
      uri = Uri.parse(url);
    } else if (url != null && url.isNotEmpty) {
      uri = ApiClient.uri(url);
    } else {
      uri = ApiClient.uri('$_base/$taskId/attachments/${attachment.id}');
    }

    try {
      final headers = <String, String>{
        'Accept': '*/*',
      };
      final token = Session.token;
      if (token != null && token.isNotEmpty) {
        headers['Authorization'] = 'Bearer $token';
      }
      final response =
          await http.get(uri, headers: headers).timeout(ApiClient.timeout);
      if (response.statusCode < 200 || response.statusCode >= 300) {
        throw ApiException(
          ApiClient.errorMessage(response),
          statusCode: response.statusCode,
        );
      }
      return response.bodyBytes;
    } on ApiException {
      rethrow;
    } catch (_) {
      throw ApiException('تعذر تنزيل المرفق');
    }
  }
}

class BranchManagerPickOption {
  const BranchManagerPickOption({
    required this.id,
    required this.displayName,
    required this.isActive,
    this.userName,
  });

  final String id;
  final String displayName;
  final bool isActive;
  final String? userName;

  factory BranchManagerPickOption.fromJson(Map<String, dynamic> json) =>
      BranchManagerPickOption(
        id: JsonRead.text(json['id']),
        displayName: JsonRead.text(
          json['displayName'],
          fallback: JsonRead.text(json['userName'], fallback: 'مستخدم فرع'),
        ),
        isActive: JsonRead.flag(json['isActive'], fallback: true),
        userName: JsonRead.optionalText(json['userName']),
      );
}
