class PaymentValidation {
  /// Minimum allowed collection payment amount in Iraqi dinars.
  static const double minAmountIqd = 2000;

  static const String minAmountMessage =
      'أقل مبلغ مسموح للتسديد هو 2,000 د.ع';

  static String? validateAmount(num? amount) {
    if (amount == null || amount <= 0) {
      return 'لا يمكن ترك المبلغ فارغًا أو يساوي صفر';
    }
    if (amount < minAmountIqd) {
      return minAmountMessage;
    }
    return null;
  }

  static bool isAcceptableAmount(num? amount) => validateAmount(amount) == null;
}
