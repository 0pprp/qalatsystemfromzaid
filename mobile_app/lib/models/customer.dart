class CustomerModel {
  final int customerID;
  final String customerName;
  final String phoneNumber;
  final String delegateName;
  final int? delegateID;
  final double amountTotalSales;
  final double amountDaySales;
  final double receiptsTotal;
  final double amountRemaining;
  final double costTotalSales;
  final String itemsNames;
  final String dateSaleDevice;
  final int numberOfDayDevice;
  final int countReceiptDevice;
  final String lastPaymentDate;
  final String address;
  final String shopName;
  final String nearestFunctionPoint;
  final String receiptName;
  final String saleName;
  final String notes;
  final bool isLegal;
  final double? receiptRateDevice;
  final int? numberOfDayPayment;

  CustomerModel({
    required this.customerID,
    required this.customerName,
    required this.phoneNumber,
    required this.delegateName,
    this.delegateID,
    required this.amountTotalSales,
    required this.amountDaySales,
    required this.receiptsTotal,
    required this.amountRemaining,
    required this.costTotalSales,
    required this.itemsNames,
    required this.dateSaleDevice,
    required this.numberOfDayDevice,
    required this.countReceiptDevice,
    required this.lastPaymentDate,
    required this.address,
    required this.shopName,
    required this.nearestFunctionPoint,
    required this.receiptName,
    required this.saleName,
    required this.notes,
    required this.isLegal,
    this.receiptRateDevice,
    this.numberOfDayPayment,
  });

  factory CustomerModel.fromJson(Map<String, dynamic> json) {
    return CustomerModel(
      customerID: json['customerID'] ?? 0,
      customerName: json['customerName'] ?? '',
      phoneNumber: json['phoneNumber'] ?? '',
      delegateName: json['delegateName'] ?? '',
      delegateID: json['delegateID'],
      amountTotalSales: (json['amountTotalSales'] ?? 0).toDouble(),
      amountDaySales: (json['amountDaySales'] ?? 0).toDouble(),
      receiptsTotal: (json['receiptsTotal'] ?? 0).toDouble(),
      amountRemaining: (json['amountRemaining'] ?? 0).toDouble(),
      costTotalSales: (json['costTotalSales'] ?? 0).toDouble(),
      itemsNames: json['itemsNames'] ?? '',
      dateSaleDevice: json['dateSaleDevice']?.toString() ?? '',
      numberOfDayDevice: json['numberOfDayDevice'] ?? 0,
      countReceiptDevice: json['countReceiptDevice'] ?? 0,
      lastPaymentDate: json['lastPaymentDate']?.toString() ?? '',
      address: json['address'] ?? '',
      shopName: json['shopName'] ?? '',
      nearestFunctionPoint: json['nearestFunctionPoint'] ?? '',
      receiptName: json['receiptName'] ?? '',
      saleName: json['saleName'] ?? '',
      notes: json['notes'] ?? '',
      isLegal: json['isLegal'] ?? false,
      receiptRateDevice: (json['receiptRateDevice'] ?? 0).toDouble(),
      numberOfDayPayment: json['numberOfDayPayment'],
    );
  }
}
