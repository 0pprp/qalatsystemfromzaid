class PaymentModel {
  final int customerPaymentID;
  final int customerID;
  final String customerName;
  final String delegateName;
  final double amountDenar;
  final String paymentDate;
  final double amountTotalSales;
  final double amountDaySales;
  final double receiptsTotal;
  final double amountRemaining;

  PaymentModel({
    required this.customerPaymentID,
    required this.customerID,
    required this.customerName,
    required this.delegateName,
    required this.amountDenar,
    required this.paymentDate,
    required this.amountTotalSales,
    required this.amountDaySales,
    required this.receiptsTotal,
    required this.amountRemaining,
  });

  factory PaymentModel.fromJson(Map<String, dynamic> json) {
    return PaymentModel(
      customerPaymentID: json['customerPaymentID'] ?? 0,
      customerID: json['customerID'] ?? 0,
      customerName: json['customerName'] ?? '',
      delegateName: json['delegateName'] ?? '',
      amountDenar: (json['amountDenar'] ?? 0).toDouble(),
      paymentDate: json['paymentDate']?.toString() ?? '',
      amountTotalSales: (json['amountTotalSales'] ?? 0).toDouble(),
      amountDaySales: (json['amountDaySales'] ?? 0).toDouble(),
      receiptsTotal: (json['receiptsTotal'] ?? 0).toDouble(),
      amountRemaining: (json['amountRemaining'] ?? 0).toDouble(),
    );
  }
}

class CustomerPaymentModel {
  final int customerPaymentID;
  final int customerID;
  final String customerName;
  final String delegateName;
  final double amountDenar;
  final String paymentDate;

  CustomerPaymentModel({
    required this.customerPaymentID,
    required this.customerID,
    required this.customerName,
    required this.delegateName,
    required this.amountDenar,
    required this.paymentDate,
  });

  factory CustomerPaymentModel.fromJson(Map<String, dynamic> json) {
    return CustomerPaymentModel(
      customerPaymentID: json['customerPaymentID'] ?? 0,
      customerID: json['customerID'] ?? 0,
      customerName: json['customerName'] ?? '',
      delegateName: json['delegateName'] ?? '',
      amountDenar: (json['amountDenar'] ?? 0).toDouble(),
      paymentDate: json['paymentDate']?.toString() ?? '',
    );
  }
}

class WeekPaymentModel {
  final int customerID;
  final String customerName;
  final String phoneNumber;
  final String delegateName;
  final String itemsNames;
  final String dateSaleDevice;
  final int numberOfDayDevice;
  final double amountTotalSales;
  final double costTotalSales;
  final double amountDaySales;
  final double receiptsTotal;
  final double amountRemaining;
  final int countReceiptDevice;
  final String lastPaymentDate;
  final List<double> amounts;

  WeekPaymentModel({
    required this.customerID,
    required this.customerName,
    required this.phoneNumber,
    required this.delegateName,
    required this.itemsNames,
    required this.dateSaleDevice,
    required this.numberOfDayDevice,
    required this.amountTotalSales,
    required this.costTotalSales,
    required this.amountDaySales,
    required this.receiptsTotal,
    required this.amountRemaining,
    required this.countReceiptDevice,
    required this.lastPaymentDate,
    required this.amounts,
  });

  factory WeekPaymentModel.fromJson(Map<String, dynamic> json, {int days = 7}) {
    final amounts = <double>[];
    for (int i = 1; i <= days; i++) {
      amounts.add((json['amount$i'] ?? 0).toDouble());
    }
    return WeekPaymentModel(
      customerID: json['customerID'] ?? 0,
      customerName: json['customerName'] ?? '',
      phoneNumber: json['phoneNumber'] ?? '',
      delegateName: json['delegateName'] ?? '',
      itemsNames: json['itemsNames'] ?? '',
      dateSaleDevice: json['dateSaleDevice']?.toString() ?? '',
      numberOfDayDevice: json['numberOfDayDevice'] ?? 0,
      amountTotalSales: (json['amountTotalSales'] ?? 0).toDouble(),
      costTotalSales: (json['costTotalSales'] ?? 0).toDouble(),
      amountDaySales: (json['amountDaySales'] ?? 0).toDouble(),
      receiptsTotal: (json['receiptsTotal'] ?? 0).toDouble(),
      amountRemaining: (json['amountRemaining'] ?? 0).toDouble(),
      countReceiptDevice: json['countReceiptDevice'] ?? 0,
      lastPaymentDate: json['lastPaymentDate']?.toString() ?? '',
      amounts: amounts,
    );
  }
}
