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
    this.id = '',
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
        id: JsonRead.text(json['id'], fallback: ''),
        actorDisplayName: JsonRead.optionalText(json['actorDisplayName']),
        actorUserName: JsonRead.optionalText(json['actorUserName']),
        actorRole: JsonRead.optionalText(json['actorRole']),
        previousStatus: JsonRead.optionalText(json['previousStatus']),
        newStatus: JsonRead.optionalText(json['newStatus']),
        decisionNote: JsonRead.optionalText(json['decisionNote']),
        createdAtUtc: AppDate.parseUtc(json['createdAtUtc']),
      );
}

class ExceptionReviewClassification {
  const ExceptionReviewClassification({
    required this.type,
    required this.labelArabic,
    required this.matchCount,
    required this.explanationArabic,
  });

  final String type;
  final String labelArabic;
  final int matchCount;
  final String explanationArabic;

  bool get isExisting => type.toLowerCase() == 'existing';
  bool get isNew => !isExisting;

  factory ExceptionReviewClassification.fromJson(Map<String, dynamic> json) =>
      ExceptionReviewClassification(
        type: JsonRead.text(json['type'], fallback: 'New'),
        labelArabic: JsonRead.text(json['labelArabic'], fallback: 'زبون جديد'),
        matchCount: JsonRead.optionalNumber(json['matchCount']) ?? 0,
        explanationArabic: JsonRead.text(json['explanationArabic']),
      );
}

class ExceptionReviewSource {
  const ExceptionReviewSource({
    required this.displayLabel,
    required this.personName,
    required this.branchName,
    this.type,
    this.listName,
  });

  final String? type;
  final String displayLabel;
  final String personName;
  final String? listName;
  final String branchName;

  factory ExceptionReviewSource.fromJson(Map<String, dynamic> json) =>
      ExceptionReviewSource(
        type: JsonRead.optionalText(json['type']),
        displayLabel:
            JsonRead.text(json['displayLabel'], fallback: 'غير متوفر'),
        personName: JsonRead.text(json['personName'], fallback: 'غير متوفر'),
        listName: JsonRead.optionalText(json['listName']),
        branchName: JsonRead.text(json['branchName'], fallback: 'غير متوفر'),
      );
}

class ExceptionReviewCurrentRequest {
  const ExceptionReviewCurrentRequest({
    required this.customerName,
    this.customerPhone,
    this.cityName,
    this.customerProvince,
    this.customerAddress,
    this.notes,
    this.saleTypeOrProduct,
    this.status,
    this.createdAtUtc,
    this.createdByName,
    this.pendingNote,
    this.preparedForSaleNote,
  });

  final String customerName;
  final String? customerPhone;
  final String? cityName;
  final String? customerProvince;
  final String? customerAddress;
  final String? notes;
  final String? saleTypeOrProduct;
  final String? status;
  final DateTime? createdAtUtc;
  final String? createdByName;
  final String? pendingNote;
  final String? preparedForSaleNote;

  factory ExceptionReviewCurrentRequest.fromJson(Map<String, dynamic> json) =>
      ExceptionReviewCurrentRequest(
        customerName:
            JsonRead.text(json['customerName'], fallback: 'غير محدد'),
        customerPhone: JsonRead.optionalText(json['customerPhone']),
        cityName: JsonRead.optionalText(json['cityName']),
        customerProvince: JsonRead.optionalText(json['customerProvince']),
        customerAddress: JsonRead.optionalText(json['customerAddress']),
        notes: JsonRead.optionalText(json['notes']),
        saleTypeOrProduct: JsonRead.optionalText(json['saleTypeOrProduct']),
        status: JsonRead.optionalText(json['status']),
        createdAtUtc: AppDate.parseUtc(json['createdAtUtc']),
        createdByName: JsonRead.optionalText(json['createdByName']),
        pendingNote: JsonRead.optionalText(json['pendingNote']),
        preparedForSaleNote: JsonRead.optionalText(json['preparedForSaleNote']),
      );
}

class ExceptionReviewFinancialSummary {
  const ExceptionReviewFinancialSummary({
    this.totalSales = 0,
    this.totalPaid = 0,
    this.remaining = 0,
    this.currentDebt = 0,
    this.lastSaleDate,
    this.lastPaymentDate,
    this.fullyPaid = false,
    this.legalStatus,
    this.receiptCount = 0,
  });

  final double totalSales;
  final double totalPaid;
  final double remaining;
  final double currentDebt;
  final DateTime? lastSaleDate;
  final DateTime? lastPaymentDate;
  final bool fullyPaid;
  final String? legalStatus;
  final int receiptCount;

  factory ExceptionReviewFinancialSummary.fromJson(Map<String, dynamic> json) =>
      ExceptionReviewFinancialSummary(
        totalSales: (json['totalSales'] as num?)?.toDouble() ?? 0,
        totalPaid: (json['totalPaid'] as num?)?.toDouble() ?? 0,
        remaining: (json['remaining'] as num?)?.toDouble() ?? 0,
        currentDebt: (json['currentDebt'] as num?)?.toDouble() ?? 0,
        lastSaleDate: AppDate.parseUtc(json['lastSaleDate']),
        lastPaymentDate: AppDate.parseUtc(json['lastPaymentDate']),
        fullyPaid: JsonRead.flag(json['fullyPaid']),
        legalStatus: JsonRead.optionalText(json['legalStatus']),
        receiptCount: JsonRead.optionalNumber(json['receiptCount']) ?? 0,
      );
}

