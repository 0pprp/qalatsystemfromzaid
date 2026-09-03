import 'package:sales_employee_application/config/app_env.dart';
import 'package:sales_employee_application/services/session.dart';

class SalesMe {
  SalesMe({
    required this.employeeId,
    required this.employeeName,
    required this.branchId,
    required this.branchName,
    required this.role,
    this.isSalesShiftStarted = false,
  });

  final int employeeId;
  final String employeeName;
  final String branchId;
  final String branchName;
  final String role;
  final bool isSalesShiftStarted;

  factory SalesMe.fromJson(Map<String, dynamic> json) => SalesMe(
        employeeId: int.tryParse('${json['employeeId'] ?? json['EmployeeId'] ?? 0}') ?? 0,
        employeeName: '${json['employeeName'] ?? json['EmployeeName'] ?? ''}',
        branchId: '${json['branchId'] ?? json['BranchId'] ?? ''}',
        branchName: '${json['branchName'] ?? json['BranchName'] ?? ''}',
        role: '${json['role'] ?? json['Role'] ?? ''}',
        isSalesShiftStarted: json['isSalesShiftStarted'] == true ||
            json['IsSalesShiftStarted'] == true,
      );
}

class SalesCustomer {
  SalesCustomer({
    required this.customerId,
    required this.fullName,
    this.phone,
    this.province,
    this.salePrice = 0,
    this.nationalCardNumber,
    this.address,
    this.nearestLandmark,
    this.mukhtarName,
    this.rationCenterNumber,
    this.sourceBranchValue,
    this.sourceBranchName,
    this.sourceDatabase,
    this.sourceApiLink,
  });

  final int customerId;
  final String fullName;
  final String? phone;
  final String? province;
  final num salePrice;
  final String? nationalCardNumber;
  final String? address;
  final String? nearestLandmark;
  final String? mukhtarName;
  final String? rationCenterNumber;
  final int? sourceBranchValue;
  final String? sourceBranchName;
  final String? sourceDatabase;
  final String? sourceApiLink;

  bool get isForeignBranch {
    final link = sourceApiLink;
    if (link == null || link.isEmpty) return false;
    final mine = Session.apiBase ?? AppEnv.apiBase();
    if (mine.isEmpty) return true;
    return AppEnv.normalizeBase(link) != AppEnv.normalizeBase(mine);
  }

  SalesCustomer copyWithBranch({
    required int sourceBranchValue,
    required String sourceBranchName,
    required String sourceDatabase,
    required String sourceApiLink,
  }) {
    return SalesCustomer(
      customerId: customerId,
      fullName: fullName,
      phone: phone,
      province: province,
      salePrice: salePrice,
      nationalCardNumber: nationalCardNumber,
      address: address,
      nearestLandmark: nearestLandmark,
      mukhtarName: mukhtarName,
      rationCenterNumber: rationCenterNumber,
      sourceBranchValue: sourceBranchValue,
      sourceBranchName: sourceBranchName,
      sourceDatabase: sourceDatabase,
      sourceApiLink: sourceApiLink,
    );
  }

  factory SalesCustomer.fromJson(Map<String, dynamic> json) => SalesCustomer(
        customerId: int.tryParse('${json['customerId'] ?? json['CustomerId'] ?? 0}') ?? 0,
        fullName: '${json['fullName'] ?? json['FullName'] ?? json['customerName'] ?? ''}',
        phone: json['phone']?.toString() ?? json['Phone']?.toString() ?? json['phoneNumber']?.toString(),
        province: json['province']?.toString() ?? json['Province']?.toString(),
        salePrice: num.tryParse('${json['salePrice'] ?? json['SalePrice'] ?? 0}') ?? 0,
        nationalCardNumber: json['nationalCardNumber']?.toString(),
        address: json['address']?.toString(),
        nearestLandmark: json['nearestLandmark']?.toString() ?? json['nearestFunctionPoint']?.toString(),
        mukhtarName: json['mukhtarName']?.toString(),
        rationCenterNumber: json['rationCenterNumber']?.toString(),
        sourceBranchValue: int.tryParse('${json['sourceBranchValue'] ?? ''}'),
        sourceBranchName: json['sourceBranchName']?.toString(),
        sourceDatabase: json['sourceDatabase']?.toString(),
        sourceApiLink: json['sourceApiLink']?.toString(),
      );
}

