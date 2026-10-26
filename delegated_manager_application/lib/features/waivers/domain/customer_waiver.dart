import 'package:delegated_manager_application/core/utils/date_format.dart';
import 'package:delegated_manager_application/core/utils/json_read.dart';
import 'package:delegated_manager_application/features/exceptions/domain/sales_exception.dart';

class WaiverStatuses {
  static const pending = 'Pending';
  static const awaitingAccountant = 'AwaitingAccountant';
  static const approved = 'Approved';
  static const rejected = 'Rejected';

  static String arabic(String? status) {
    switch ((status ?? '').trim()) {
      case pending:
        return 'قيد الانتظار';
      case awaitingAccountant:
        return 'بانتظار المحاسب';
      case approved:
        return 'موافق عليه';
      case rejected:
        return 'مرفوض';
      default:
        return status ?? '-';
    }
  }
}

class WaiverSummary {
  const WaiverSummary({
    required this.id,
    required this.status,
    required this.customerName,
    this.cityValue,
    this.cityName,
    this.customerId,
    this.branchManagerDisplayName,
    this.note,
    this.requestedAtUtc,
    this.decidedAtUtc,
    this.approvedAmount,
  });

  final String id;
  final String status;
  final String customerName;
  final String? cityValue;
  final String? cityName;
  final int? customerId;
  final String? branchManagerDisplayName;
  final String? note;
  final DateTime? requestedAtUtc;
  final DateTime? decidedAtUtc;
  final double? approvedAmount;

  factory WaiverSummary.fromJson(Map<String, dynamic> json) => WaiverSummary(
        id: JsonRead.text(json['id']),
        status: JsonRead.text(json['status']),
        customerName:
            JsonRead.text(json['customerName'], fallback: 'غير محدد'),
        cityValue: JsonRead.optionalText(json['cityValue']),
        cityName: JsonRead.optionalText(json['cityName']),
        customerId: JsonRead.optionalNumber(json['customerId']),
        branchManagerDisplayName:
            JsonRead.optionalText(json['branchManagerDisplayName']),
        note: JsonRead.optionalText(json['note']),
        requestedAtUtc: AppDate.parseUtc(json['requestedAtUtc']),
        decidedAtUtc: AppDate.parseUtc(json['decidedAtUtc']),
        approvedAmount: (json['approvedAmount'] as num?)?.toDouble(),
      );
}

class WaiverRequest {
  const WaiverRequest({
    required this.summary,
    this.customerPhone,
    this.branchManagerUserName,
    this.decisionMakerDisplayName,
    this.decisionNote,
    this.paymentPosted = false,
  });

  final WaiverSummary summary;
  final String? customerPhone;
  final String? branchManagerUserName;
  final String? decisionMakerDisplayName;
  final String? decisionNote;
  final bool paymentPosted;

  String get id => summary.id;
  String get status => summary.status;
  String? get note => summary.note;

  factory WaiverRequest.fromJson(Map<String, dynamic> json) => WaiverRequest(
        summary: WaiverSummary.fromJson(json),
        customerPhone: JsonRead.optionalText(json['customerPhone']),
        branchManagerUserName:
            JsonRead.optionalText(json['branchManagerUserName']),
        decisionMakerDisplayName:
            JsonRead.optionalText(json['decisionMakerDisplayName']),
        decisionNote: JsonRead.optionalText(json['decisionNote']),
        paymentPosted: JsonRead.flag(json['paymentPosted']),
      );
}

class WaiverDetail {
  const WaiverDetail({
    required this.request,
    this.review,
    this.reviewError,
  });

  final WaiverRequest request;
  final ExceptionReview? review;
  final String? reviewError;

  factory WaiverDetail.fromJson(Map<String, dynamic> json) {
    final reviewRaw = json['review'];
    return WaiverDetail(
      request: WaiverRequest.fromJson(JsonRead.map(json['request'])),
      review: reviewRaw is Map<String, dynamic>
          ? ExceptionReview.fromJson(reviewRaw)
          : reviewRaw is Map
              ? ExceptionReview.fromJson(Map<String, dynamic>.from(reviewRaw))
              : null,
      reviewError: JsonRead.optionalText(json['reviewError']),
    );
  }
}
