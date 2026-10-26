import 'package:delegated_manager_application/core/utils/date_format.dart';
import 'package:delegated_manager_application/core/utils/json_read.dart';

/// Matches `DirectorAssignedTaskStatus` on the sales gateway.
class AssignedTaskStatus {
  static const sent = 'مرسلة';
  static const transferred = 'منقولة';
  static const accepted = 'مقبولة';
  static const returned = 'معادة';
  static const awaitingDelegatedConfirm = 'بانتظار_تصديق_المفوض';
  static const completed = 'منجزة';

  static String display(String? status) {
    switch ((status ?? '').trim()) {
      case awaitingDelegatedConfirm:
        return 'بانتظار تصديق المفوض';
      case sent:
      case transferred:
      case accepted:
      case returned:
      case completed:
        return status!.trim();
      default:
        return status?.trim().isNotEmpty == true ? status!.trim() : '-';
    }
  }
}

/// Matches `DirectorAssignedTaskPriority` on the sales gateway (1=عالية).
class AssignedTaskPriority {
  static const high = 1;
  static const medium = 2;
  static const low = 3;

  static String label(int priority) {
    switch (priority) {
      case high:
        return 'عالية';
      case medium:
        return 'متوسطة';
      case low:
        return 'ضعيفة';
      default:
        return 'متوسطة';
    }
  }
}

class AssignedTaskActions {
  const AssignedTaskActions({
    required this.canAccept,
    required this.canReject,
    required this.canTransfer,
    required this.canComplete,
    required this.canConfirm,
  });

  final bool canAccept;
  final bool canReject;
  final bool canTransfer;
  final bool canComplete;
  final bool canConfirm;

  bool get hasAny =>
      canAccept || canReject || canTransfer || canComplete || canConfirm;

  static AssignedTaskActions forStatus(String? status) {
    switch ((status ?? '').trim()) {
      case AssignedTaskStatus.sent:
        return const AssignedTaskActions(
          canAccept: true,
          canReject: true,
          canTransfer: false,
          canComplete: false,
          canConfirm: false,
        );
      case AssignedTaskStatus.transferred:
        // Transferred to BM — DM keeps visibility but no actions until confirm.
        return const AssignedTaskActions(
          canAccept: false,
          canReject: false,
          canTransfer: false,
          canComplete: false,
          canConfirm: false,
        );
      case AssignedTaskStatus.accepted:
        return const AssignedTaskActions(
          canAccept: false,
          canReject: false,
          canTransfer: true,
          canComplete: true,
          canConfirm: false,
        );
      case AssignedTaskStatus.awaitingDelegatedConfirm:
        return const AssignedTaskActions(
          canAccept: false,
          canReject: false,
          canTransfer: false,
          canComplete: false,
          canConfirm: true,
        );
      default:
        return const AssignedTaskActions(
          canAccept: false,
          canReject: false,
          canTransfer: false,
          canComplete: false,
          canConfirm: false,
        );
    }
  }
}

class AssignedTaskAttachment {
  const AssignedTaskAttachment({
    required this.id,
    required this.fileName,
    this.taskId,
    this.contentType,
    this.sizeBytes,
    this.downloadUrl,
  });

  final String id;
  final String fileName;
  final String? taskId;
  final String? contentType;
  final int? sizeBytes;

  /// Absolute or relative URL when the API provides one.
  final String? downloadUrl;

  factory AssignedTaskAttachment.fromJson(Map<String, dynamic> json) =>
      AssignedTaskAttachment(
        id: JsonRead.text(json['id']),
        fileName: JsonRead.text(json['fileName'], fallback: 'مرفق'),
        taskId: JsonRead.optionalText(json['taskId']),
        contentType: JsonRead.optionalText(json['contentType']),
        sizeBytes: JsonRead.optionalNumber(json['sizeBytes']),
        downloadUrl: JsonRead.optionalText(json['downloadUrl']) ??
            JsonRead.optionalText(json['url']),
      );
}

class AssignedTask {
  const AssignedTask({
    required this.id,
    required this.title,
    this.description = '',
    required this.dueDate,
    this.dueAtUtc,
    required this.priority,
    required this.status,
    this.assigneeKind,
    this.assigneeKey,
    this.assigneeDisplayName,
    this.currentHolderKind,
    this.currentHolderKey,
    this.rejectNote,
    this.createdAtUtc,
    this.updatedAtUtc,
    this.transferredAtUtc,
    this.branchCompletedAtUtc,
    this.delegatedCompletedAtUtc,
    this.delegatedConfirmedAtUtc,
    this.attachments = const [],
  });

