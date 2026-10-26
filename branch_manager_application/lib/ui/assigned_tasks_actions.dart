/// Pure helpers for BM assigned-tasks inbox (no transfer).
class BmAssignedTaskActions {
  const BmAssignedTaskActions({
    required this.canAccept,
    required this.canReject,
    required this.canComplete,
    this.canTransfer = false,
  });

  final bool canAccept;
  final bool canReject;
  final bool canComplete;

  /// Always false for branch manager — transfer is DM-only.
  final bool canTransfer;

  static BmAssignedTaskActions forStatus(String? status) {
    switch ((status ?? '').trim()) {
      case 'مرسلة':
      case 'منقولة':
        return const BmAssignedTaskActions(
          canAccept: true,
          canReject: true,
          canComplete: false,
        );
      case 'مقبولة':
        return const BmAssignedTaskActions(
          canAccept: false,
          canReject: false,
          canComplete: true,
        );
      default:
        return const BmAssignedTaskActions(
          canAccept: false,
          canReject: false,
          canComplete: false,
        );
    }
  }

  /// Priority: عالية(1) → متوسطة(2) → ضعيفة(3), then sooner due date.
  static List<BmAssignedTask> sortInbox(List<BmAssignedTask> rows) {
    final copy = List<BmAssignedTask>.of(rows);
    copy.sort((a, b) {
      final p = a.priority.compareTo(b.priority);
      if (p != 0) return p;
      final ad = a.dueDate;
      final bd = b.dueDate;
      if (ad == null && bd == null) return 0;
      if (ad == null) return 1;
      if (bd == null) return -1;
      return ad.compareTo(bd);
    });
    return copy;
  }
}

class BmAssignedTaskAttachment {
  const BmAssignedTaskAttachment({
    required this.id,
    required this.fileName,
    this.contentType,
    this.sizeBytes,
  });

  final String id;
  final String fileName;
  final String? contentType;
  final int? sizeBytes;

  factory BmAssignedTaskAttachment.fromJson(Map<String, dynamic> m) =>
      BmAssignedTaskAttachment(
        id: '${m['id'] ?? m['Id'] ?? ''}',
        fileName: '${m['fileName'] ?? m['FileName'] ?? 'مرفق'}'.trim(),
        contentType: (m['contentType'] ?? m['ContentType'])?.toString(),
        sizeBytes: m['sizeBytes'] is int
            ? m['sizeBytes'] as int
            : int.tryParse('${m['sizeBytes'] ?? m['SizeBytes'] ?? ''}'),
      );
}

class BmAssignedTask {
  const BmAssignedTask({
    required this.id,
    required this.title,
    this.description = '',
    required this.priority,
    required this.status,
    this.dueDate,
    this.dueAtUtc,
    this.rejectNote,
    this.assigneeDisplayName,
    this.transferredAtUtc,
    this.branchCompletedAtUtc,
    this.delegatedCompletedAtUtc,
    this.delegatedConfirmedAtUtc,
    this.attachments = const [],
  });

  final String id;
  final String title;
  final String description;
  final DateTime? dueDate;
  final DateTime? dueAtUtc;
  final int priority;
  final String status;
  final String? rejectNote;
  final String? assigneeDisplayName;
  final DateTime? transferredAtUtc;
  final DateTime? branchCompletedAtUtc;
  final DateTime? delegatedCompletedAtUtc;
  final DateTime? delegatedConfirmedAtUtc;
  final List<BmAssignedTaskAttachment> attachments;

  bool get isOverdue {
    if (status == 'منجزة') return false;
    if (dueAtUtc != null) {
      return dueAtUtc!.toUtc().isBefore(DateTime.now().toUtc());
    }
    if (dueDate == null) return false;
    final today = DateTime.now();
    final due = DateTime(dueDate!.year, dueDate!.month, dueDate!.day);
    final now = DateTime(today.year, today.month, today.day);
    return due.isBefore(now);
  }

