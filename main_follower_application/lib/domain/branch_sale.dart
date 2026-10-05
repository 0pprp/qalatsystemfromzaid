class BranchSale {
  const BranchSale({
    required this.saleId,
    required this.customerName,
    this.customerId,
    this.phone,
    this.amount,
    this.employeeName,
    this.createdAt,
    this.status,
  });

  final int saleId;
  final int? customerId;
  final String customerName;
  final String? phone;
  final num? amount;
  final String? employeeName;
  final String? createdAt;
  final String? status;

  factory BranchSale.fromJson(Map<String, dynamic> json) {
    int asInt(dynamic v) {
      if (v is int) return v;
      return int.tryParse(v?.toString() ?? '') ?? 0;
    }

    int? asIntOrNull(dynamic v) {
      if (v == null) return null;
      if (v is int) return v;
      return int.tryParse(v.toString());
    }

    num? asNum(dynamic v) {
      if (v is num) return v;
      return num.tryParse(v?.toString() ?? '');
    }

    return BranchSale(
      saleId: asInt(json['saleId'] ?? json['SaleId'] ?? json['id'] ?? json['Id']),
      customerId: asIntOrNull(
          json['customerId'] ?? json['CustomerId'] ?? json['customerID']),
      customerName: (json['customerName'] ??
              json['CustomerName'] ??
              json['fullName'] ??
              json['FullName'] ??
              '—')
          .toString(),
      phone: (json['customerPhone'] ?? json['CustomerPhone'] ?? json['phone'] ?? json['Phone'])
          ?.toString(),
      amount: asNum(json['amount'] ?? json['Amount'] ?? json['total'] ?? json['Total']),
      employeeName: (json['employeeName'] ??
              json['EmployeeName'] ??
              json['salesEmployeeName'] ??
              json['SalesEmployeeName'])
          ?.toString(),
      createdAt: (json['createdAtUtc'] ?? json['CreatedAtUtc'] ?? json['date'] ?? json['Date'])
          ?.toString(),
      status: (json['status'] ?? json['Status'])?.toString(),
    );
  }
}

class SaleReview {
  const SaleReview({
    required this.id,
    required this.missionId,
    required this.cityValue,
    required this.saleId,
    required this.score,
    this.customerName,
    this.note,
  });

  final int id;
  final int missionId;
  final String cityValue;
  final int saleId;
  final int score;
  final String? customerName;
  final String? note;

  factory SaleReview.fromJson(Map<String, dynamic> json) {
    int asInt(dynamic v) {
      if (v is int) return v;
      return int.tryParse(v?.toString() ?? '') ?? 0;
    }

    return SaleReview(
      id: asInt(json['id'] ?? json['Id']),
      missionId: asInt(json['missionId'] ?? json['MissionId']),
      cityValue: (json['cityValue'] ?? json['CityValue'] ?? '').toString(),
      saleId: asInt(json['saleId'] ?? json['SaleId']),
      score: asInt(json['score'] ?? json['Score']),
      customerName: (json['customerName'] ?? json['CustomerName'])?.toString(),
      note: (json['note'] ?? json['Note'])?.toString(),
    );
  }
}
