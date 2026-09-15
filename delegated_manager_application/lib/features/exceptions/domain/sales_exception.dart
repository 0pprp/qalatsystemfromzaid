import 'package:delegated_manager_application/core/utils/date_format.dart';
import 'package:delegated_manager_application/core/utils/json_read.dart';

/// Row of `GET delegated-manager/exceptions`.
class ExceptionSummary {
  const ExceptionSummary({
    required this.id,
    required this.status,
    required this.customerName,
    this.cityValue,
    this.cityName,
    this.customerId,
    this.salesRequestId,
    this.requestingManagerDisplayName,
    this.targetApproverType,
    this.requestedAtUtc,
    this.decidedAtUtc,
  });

  final String id;
  final String status;
  final String customerName;
  final String? cityValue;
  final String? cityName;
  final int? customerId;
  final int? salesRequestId;
  final String? requestingManagerDisplayName;
  final String? targetApproverType;
  final DateTime? requestedAtUtc;
  final DateTime? decidedAtUtc;

  factory ExceptionSummary.fromJson(Map<String, dynamic> json) =>
      ExceptionSummary(
        id: JsonRead.text(json['id']),
        status: JsonRead.text(json['status']),
        customerName:
            JsonRead.text(json['customerName'], fallback: 'غير محدد'),
        cityValue: JsonRead.optionalText(json['cityValue']),
        cityName: JsonRead.optionalText(json['cityName']),
        customerId: JsonRead.optionalNumber(json['customerId']),
        salesRequestId: JsonRead.optionalNumber(json['salesRequestId']),
        requestingManagerDisplayName:
            JsonRead.optionalText(json['requestingManagerDisplayName']),
        targetApproverType: JsonRead.optionalText(json['targetApproverType']),
        requestedAtUtc: AppDate.parseUtc(json['requestedAtUtc']),
        decidedAtUtc: AppDate.parseUtc(json['decidedAtUtc']),
      );
}

/// `request` object of `GET delegated-manager/exceptions/{id}` and the body of
/// the decision response.
class ExceptionRequest {
  const ExceptionRequest({
    required this.summary,
    this.customerPhone,
    this.requestingManagerUserName,
    this.reason,
    this.decisionMakerUserName,
    this.decisionMakerDisplayName,
    this.decisionNote,
    this.branchCustomerNotePosted = false,
  });

  final ExceptionSummary summary;
  final String? customerPhone;
  final String? requestingManagerUserName;
  final String? reason;
  final String? decisionMakerUserName;
  final String? decisionMakerDisplayName;
  final String? decisionNote;
  final bool branchCustomerNotePosted;

  String get id => summary.id;
  String get status => summary.status;

  factory ExceptionRequest.fromJson(Map<String, dynamic> json) =>
      ExceptionRequest(
        summary: ExceptionSummary.fromJson(json),
        customerPhone: JsonRead.optionalText(json['customerPhone']),
        requestingManagerUserName:
            JsonRead.optionalText(json['requestingManagerUserName']),
        reason: JsonRead.optionalText(json['reason']),
        decisionMakerUserName:
            JsonRead.optionalText(json['decisionMakerUserName']),
        decisionMakerDisplayName:
            JsonRead.optionalText(json['decisionMakerDisplayName']),
        decisionNote: JsonRead.optionalText(json['decisionNote']),
        branchCustomerNotePosted:
            JsonRead.flag(json['branchCustomerNotePosted']),
      );
}

class ExceptionAuditEntry {
  const ExceptionAuditEntry({
    required this.id,
    this.actorDisplayName,
    this.actorUserName,
    this.actorRole,
    this.previousStatus,
    this.newStatus,
    this.decisionNote,
    this.createdAtUtc,
  });

  final String id;
  final String? actorDisplayName;
  final String? actorUserName;
  final String? actorRole;
  final String? previousStatus;
  final String? newStatus;
  final String? decisionNote;
  final DateTime? createdAtUtc;

  factory ExceptionAuditEntry.fromJson(Map<String, dynamic> json) =>
      ExceptionAuditEntry(
        id: JsonRead.text(json['id']),
        actorDisplayName: JsonRead.optionalText(json['actorDisplayName']),
        actorUserName: JsonRead.optionalText(json['actorUserName']),
        actorRole: JsonRead.optionalText(json['actorRole']),
        previousStatus: JsonRead.optionalText(json['previousStatus']),
        newStatus: JsonRead.optionalText(json['newStatus']),
        decisionNote: JsonRead.optionalText(json['decisionNote']),
        createdAtUtc: AppDate.parseUtc(json['createdAtUtc']),
      );
}

class ExceptionDetail {
  const ExceptionDetail({required this.request, required this.audit});

  final ExceptionRequest request;
  final List<ExceptionAuditEntry> audit;

  factory ExceptionDetail.fromJson(Map<String, dynamic> json) =>
      ExceptionDetail(
        request: ExceptionRequest.fromJson(JsonRead.map(json['request'])),
        audit: JsonRead.list(json['audit'])
            .map(ExceptionAuditEntry.fromJson)
            .toList(),
      );
}

/// Body of `PUT delegated-manager/exceptions/{id}/decision`.
class ExceptionDecision {
  static const String approved = 'Approved';
  static const String rejected = 'Rejected';
}
