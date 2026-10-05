class CustomerProfile {
  const CustomerProfile({
    this.customerId,
    required this.customerName,
    this.phone,
    this.address,
    this.province,
    this.nationalCardNumber,
    this.delegateName,
    this.cityName,
    this.notes = const [],
    this.documents = const [],
    this.evaluations = const [],
    this.sales = const [],
    this.payments = const [],
    this.history = const [],
  });

  final int? customerId;
  final String customerName;
  final String? phone;
  final String? address;
  final String? province;
  final String? nationalCardNumber;
  final String? delegateName;
  final String? cityName;
  final List<CustomerNote> notes;
  final List<CustomerDocument> documents;
  final List<CustomerEvaluation> evaluations;
  final List<CustomerSaleRow> sales;
  final List<CustomerPayment> payments;
  final List<CustomerHistoryRow> history;

  factory CustomerProfile.fromJson(Map<String, dynamic> json) {
    List<Map<String, dynamic>> maps(dynamic raw) {
      if (raw is! List) return const [];
      return raw
          .whereType<Map>()
          .map((e) => Map<String, dynamic>.from(e))
          .toList();
    }

    int? asInt(dynamic v) {
      if (v == null) return null;
      if (v is int) return v;
      return int.tryParse(v.toString());
    }

    return CustomerProfile(
      customerId: asInt(json['customerId'] ?? json['CustomerId']),
      customerName: (json['customerName'] ?? json['CustomerName'] ?? '—').toString(),
      phone: (json['phone'] ?? json['Phone'])?.toString(),
      address: (json['address'] ?? json['Address'])?.toString(),
      province: (json['province'] ?? json['Province'])?.toString(),
      nationalCardNumber:
          (json['nationalCardNumber'] ?? json['NationalCardNumber'])?.toString(),
      delegateName: (json['delegateName'] ??
              json['DelegateName'] ??
              json['customerListName'] ??
              json['CustomerListName'])
          ?.toString(),
      cityName: (json['cityName'] ?? json['CityName'])?.toString(),
      notes: maps(json['notes'] ?? json['Notes']).map(CustomerNote.fromJson).toList(),
      documents: maps(json['customerDocuments'] ?? json['CustomerDocuments'])
          .map(CustomerDocument.fromJson)
          .toList(),
      evaluations: maps(json['evaluations'] ?? json['Evaluations'])
          .map(CustomerEvaluation.fromJson)
          .toList(),
      sales: maps(json['sales'] ?? json['Sales']).map(CustomerSaleRow.fromJson).toList(),
      payments:
          maps(json['payments'] ?? json['Payments']).map(CustomerPayment.fromJson).toList(),
      history:
          maps(json['history'] ?? json['History']).map(CustomerHistoryRow.fromJson).toList(),
    );
  }
}

class CustomerNote {
  const CustomerNote({
    required this.body,
    this.authorName,
    this.authorRole,
    this.createdAt,
  });

  final String body;
  final String? authorName;
  final String? authorRole;
  final String? createdAt;

  factory CustomerNote.fromJson(Map<String, dynamic> json) => CustomerNote(
        body: (json['note'] ??
                json['Note'] ??
                json['body'] ??
                json['Body'] ??
                json['text'] ??
                '')
            .toString(),
        authorName: (json['authorName'] ??
                json['AuthorName'] ??
                json['createdByName'] ??
                json['CreatedByName'] ??
                json['userName'] ??
                json['UserName'])
            ?.toString(),
        authorRole: (json['authorRole'] ??
                json['AuthorRole'] ??
                json['userType'] ??
                json['UserType'] ??
                json['role'] ??
                json['Role'])
            ?.toString(),
        createdAt: (json['createdAtUtc'] ?? json['CreatedAtUtc'] ?? json['createdAt'])
            ?.toString(),
      );
}

class CustomerDocument {
  const CustomerDocument({
    required this.id,
    required this.type,
    this.label,
  });

  final int id;
  final String type;
  final String? label;

  String get displayLabel {
    if (label != null && label!.trim().isNotEmpty) return label!.trim();
    return switch (type) {
      'NationalIdFront' => 'البطاقة الوطنية - أمامية',
      'NationalIdBack' => 'البطاقة الوطنية - خلفية',
      'ResidenceCardFront' => 'بطاقة السكن - أمامية',
      'ResidenceCardBack' => 'بطاقة السكن - خلفية',
      'ResidenceCertificate' => 'تأييد السكن',
      'ResidenceCard' => 'بطاقة السكن',
      _ => type.isEmpty ? 'مستمسك' : type,
    };
  }