  final String id;
  final String title;
  final String description;
  final DateTime dueDate;
  final DateTime? dueAtUtc;
  final int priority;
  final String status;
  final String? assigneeKind;
  final String? assigneeKey;
  final String? assigneeDisplayName;
  final String? currentHolderKind;
  final String? currentHolderKey;
  final String? rejectNote;
  final DateTime? createdAtUtc;
  final DateTime? updatedAtUtc;
  final DateTime? transferredAtUtc;
  final DateTime? branchCompletedAtUtc;
  final DateTime? delegatedCompletedAtUtc;
  final DateTime? delegatedConfirmedAtUtc;
  final List<AssignedTaskAttachment> attachments;

  String get priorityLabel => AssignedTaskPriority.label(priority);
  String get statusLabel => AssignedTaskStatus.display(status);
  AssignedTaskActions get actions => AssignedTaskActions.forStatus(status);

  String get dueDisplay {
    if (dueAtUtc != null) {
      return AppDate.iraqDateTime12h(dueAtUtc);
    }
    return AppDate.day(dueDate);
  }

  bool isOverdue([DateTime? now]) {
    if (status == AssignedTaskStatus.completed) return false;
    final ref = now ?? DateTime.now();
    if (dueAtUtc != null) {
      return dueAtUtc!.toUtc().isBefore(ref.toUtc());
    }
    final day = DateTime(ref.year, ref.month, ref.day);
    final due = DateTime(dueDate.year, dueDate.month, dueDate.day);
    return due.isBefore(day);
  }

  factory AssignedTask.fromJson(Map<String, dynamic> json) {
    final attachmentsRaw = json['attachments'];
    return AssignedTask(
      id: JsonRead.text(json['id']),
      title: JsonRead.text(json['title'], fallback: 'مهمة'),
      description: JsonRead.text(
        json['description'] ?? json['Description'],
      ),
      dueDate: _parseDueDate(json['dueDate']),
      dueAtUtc: AppDate.parseUtc(json['dueAtUtc'] ?? json['DueAtUtc']),
      priority: JsonRead.number(json['priority'],
          fallback: AssignedTaskPriority.medium),
      status: JsonRead.text(json['status']),
      assigneeKind: JsonRead.optionalText(json['assigneeKind']),
      assigneeKey: JsonRead.optionalText(json['assigneeKey']),
      assigneeDisplayName: JsonRead.optionalText(json['assigneeDisplayName']),
      currentHolderKind: JsonRead.optionalText(json['currentHolderKind']),
      currentHolderKey: JsonRead.optionalText(json['currentHolderKey']),
      rejectNote: JsonRead.optionalText(json['rejectNote']),
      createdAtUtc: AppDate.parseUtc(json['createdAtUtc']),
      updatedAtUtc: AppDate.parseUtc(json['updatedAtUtc']),
      transferredAtUtc:
          AppDate.parseUtc(json['transferredAtUtc'] ?? json['TransferredAtUtc']),
      branchCompletedAtUtc: AppDate.parseUtc(json['branchCompletedAtUtc']),
      delegatedCompletedAtUtc:
          AppDate.parseUtc(json['delegatedCompletedAtUtc']),
      delegatedConfirmedAtUtc:
          AppDate.parseUtc(json['delegatedConfirmedAtUtc']),
      attachments: JsonRead.list(attachmentsRaw)
          .map(AssignedTaskAttachment.fromJson)
          .where((a) => a.id.isNotEmpty)
          .toList(),
    );
  }

  static DateTime _parseDueDate(dynamic value) {
    if (value is DateTime) {
      return DateTime(value.year, value.month, value.day);
    }
    final raw = value?.toString().trim() ?? '';
    if (raw.isEmpty) {
      final now = DateTime.now();
      return DateTime(now.year, now.month, now.day);
    }
    // Gateway DateOnly typically serializes as "yyyy-MM-dd".
    final dateOnly = RegExp(r'^(\d{4})-(\d{2})-(\d{2})').firstMatch(raw);
    if (dateOnly != null) {
      return DateTime(
        int.parse(dateOnly.group(1)!),
        int.parse(dateOnly.group(2)!),
        int.parse(dateOnly.group(3)!),
      );
    }
    final parsed = DateTime.tryParse(raw);
    if (parsed != null) {
      return DateTime(parsed.year, parsed.month, parsed.day);
    }
    final now = DateTime.now();
    return DateTime(now.year, now.month, now.day);
  }
}

/// Priority ascending (1 عالية first), then due date ascending.
List<AssignedTask> sortAssignedTasks(Iterable<AssignedTask> tasks) {
  final list = List<AssignedTask>.from(tasks);
  list.sort((a, b) {
    final byPriority = a.priority.compareTo(b.priority);
    if (byPriority != 0) return byPriority;
    final byDue = a.dueDate.compareTo(b.dueDate);
    if (byDue != 0) return byDue;
    return a.id.compareTo(b.id);
  });
  return list;
}
