/// Hides bundles that were sent and are still in transit, and bundles fully approved.
bool archiveShownWhenHidingSentAndApproved(String phase) {
  return phase != 'InTransit' && phase != 'Approved';
}

String archivePhaseLabel(String phase) {
  switch (phase) {
    case 'Open':
      return 'الحزمة الحالية';
    case 'Ready':
      return 'جاهزة للإرسال';
    case 'InTransit':
      return 'مُسلّمة للجنة / الأرشيف';
    case 'NeedsFix':
      return 'غير مكتملة';
    case 'Approved':
      return 'معتمدة';
    default:
      return phase;
  }
}

String archiveItemStatusLabel(String status) {
  switch (status) {
    case 'Approved':
      return 'معتمد';
    case 'Refused':
      return 'فيه مشكلة';
    case 'Pending':
      return 'لم يُرسل';
    case 'ToCommittee':
      return 'بانتظار استلام اللجنة';
    case 'WithCommittee':
      return 'استلمته اللجنة';
    case 'ArchiveRefused':
      return 'عند اللجنة';
    case 'ToArchive':
      return 'عند مسؤول الأرشيف';
    default:
      return status;
  }
}

String formatArchiveDate(String? raw) {
  if (raw == null || raw.trim().isEmpty) return '';
  final date = raw.length >= 10 ? raw.substring(0, 10) : raw;
  final parts = date.split('-');
  if (parts.length != 3) return date;
  return '${parts[0]}/${parts[1]}/${parts[2]}';
}

class ArchiveRecipient {
  ArchiveRecipient({required this.name, required this.role});

  final String name;
  final String role;

  String get label => role == 'مسؤول لجنة المراقبة' ? '$name — مسؤول اللجنة' : '$name — مساعد مسؤول اللجنة';

  factory ArchiveRecipient.fromJson(Map<String, dynamic> json) => ArchiveRecipient(
        name: '${json['name'] ?? json['Name'] ?? ''}'.trim(),
        role: '${json['role'] ?? json['Role'] ?? ''}'.trim(),
      );
}

class ArchiveItem {
  ArchiveItem({
    required this.archiveId,
    required this.saleId,
    required this.customerName,
    required this.saleDate,
    required this.salePrice,
    required this.itemName,
    required this.listName,
    required this.status,
    this.refusalNote,
  });

  final int archiveId;
  final int saleId;
  final String customerName;
  final String saleDate;
  final num salePrice;
  final String itemName;
  final String listName;
  final String status;
  final String? refusalNote;

  bool get hasProblem => status == 'Refused';

  factory ArchiveItem.fromJson(Map<String, dynamic> json) => ArchiveItem(
        archiveId: int.tryParse('${json['archiveId'] ?? json['ArchiveId'] ?? 0}') ?? 0,
        saleId: int.tryParse('${json['saleId'] ?? json['SaleId'] ?? 0}') ?? 0,
        customerName: '${json['customerName'] ?? json['CustomerName'] ?? ''}',
        saleDate: '${json['saleDate'] ?? json['SaleDate'] ?? ''}',
        salePrice: num.tryParse('${json['salePrice'] ?? json['SalePrice'] ?? 0}') ?? 0,
        itemName: '${json['itemName'] ?? json['ItemName'] ?? ''}',
        listName: '${json['listName'] ?? json['ListName'] ?? ''}',
        status: '${json['status'] ?? json['Status'] ?? 'Pending'}',
        refusalNote: json['refusalNote']?.toString() ?? json['RefusalNote']?.toString(),
      );
}

class ArchiveBundle {
  ArchiveBundle({
    required this.bundleId,
    required this.bundleNumber,
    required this.employeeId,
    required this.employeeName,
    required this.periodStart,
    required this.periodEnd,
    required this.phase,
    required this.canSend,
    required this.itemCount,
    required this.approvedCount,
    required this.refusedCount,
    required this.pendingCount,
    this.intermediateName,
    this.intermediatePhone,
    this.recipientName,
    this.note,
    this.sentAt,
    this.hasPhoto = false,
    this.photoCount = 0,
    this.photoIds = const [],
    this.items = const [],
  });

  final int bundleId;
  final int bundleNumber;
  final int employeeId;
  final String employeeName;
  final String periodStart;
  final String periodEnd;
  final String phase;
  final bool canSend;
  final int itemCount;
  final int approvedCount;
  final int refusedCount;
  final int pendingCount;
  final String? intermediateName;
  final String? intermediatePhone;
  final String? recipientName;
  final String? note;
  final String? sentAt;
  final bool hasPhoto;
  final int photoCount;
  final List<int> photoIds;
  final List<ArchiveItem> items;

  String get periodLabel =>
      '${formatArchiveDate(periodStart)} — ${formatArchiveDate(periodEnd)}';

  factory ArchiveBundle.fromJson(Map<String, dynamic> json) => ArchiveBundle(
        bundleId: int.tryParse('${json['bundleId'] ?? json['BundleId'] ?? 0}') ?? 0,
        bundleNumber: int.tryParse('${json['bundleNumber'] ?? json['BundleNumber'] ?? 0}') ?? 0,
        employeeId: int.tryParse('${json['employeeId'] ?? json['EmployeeId'] ?? 0}') ?? 0,
        employeeName: '${json['employeeName'] ?? json['EmployeeName'] ?? ''}',
        periodStart: '${json['periodStart'] ?? json['PeriodStart'] ?? ''}',
        periodEnd: '${json['periodEnd'] ?? json['PeriodEnd'] ?? ''}',
        phase: '${json['phase'] ?? json['Phase'] ?? ''}',
        canSend: json['canSend'] == true || json['CanSend'] == true,
        itemCount: int.tryParse('${json['itemCount'] ?? json['ItemCount'] ?? 0}') ?? 0,
        approvedCount: int.tryParse('${json['approvedCount'] ?? json['ApprovedCount'] ?? 0}') ?? 0,
        refusedCount: int.tryParse('${json['refusedCount'] ?? json['RefusedCount'] ?? 0}') ?? 0,
        pendingCount: int.tryParse('${json['pendingCount'] ?? json['PendingCount'] ?? 0}') ?? 0,
        intermediateName: json['intermediateName']?.toString() ?? json['IntermediateName']?.toString(),
        intermediatePhone: json['intermediatePhone']?.toString() ?? json['IntermediatePhone']?.toString(),
        recipientName: json['recipientName']?.toString() ?? json['RecipientName']?.toString(),
        note: json['note']?.toString() ?? json['Note']?.toString(),
        sentAt: json['sentAt']?.toString() ?? json['SentAt']?.toString(),
        hasPhoto: json['hasPhoto'] == true || json['HasPhoto'] == true,
        photoCount: int.tryParse('${json['photoCount'] ?? json['PhotoCount'] ?? 0}') ?? 0,
        photoIds: [
          for (final id in (json['photoIds'] ?? json['PhotoIds'] ?? const []))
            if (int.tryParse('$id') != null) int.parse('$id'),
        ],
        items: [
          for (final e in (json['items'] ?? json['Items'] ?? const []))
            if (e is Map) ArchiveItem.fromJson(Map<String, dynamic>.from(e)),
        ],
      );
}