class SalesInventoryItem {
  SalesInventoryItem({
    required this.productId,
    required this.productName,
    required this.availableQuantity,
    required this.salePrice,
    this.dailyInstallment,
    this.notes,
    this.storeId,
  });

  final int productId;
  final String productName;
  final int availableQuantity;
  final num salePrice;
  /// Catalog daily installment from inventory JSON when present.
  /// Current GET /api/sales/inventory does not include this field.
  final num? dailyInstallment;
  final String? notes;
  final int? storeId;

  static num? _optionalNum(Map<String, dynamic> json, List<String> keys) {
    for (final key in keys) {
      if (!json.containsKey(key) || json[key] == null) continue;
      final value = json[key];
      if (value is num) return value;
      final parsed = num.tryParse('$value');
      if (parsed != null) return parsed;
    }
    return null;
  }

  factory SalesInventoryItem.fromJson(Map<String, dynamic> json) => SalesInventoryItem(
        productId: int.tryParse('${json['productId'] ?? json['ProductId'] ?? json['itemID'] ?? json['ItemID'] ?? 0}') ?? 0,
        productName: '${json['productName'] ?? json['ProductName'] ?? json['itemName'] ?? json['ItemName'] ?? ''}',
        availableQuantity: int.tryParse('${json['availableQuantity'] ?? json['AvailableQuantity'] ?? json['quantity'] ?? json['Quantity'] ?? 0}') ?? 0,
        salePrice: num.tryParse('${json['salePrice'] ?? json['SalePrice'] ?? json['itemPriceDenar'] ?? json['ItemPriceDenar'] ?? 0}') ?? 0,
        dailyInstallment: _optionalNum(json, const [
          'dailyInstallment',
          'DailyInstallment',
          'amountDayDenar',
          'AmountDayDenar',
          'amountDay',
          'AmountDay',
        ]),
        notes: (json['notes'] ?? json['Notes'])?.toString(),
        storeId: int.tryParse('${json['storeId'] ?? json['StoreId'] ?? json['storeID'] ?? json['StoreID'] ?? ''}'),
      );
}

class SalesDraftItem {
  SalesDraftItem({
    required this.productId,
    required this.quantity,
    this.productName,
    this.unitSalePrice,
    this.lineSalePrice,
  });

  final int productId;
  final int quantity;
  final String? productName;
  final num? unitSalePrice;
  final num? lineSalePrice;

  factory SalesDraftItem.fromJson(Map<String, dynamic> json) => SalesDraftItem(
        productId: int.tryParse('${json['productId'] ?? json['ProductId'] ?? 0}') ?? 0,
        quantity: int.tryParse('${json['quantity'] ?? json['Quantity'] ?? 0}') ?? 0,
        productName: json['productName']?.toString(),
        unitSalePrice: num.tryParse('${json['unitSalePrice'] ?? 0}'),
        lineSalePrice: num.tryParse('${json['lineSalePrice'] ?? 0}'),
      );

  Map<String, dynamic> toRequest() => {
        'productId': productId,
        'quantity': quantity,
      };
}

class SalesDraft {
  SalesDraft({
    required this.saleId,
    required this.fullName,
    required this.status,
    required this.evaluationLevel,
    required this.evaluationNote,
    required this.baseSalePrice,
    required this.finalSalePrice,
    required this.dailyInstallment,
    required this.createdAt,
    this.phone,
    this.province,
    this.nationalCardNumber,
    this.address,
    this.nearestLandmark,
    this.mukhtarName,
    this.rationCenterNumber,
    this.employeeName,
    this.completedAt,
    this.documentsStatus,
    this.items = const [],
    this.documents = const [],
  });

  final int saleId;
  final String fullName;
  final String? phone;
  final String? province;
  final String? nationalCardNumber;
  final String? address;
  final String? nearestLandmark;
  final String? mukhtarName;
  final String? rationCenterNumber;
  final String? employeeName;
  final String status;
  final int evaluationLevel;
  final String evaluationNote;
  final num baseSalePrice;
  final num finalSalePrice;
  final num dailyInstallment;
  final DateTime createdAt;
  final DateTime? completedAt;
  final String? documentsStatus;
  final List<SalesDraftItem> items;
  final List<SalesDocument> documents;

