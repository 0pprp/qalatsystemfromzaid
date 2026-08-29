class StatisticsAppModel {
  final int numberOfStores;
  final int numberOfItems;
  final int numberOfSuppliers;
  final int numberOfPurchases;
  final int numberOfDelegates;
  final int numberOfCustomers;
  final int numberOfSales;
  final int numberOfPayments;
  final int numberOfCashBoxes;
  final int numberOfAdditionsToBox;
  final int numberOfWithdrawalsFromBox;
  final int numberOfTransfersBetweenBoxes;

  StatisticsAppModel({
    required this.numberOfStores,
    required this.numberOfItems,
    required this.numberOfSuppliers,
    required this.numberOfPurchases,
    required this.numberOfDelegates,
    required this.numberOfCustomers,
    required this.numberOfSales,
    required this.numberOfPayments,
    required this.numberOfCashBoxes,
    required this.numberOfAdditionsToBox,
    required this.numberOfWithdrawalsFromBox,
    required this.numberOfTransfersBetweenBoxes,
  });

  factory StatisticsAppModel.fromJson(Map<String, dynamic> json) {
    return StatisticsAppModel(
      numberOfStores: json['numberOfStores'] ?? 0,
      numberOfItems: json['numberOfItems'] ?? 0,
      numberOfSuppliers: json['numberOfSuppliers'] ?? 0,
      numberOfPurchases: json['numberOfPurchases'] ?? 0,
      numberOfDelegates: json['numberOfDelegates'] ?? 0,
      numberOfCustomers: json['numberOfCustomers'] ?? 0,
      numberOfSales: json['numberOfSales'] ?? 0,
      numberOfPayments: json['numberOfPayments'] ?? 0,
      numberOfCashBoxes: json['numberOfCashBoxes'] ?? 0,
      numberOfAdditionsToBox: json['numberOfAdditionsToBox'] ?? 0,
      numberOfWithdrawalsFromBox: json['numberOfWithdrawalsFromBox'] ?? 0,
      numberOfTransfersBetweenBoxes: json['numberOfTransfersBetweenBoxes'] ?? 0,
    );
  }
}

class DelegateStatisticsModel {
  final String delegateName;
  final int numberOfCustomer;
  final double amountPrice;
  final double amountCost;
  final double amountDay;
  final int numberOfItemSale;
  final double amountReceipt;
  final int numberOfCustomerZero;
  final double amountPriceZero;
  final double amountDayZero;

  DelegateStatisticsModel({
    required this.delegateName,
    required this.numberOfCustomer,
    required this.amountPrice,
    required this.amountCost,
    required this.amountDay,
    required this.numberOfItemSale,
    required this.amountReceipt,
    required this.numberOfCustomerZero,
    required this.amountPriceZero,
    required this.amountDayZero,
  });

  factory DelegateStatisticsModel.fromJson(Map<String, dynamic> json) {
    return DelegateStatisticsModel(
      delegateName: json['delegateName'] ?? '',
      numberOfCustomer: json['numberOfCustomer'] ?? 0,
      amountPrice: (json['amountPrice'] ?? 0).toDouble(),
      amountCost: (json['amountCost'] ?? 0).toDouble(),
      amountDay: (json['amountDay'] ?? 0).toDouble(),
      numberOfItemSale: json['numberOfItemSale'] ?? 0,
      amountReceipt: (json['amountReceipt'] ?? 0).toDouble(),
      numberOfCustomerZero: json['numberOfCustomerZero'] ?? 0,
      amountPriceZero: (json['amountPriceZero'] ?? 0).toDouble(),
      amountDayZero: (json['amountDayZero'] ?? 0).toDouble(),
    );
  }
}