class ExceptionReviewPreviousSale {
  const ExceptionReviewPreviousSale({
    this.saleDate,
    this.saleAmount,
    this.accountZero,
    this.branchName,
  });

  final DateTime? saleDate;
  final double? saleAmount;
  final bool? accountZero;
  final String? branchName;

  factory ExceptionReviewPreviousSale.fromJson(Map<String, dynamic> json) =>
      ExceptionReviewPreviousSale(
        saleDate: AppDate.parseUtc(json['saleDate']),
        saleAmount: (json['saleAmount'] as num?)?.toDouble(),
        accountZero: json['accountZero'] as bool?,
        branchName: JsonRead.optionalText(json['branchName']),
      );
}

class ExceptionReviewMatchCustomer {
  const ExceptionReviewMatchCustomer({
    required this.fullName,
    required this.matchReasons,
    this.phone,
    this.cityName,
    this.province,
    this.address,
    this.occupation,
    this.ratingLabel,
    this.ratingScore,
    this.isLegal = false,
    this.financialSummary = const ExceptionReviewFinancialSummary(),
    this.previousSales = const [],
  });

  final String fullName;
  final String? phone;
  final String? cityName;
  final String? province;
  final String? address;
  final String? occupation;
  final List<String> matchReasons;
  final String? ratingLabel;
  final int? ratingScore;
  final bool isLegal;
  final ExceptionReviewFinancialSummary financialSummary;
  final List<ExceptionReviewPreviousSale> previousSales;

  factory ExceptionReviewMatchCustomer.fromJson(Map<String, dynamic> json) =>
      ExceptionReviewMatchCustomer(
        fullName: JsonRead.text(json['fullName'], fallback: 'غير محدد'),
        phone: JsonRead.optionalText(json['phone']),
        cityName: JsonRead.optionalText(json['cityName']),
        province: JsonRead.optionalText(json['province']),
        address: JsonRead.optionalText(json['address']),
        occupation: JsonRead.optionalText(json['occupation']),
        matchReasons: _stringList(json['matchReasons']),
        ratingLabel: JsonRead.optionalText(json['ratingLabel']),
        ratingScore: JsonRead.optionalNumber(json['ratingScore']),
        isLegal: JsonRead.flag(json['isLegal']),
        financialSummary: ExceptionReviewFinancialSummary.fromJson(
            JsonRead.map(json['financialSummary'])),
        previousSales: JsonRead.list(json['previousSales'])
            .map(ExceptionReviewPreviousSale.fromJson)
            .toList(),
      );
}

class ExceptionReview {
  const ExceptionReview({
    required this.customerClassification,
    required this.source,
    this.currentRequest,
    this.matchingCustomers = const [],
  });

  final ExceptionReviewClassification customerClassification;
  final ExceptionReviewCurrentRequest? currentRequest;
  final ExceptionReviewSource source;
  final List<ExceptionReviewMatchCustomer> matchingCustomers;

  factory ExceptionReview.fromJson(Map<String, dynamic> json) =>
      ExceptionReview(
        customerClassification: ExceptionReviewClassification.fromJson(
            JsonRead.map(json['customerClassification'])),
        currentRequest: json['currentRequest'] == null
            ? null
            : ExceptionReviewCurrentRequest.fromJson(
                JsonRead.map(json['currentRequest'])),
        source: ExceptionReviewSource.fromJson(JsonRead.map(json['source'])),
        matchingCustomers: JsonRead.list(json['matchingCustomers'])
            .map(ExceptionReviewMatchCustomer.fromJson)
            .toList(),
      );
}

class ExceptionDetail {
  const ExceptionDetail({
    required this.request,
    required this.audit,
    this.review,
  });

  final ExceptionRequest request;
  final List<ExceptionAuditEntry> audit;
  final ExceptionReview? review;

  factory ExceptionDetail.fromJson(Map<String, dynamic> json) {
    final reviewRaw = json['review'];
    return ExceptionDetail(
      request: ExceptionRequest.fromJson(JsonRead.map(json['request'])),
      audit: JsonRead.list(json['audit'])
          .map(ExceptionAuditEntry.fromJson)
          .toList(),
      review: reviewRaw is Map<String, dynamic>
          ? ExceptionReview.fromJson(reviewRaw)
          : reviewRaw is Map
              ? ExceptionReview.fromJson(Map<String, dynamic>.from(reviewRaw))
              : null,
    );
  }
}

class ExceptionDecision {
  static const String approved = 'Approved';
  static const String rejected = 'Rejected';
}

List<String> _stringList(dynamic value) {
  if (value is! List) return const [];
  return value
      .map((e) => e?.toString().trim() ?? '')
      .where((e) => e.isNotEmpty)
      .toList();
}