  bool get isRejected => status == 'Rejected' || evaluationLevel == 1;
  bool get isCompleted =>
      status == 'Completed' ||
      status == 'DocumentsReady' ||
      status == 'DocumentsPending' ||
      completedAt != null;
  bool get canComplete => status == 'Pending' && !isRejected;

  SalesDraft copyWith({
    String? status,
    DateTime? completedAt,
    String? documentsStatus,
    List<SalesDocument>? documents,
  }) =>
      SalesDraft(
        saleId: saleId,
        fullName: fullName,
        phone: phone,
        province: province,
        nationalCardNumber: nationalCardNumber,
        address: address,
        nearestLandmark: nearestLandmark,
        mukhtarName: mukhtarName,
        rationCenterNumber: rationCenterNumber,
        employeeName: employeeName,
        status: status ?? this.status,
        evaluationLevel: evaluationLevel,
        evaluationNote: evaluationNote,
        baseSalePrice: baseSalePrice,
        finalSalePrice: finalSalePrice,
        dailyInstallment: dailyInstallment,
        createdAt: createdAt,
        completedAt: completedAt ?? this.completedAt,
        documentsStatus: documentsStatus ?? this.documentsStatus,
        items: items,
        documents: documents ?? this.documents,
      );

  factory SalesDraft.fromJson(Map<String, dynamic> json) => SalesDraft(
        saleId: int.tryParse('${json['saleId'] ?? json['SaleId'] ?? 0}') ?? 0,
        fullName: '${json['fullName'] ?? json['FullName'] ?? ''}',
        phone: json['phone']?.toString(),
        province: json['province']?.toString(),
        nationalCardNumber: json['nationalCardNumber']?.toString() ?? json['NationalCardNumber']?.toString(),
        address: json['address']?.toString() ?? json['Address']?.toString(),
        nearestLandmark: json['nearestLandmark']?.toString() ?? json['NearestLandmark']?.toString(),
        mukhtarName: json['mukhtarName']?.toString() ?? json['MukhtarName']?.toString(),
        rationCenterNumber: json['rationCenterNumber']?.toString() ?? json['RationCenterNumber']?.toString(),
        employeeName: json['userName']?.toString() ?? json['UserName']?.toString() ?? json['employeeName']?.toString(),
        status: '${json['status'] ?? json['Status'] ?? 'Pending'}',
        evaluationLevel: int.tryParse('${json['evaluationLevel'] ?? json['EvaluationLevel'] ?? 0}') ?? 0,
        evaluationNote: '${json['evaluationNote'] ?? json['EvaluationNote'] ?? ''}',
        baseSalePrice: num.tryParse('${json['baseSalePrice'] ?? json['BaseSalePrice'] ?? 0}') ?? 0,
        finalSalePrice: num.tryParse('${json['finalSalePrice'] ?? json['FinalSalePrice'] ?? 0}') ?? 0,
        dailyInstallment: num.tryParse('${json['dailyInstallment'] ?? json['DailyInstallment'] ?? 0}') ?? 0,
        createdAt: DateTime.tryParse('${json['createdAt'] ?? json['CreatedAt'] ?? ''}') ?? DateTime.now(),
        completedAt: DateTime.tryParse('${json['completedAt'] ?? json['CompletedAt'] ?? ''}'),
        documentsStatus: json['documentsStatus']?.toString() ?? json['DocumentsStatus']?.toString(),
        items: [
          for (final e in (json['items'] ?? json['Items'] ?? const []))
            if (e is Map) SalesDraftItem.fromJson(Map<String, dynamic>.from(e)),
        ],
        documents: [
          for (final e in (json['documents'] ?? json['Documents'] ?? const []))
            if (e is Map) SalesDocument.fromJson(Map<String, dynamic>.from(e)),
        ],
      );
}

class SalesDocument {
  SalesDocument({
    required this.type,
    required this.fileName,
    required this.downloadUrl,
    this.documentId,
  });

  final String type;
  final String fileName;
  final String downloadUrl;
  final int? documentId;

  bool get isContract => type == 'Contract';
  bool get isPromissoryNote => type == 'PromissoryNote';

