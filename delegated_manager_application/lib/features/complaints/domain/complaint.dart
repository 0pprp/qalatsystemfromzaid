import 'package:delegated_manager_application/core/theme/app_theme.dart';
import 'package:delegated_manager_application/core/utils/date_format.dart';
import 'package:delegated_manager_application/core/utils/json_read.dart';

/// Row of `GET delegated-manager/complaints`.
class ComplaintSummary {
  const ComplaintSummary({
    required this.id,
    required this.subject,
    required this.status,
    required this.senderDisplayName,
    this.sourceApp,
    this.sourceType,
    this.sourceLabel,
    this.senderRole,
    this.cityValue,
    this.cityName,
    this.createdAtUtc,
    this.readAtUtc,
  });

  final String id;
  final String subject;
  final String status;
  final String senderDisplayName;
  final String? sourceApp;
  final String? sourceType;
  final String? sourceLabel;
  final String? senderRole;
  final String? cityValue;
  final String? cityName;
  final DateTime? createdAtUtc;
  final DateTime? readAtUtc;

  bool get isUnread => status != ComplaintStatuses.read && readAtUtc == null;

  /// Friendly Arabic source for UI (never raw app keys).
  String get friendlySource {
    if ((sourceLabel ?? '').trim().isNotEmpty) return sourceLabel!.trim();
    final app = (sourceApp ?? '').toLowerCase();
    if (app.contains('delegate')) return 'مندوب';
    if (app.contains('follower')) return 'متابع';
    if (app.contains('sales') || app.contains('employee')) return 'موظف مبيعات';
    return 'غير متوفر';
  }

  factory ComplaintSummary.fromJson(Map<String, dynamic> json) =>
      ComplaintSummary(
        id: JsonRead.text(json['id']),
        subject: JsonRead.text(json['subject'], fallback: 'بدون عنوان'),
        status: JsonRead.text(json['status'],
            fallback: ComplaintStatuses.unread),
        senderDisplayName:
            JsonRead.text(json['senderDisplayName'], fallback: 'غير محدد'),
        sourceApp: JsonRead.optionalText(json['sourceApp']),
        sourceType: JsonRead.optionalText(json['sourceType']),
        sourceLabel: JsonRead.optionalText(json['sourceLabel']),
        senderRole: JsonRead.optionalText(json['senderRole']),
        cityValue: JsonRead.optionalText(json['cityValue']),
        cityName: JsonRead.optionalText(json['cityName']),
        createdAtUtc: AppDate.parseUtc(json['createdAtUtc']),
        readAtUtc: AppDate.parseUtc(json['readAtUtc']),
      );
}

/// Row of `GET delegated-manager/complaints/{id}`.
class ComplaintDetail {
  const ComplaintDetail({
    required this.summary,
    required this.body,
    this.senderUserName,
    this.senderUserId,
    this.metadataJson,
  });

  final ComplaintSummary summary;
  final String body;
  final String? senderUserName;
  final int? senderUserId;
  final String? metadataJson;

  String get id => summary.id;

  factory ComplaintDetail.fromJson(Map<String, dynamic> json) =>
      ComplaintDetail(
        summary: ComplaintSummary.fromJson(json),
        body: JsonRead.text(json['body'], fallback: '-'),
        senderUserName: JsonRead.optionalText(json['senderUserName']),
        senderUserId: JsonRead.optionalNumber(json['senderUserId']),
        metadataJson: JsonRead.optionalText(json['metadataJson']),
      );
}