  factory CustomerDocument.fromJson(Map<String, dynamic> json) {
    int asInt(dynamic v) {
      if (v is int) return v;
      return int.tryParse(v?.toString() ?? '') ?? 0;
    }

    return CustomerDocument(
      id: asInt(json['id'] ?? json['Id'] ?? json['documentId'] ?? json['DocumentId']),
      type: (json['documentType'] ?? json['DocumentType'] ?? json['type'] ?? '').toString(),
      label: (json['label'] ?? json['Label'] ?? json['title'] ?? json['Title'])?.toString(),
    );
  }
}

class CustomerEvaluation {
  const CustomerEvaluation({
    required this.saleId,
    required this.level,
    required this.name,
    this.note,
  });

  final int saleId;
  final int level;
  final String name;
  final String? note;

  factory CustomerEvaluation.fromJson(Map<String, dynamic> json) {
    int asInt(dynamic v) {
      if (v is int) return v;
      return int.tryParse(v?.toString() ?? '') ?? 0;
    }

    return CustomerEvaluation(
      saleId: asInt(json['saleId'] ?? json['SaleId']),
      level: asInt(json['evaluationLevel'] ?? json['EvaluationLevel']),
      name: (json['evaluationName'] ?? json['EvaluationName'] ?? '').toString(),
      note: (json['evaluationNote'] ?? json['EvaluationNote'])?.toString(),
    );
  }
}

class CustomerSaleRow {
  const CustomerSaleRow({
    required this.saleId,
    required this.status,
    this.employeeName,
    this.finalPrice,
    this.date,
  });

  final int saleId;
  final String status;
  final String? employeeName;
  final num? finalPrice;
  final String? date;

  String get statusLabel => switch (status) {
        'Pending' => 'بانتظار المصادقة',
        'Completed' => 'مكتمل / مصادق',
        'Rejected' => 'مرفوض',
        'DocumentsPending' => 'مستمسكات ناقصة',
        'DocumentsReady' => 'مستمسكات جاهزة',
        _ => status.isEmpty ? '—' : status,
      };

  factory CustomerSaleRow.fromJson(Map<String, dynamic> json) {
    int asInt(dynamic v) {
      if (v is int) return v;
      return int.tryParse(v?.toString() ?? '') ?? 0;
    }

    num? asNum(dynamic v) {
      if (v is num) return v;
      return num.tryParse(v?.toString() ?? '');
    }

    return CustomerSaleRow(
      saleId: asInt(json['saleId'] ?? json['SaleId']),
      status: (json['status'] ?? json['Status'] ?? '').toString(),
      employeeName: (json['employeeName'] ?? json['EmployeeName'])?.toString(),
      finalPrice: asNum(json['finalSalePrice'] ?? json['FinalSalePrice']),
      date: (json['date'] ?? json['Date'])?.toString(),
    );
  }
}

class CustomerPayment {
  const CustomerPayment({
    this.amount,
    this.date,
    this.note,
  });

  final num? amount;
  final String? date;
  final String? note;

  factory CustomerPayment.fromJson(Map<String, dynamic> json) {
    num? asNum(dynamic v) {
      if (v is num) return v;
      return num.tryParse(v?.toString() ?? '');
    }

    return CustomerPayment(
      amount: asNum(json['amount'] ?? json['Amount']),
      date: (json['paymentDate'] ?? json['PaymentDate'])?.toString(),
      note: (json['note'] ?? json['Note'])?.toString(),
    );
  }
}

class CustomerHistoryRow {
  const CustomerHistoryRow({
    required this.title,
    this.detail,
    this.at,
    this.actor,
  });

  final String title;
  final String? detail;
  final String? at;
  final String? actor;

  factory CustomerHistoryRow.fromJson(Map<String, dynamic> json) => CustomerHistoryRow(
        title: (json['action'] ??
                json['Action'] ??
                json['status'] ??
                json['Status'] ??
                json['title'] ??
                json['Title'] ??
                'حدث')
            .toString(),
        detail: (json['note'] ?? json['Note'] ?? json['details'] ?? json['Details'])
            ?.toString(),
        at: (json['atUtc'] ?? json['AtUtc'] ?? json['createdAtUtc'] ?? json['CreatedAtUtc'])
            ?.toString(),
        actor: (json['actorName'] ??
                json['ActorName'] ??
                json['userName'] ??
                json['UserName'] ??
                json['by'] ??
                json['By'])
            ?.toString(),
      );
}