  factory SalesDocument.fromJson(Map<String, dynamic> json) => SalesDocument(
        type: '${json['type'] ?? json['Type'] ?? ''}',
        fileName: '${json['fileName'] ?? json['FileName'] ?? ''}',
        downloadUrl: '${json['downloadUrl'] ?? json['DownloadUrl'] ?? ''}',
        documentId: int.tryParse('${json['documentId'] ?? json['DocumentId'] ?? ''}'),
      );
}

class SalesCompleteResult {
  SalesCompleteResult({
    required this.saleId,
    required this.status,
    required this.finalSalePrice,
    this.completedAt,
    this.documentsStatus,
    this.documents = const [],
  });

  final int saleId;
  final String status;
  final String? documentsStatus;
  final DateTime? completedAt;
  final num finalSalePrice;
  final List<SalesDocument> documents;

  factory SalesCompleteResult.fromJson(Map<String, dynamic> json) => SalesCompleteResult(
        saleId: int.tryParse('${json['saleId'] ?? json['SaleId'] ?? 0}') ?? 0,
        status: '${json['status'] ?? json['Status'] ?? 'Completed'}',
        documentsStatus: json['documentsStatus']?.toString(),
        completedAt: DateTime.tryParse('${json['completedAt'] ?? json['CompletedAt'] ?? ''}'),
        finalSalePrice: num.tryParse('${json['finalSalePrice'] ?? json['FinalSalePrice'] ?? 0}') ?? 0,
        documents: [
          for (final e in (json['documents'] ?? json['Documents'] ?? const []))
            if (e is Map) SalesDocument.fromJson(Map<String, dynamic>.from(e)),
        ],
      );
}

class SalesDraftCreateRequest {
  SalesDraftCreateRequest({
    this.customerId,
    required this.customer,
    required this.items,
    required this.evaluationLevel,
    required this.evaluationNote,
    required this.dailyInstallment,
    this.salesRequestId,
  });

  final int? customerId;
  final Map<String, String> customer;
  final List<SalesDraftItem> items;
  final int evaluationLevel;
  final String evaluationNote;
  final num dailyInstallment;
  final int? salesRequestId;

  Map<String, dynamic> toJson() => {
        if (customerId != null) 'customerId': customerId,
        'customer': customer,
        'items': items.map((e) => e.toRequest()).toList(),
        'evaluationLevel': evaluationLevel,
        'evaluationNote': evaluationNote,
        'dailyInstallment': dailyInstallment,
        if (salesRequestId != null) 'salesRequestId': salesRequestId,
      };
}

class SalesWorkRequest {
  SalesWorkRequest({
    required this.id,
    required this.customerName,
    this.customerPhone,
    this.customerProvince,
    this.customerAddress,
    this.existingCustomerId,
    this.notes,
    required this.status,
    required this.createdAtUtc,
    this.convertedToSaleId,
    this.rejectionReason,
  });

  final int id;
  final String customerName;
  final String? customerPhone;
  final String? customerProvince;
  final String? customerAddress;
  final int? existingCustomerId;
  final String? notes;
  final String status;
  final DateTime createdAtUtc;
  final int? convertedToSaleId;
  final String? rejectionReason;

  bool get isNew => status == 'New';
  bool get canConvert => status != 'Rejected' && status != 'Completed' && convertedToSaleId == null;

  factory SalesWorkRequest.fromJson(Map<String, dynamic> json) => SalesWorkRequest(
        id: int.tryParse('${json['id'] ?? json['Id'] ?? 0}') ?? 0,
        customerName: '${json['customerName'] ?? json['CustomerName'] ?? ''}',
        customerPhone: json['customerPhone']?.toString() ?? json['CustomerPhone']?.toString(),
        customerProvince: json['customerProvince']?.toString(),
        customerAddress: json['customerAddress']?.toString(),
        existingCustomerId: int.tryParse('${json['existingCustomerId'] ?? json['ExistingCustomerId'] ?? ''}'),
        notes: json['notes']?.toString() ?? json['Notes']?.toString(),
        status: '${json['status'] ?? json['Status'] ?? 'New'}',
        createdAtUtc: DateTime.tryParse('${json['createdAtUtc'] ?? json['CreatedAtUtc'] ?? ''}')?.toUtc() ??
            DateTime.now().toUtc(),
        convertedToSaleId: int.tryParse('${json['convertedToSaleId'] ?? json['ConvertedToSaleId'] ?? ''}'),
        rejectionReason: json['rejectionReason']?.toString(),
      );
}
