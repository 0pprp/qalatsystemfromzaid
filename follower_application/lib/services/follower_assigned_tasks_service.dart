import 'dart:convert';

import 'package:follower_application/config/app_env.dart';
import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';

class FollowerAssignedTask {
  const FollowerAssignedTask({
    required this.id,
    required this.title,
    required this.description,
    required this.status,
    required this.holderKey,
    required this.priority,
    this.dueDate,
    this.updatedAtUtc,
  });

  final String id;
  final String title;
  final String description;
  final String status;
  final String holderKey;
  final int priority;
  final String? dueDate;
  final DateTime? updatedAtUtc;

  bool get isSent => status == 'مرسلة';
  bool get canAccept => status == 'مرسلة' || status == 'منقولة';
  bool get canReject => canAccept;
  bool get canComplete => status == 'مقبولة';

  factory FollowerAssignedTask.fromJson(Map<String, dynamic> m) {
    DateTime? updated;
    final raw = m['updatedAtUtc'] ?? m['UpdatedAtUtc'];
    if (raw != null) {
      updated = DateTime.tryParse(raw.toString());
    }
    final holder = '${m['currentHolderKey'] ?? m['CurrentHolderKey'] ?? m['assigneeKey'] ?? m['AssigneeKey'] ?? ''}'
        .trim();
    return FollowerAssignedTask(
      id: '${m['id'] ?? m['Id'] ?? ''}',
      title: '${m['title'] ?? m['Title'] ?? ''}'.trim(),
      description: '${m['description'] ?? m['Description'] ?? ''}'.trim(),
      status: '${m['status'] ?? m['Status'] ?? ''}'.trim(),
      holderKey: holder,
      priority: int.tryParse('${m['priority'] ?? m['Priority'] ?? 2}') ?? 2,
      dueDate: (m['dueDate'] ?? m['DueDate'])?.toString(),
      updatedAtUtc: updated,
    );
  }
}

class FollowerAssignedTasksInbox {
  const FollowerAssignedTasksInbox({
    required this.hasUnread,
    required this.tasks,
  });

  final bool hasUnread;
  final List<FollowerAssignedTask> tasks;
}

class FollowerAssignedTasksService {
  static const _seenKey = 'follower_assigned_tasks_seen_utc';

  static Future<String> _holderKey() async {
    final prefs = await SharedPreferences.getInstance();
    final id = (prefs.getString('UserId') ?? prefs.getString('DelegateID') ?? '')
        .trim();
    return id;
  }

  static Future<String> _displayName() async {
    final prefs = await SharedPreferences.getInstance();
    return (prefs.getString('DelegateName') ??
            prefs.getString('UserName') ??
            '')
        .trim();
  }

  static Uri _uri(String path, [Map<String, String>? query]) {
    final base = AppEnv.gatewayApiBase();
    return Uri.parse('$base$path').replace(queryParameters: query);
  }

  static Future<FollowerAssignedTasksInbox> fetchInbox() async {
    final key = await _holderKey();
    if (key.isEmpty) {
      return const FollowerAssignedTasksInbox(hasUnread: false, tasks: []);
    }
    try {
      final res = await http
          .get(_uri('mobile-follower-assigned-tasks/inbox', {'holderKey': key}))
          .timeout(const Duration(seconds: 15));
      if (res.statusCode != 200) {
        return const FollowerAssignedTasksInbox(hasUnread: false, tasks: []);
      }
      final decoded = jsonDecode(res.body);
      if (decoded is! Map) {
        return const FollowerAssignedTasksInbox(hasUnread: false, tasks: []);
      }
      final rawTasks = decoded['tasks'] ?? decoded['Tasks'] ?? [];
      final tasks = <FollowerAssignedTask>[];
      if (rawTasks is List) {
        for (final row in rawTasks) {
          if (row is! Map) continue;
          final t = FollowerAssignedTask.fromJson(
              Map<String, dynamic>.from(row));
          if (t.id.isEmpty) continue;
          tasks.add(t);
        }
      }
      final apiUnread = decoded['hasUnread'] == true ||
          decoded['HasUnread'] == true ||
          tasks.any((t) => t.isSent);
      final localUnread = await _hasLocalUnread(tasks);
      return FollowerAssignedTasksInbox(
        hasUnread: apiUnread || localUnread,
        tasks: tasks,
      );
    } catch (_) {
      return const FollowerAssignedTasksInbox(hasUnread: false, tasks: []);
    }
  }

