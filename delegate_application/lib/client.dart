/// Shared customer list model used by the customers screen and PDF reports.
/// Kept in its own library to avoid circular imports with report modules.
class Client {
  final String name;
  final String phone;
  final String address;
  final String shopName;
  final double salePrice;
  final double receipt;
  final double dailyInstallment;
  final double remaining;
  final String itemsNames;
  final double amount1;
  final double amount2;
  final double amount3;
  final double amount4;
  final double amount5;
  final double amount6;
  final double amount7;
  final String phoneNumberCompany;
  final String countReceiptDevice;
  final String numberOfDayPayment;
  final String isLegal;
  final String lastPaymentDate;
  final String dateSaleDevice;
  final int customerId;

  Client({
    required this.name,
    required this.phone,
    required this.address,
    required this.shopName,
    required this.salePrice,
    required this.receipt,
    required this.dailyInstallment,
    required this.remaining,
    required this.itemsNames,
    required this.amount1,
    required this.amount2,
    required this.amount3,
    required this.amount4,
    required this.amount5,
    required this.amount6,
    required this.amount7,
    required this.phoneNumberCompany,
    required this.countReceiptDevice,
    required this.numberOfDayPayment,
    required this.isLegal,
    required this.lastPaymentDate,
    required this.dateSaleDevice,
    required this.customerId,
  });
}