  String get priorityLabel {
    switch (priority) {
      case 1:
        return 'عالية';
      case 2:
        return 'متوسطة';
      case 3:
        return 'ضعيفة';
      default:
        return '$priority';
    }
  }

  String get statusLabel {
    if (status == 'بانتظار_تصديق_المفوض') return 'بانتظار تصديق المفوض';
    return status.isEmpty ? '-' : status;
  }

  String get dueDisplay {
    if (dueAtUtc != null) return _iraqDateTime12h(dueAtUtc!);
    if (dueDate != null) {
      return '${dueDate!.year}/${_two(dueDate!.month)}/${_two(dueDate!.day)}';
    }
    return '-';
  }

  BmAssignedTaskActions get actions => BmAssignedTaskActions.forStatus(status);

  factory BmAssignedTask.fromJson(Map<String, dynamic> m) {
    final dueRaw = m['dueDate'] ?? m['DueDate'];
    DateTime? due;
    if (dueRaw != null) {
      due = DateTime.tryParse('$dueRaw');
    }
    final p = m['priority'] ?? m['Priority'];
    int priority = 2;
    if (p is int) {
      priority = p;
    } else if (p != null) {
      priority = int.tryParse('$p') ?? 2;
    }
    final attsRaw = m['attachments'] ?? m['Attachments'];
    final atts = <BmAssignedTaskAttachment>[];
    if (attsRaw is List) {
      for (final row in attsRaw) {
        if (row is Map) {
          final a =
              BmAssignedTaskAttachment.fromJson(Map<String, dynamic>.from(row));
          if (a.id.isNotEmpty) atts.add(a);
        }
      }
    }
    return BmAssignedTask(
      id: '${m['id'] ?? m['Id'] ?? ''}',
      title: '${m['title'] ?? m['Title'] ?? ''}'.trim(),
      description:
          '${m['description'] ?? m['Description'] ?? m['body'] ?? ''}'.trim(),
      dueDate: due,
      dueAtUtc: _parseUtc(m['dueAtUtc'] ?? m['DueAtUtc']),
      priority: priority,
      status: '${m['status'] ?? m['Status'] ?? ''}'.trim(),
      rejectNote: (m['rejectNote'] ?? m['RejectNote'])?.toString(),
      assigneeDisplayName:
          (m['assigneeDisplayName'] ?? m['AssigneeDisplayName'])?.toString(),
      transferredAtUtc:
          _parseUtc(m['transferredAtUtc'] ?? m['TransferredAtUtc']),
      branchCompletedAtUtc:
          _parseUtc(m['branchCompletedAtUtc'] ?? m['BranchCompletedAtUtc']),
      delegatedCompletedAtUtc: _parseUtc(
          m['delegatedCompletedAtUtc'] ?? m['DelegatedCompletedAtUtc']),
      delegatedConfirmedAtUtc: _parseUtc(
          m['delegatedConfirmedAtUtc'] ?? m['DelegatedConfirmedAtUtc']),
      attachments: atts,
    );
  }

  static DateTime? _parseUtc(dynamic value) {
    if (value == null) return null;
    final raw = value.toString().trim();
    if (raw.isEmpty) return null;
    return DateTime.tryParse(raw.endsWith('Z') ? raw : '${raw}Z');
  }

  static String _two(int n) => n.toString().padLeft(2, '0');

  static String _iraqDateTime12h(DateTime value) {
    final utc = value.isUtc ? value : value.toUtc();
    final iraq = utc.add(const Duration(hours: 3));
    final h24 = iraq.hour;
    final period = h24 >= 12 ? 'مساء' : 'صباح';
    var h12 = h24 % 12;
    if (h12 == 0) h12 = 12;
    return '${iraq.year}/${_two(iraq.month)}/${_two(iraq.day)} '
        '${_two(h12)}:${_two(iraq.minute)} $period';
  }

  static String stampDisplay(DateTime? value) {
    if (value == null) return '-';
    return _iraqDateTime12h(value);
  }
}