  static Future<bool> hasUnreadBadge() async {
    final inbox = await fetchInbox();
    return inbox.hasUnread;
  }

  static Future<void> markSeen() async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(_seenKey, DateTime.now().toUtc().toIso8601String());
  }

  static Future<bool> _hasLocalUnread(List<FollowerAssignedTask> tasks) async {
    if (tasks.isEmpty) return false;
    final prefs = await SharedPreferences.getInstance();
    final seenRaw = prefs.getString(_seenKey);
    final seen = seenRaw == null ? null : DateTime.tryParse(seenRaw);
    if (seen == null) return tasks.any((t) => t.isSent);
    return tasks.any((t) =>
        t.isSent &&
        t.updatedAtUtc != null &&
        t.updatedAtUtc!.isAfter(seen));
  }

  static Future<String?> accept(FollowerAssignedTask task) async {
    final display = await _displayName();
    final key = task.holderKey.isNotEmpty ? task.holderKey : await _holderKey();
    try {
      final res = await http
          .post(
            _uri('mobile-follower-assigned-tasks/${task.id}/accept'),
            headers: {'Content-Type': 'application/json'},
            body: jsonEncode({
              'holderKey': key,
              'displayName': display.isEmpty ? key : display,
            }),
          )
          .timeout(const Duration(seconds: 15));
      if (res.statusCode >= 200 && res.statusCode < 300) return null;
      return _err(res) ?? 'تعذر قبول المهمة';
    } catch (_) {
      return 'تعذر الاتصال بالسيرفر';
    }
  }

  static Future<String?> reject(FollowerAssignedTask task, String note) async {
    final display = await _displayName();
    final key = task.holderKey.isNotEmpty ? task.holderKey : await _holderKey();
    try {
      final res = await http
          .post(
            _uri(
              'mobile-follower-assigned-tasks/${task.id}/reject',
              {
                'holderKey': key,
                'displayName': display.isEmpty ? key : display,
              },
            ),
            headers: {'Content-Type': 'application/json'},
            body: jsonEncode({'note': note}),
          )
          .timeout(const Duration(seconds: 15));
      if (res.statusCode >= 200 && res.statusCode < 300) return null;
      return _err(res) ?? 'تعذر رفض المهمة';
    } catch (_) {
      return 'تعذر الاتصال بالسيرفر';
    }
  }

  static Future<String?> complete(FollowerAssignedTask task) async {
    final display = await _displayName();
    final key = task.holderKey.isNotEmpty ? task.holderKey : await _holderKey();
    try {
      final res = await http
          .post(
            _uri('mobile-follower-assigned-tasks/${task.id}/complete'),
            headers: {'Content-Type': 'application/json'},
            body: jsonEncode({
              'holderKey': key,
              'displayName': display.isEmpty ? key : display,
            }),
          )
          .timeout(const Duration(seconds: 15));
      if (res.statusCode >= 200 && res.statusCode < 300) return null;
      return _err(res) ?? 'تعذر إنجاز المهمة';
    } catch (_) {
      return 'تعذر الاتصال بالسيرفر';
    }
  }

  static String? _err(http.Response res) {
    try {
      final decoded = jsonDecode(res.body);
      if (decoded is Map) {
        final msg = decoded['message'] ?? decoded['Message'];
        if (msg != null && '$msg'.trim().isNotEmpty) return '$msg'.trim();
      }
    } catch (_) {}
    return null;
  }
}
