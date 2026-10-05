/// Split Daily «المسددين» rows by paid vs due installment.
class FollowerPayersSplit {
  FollowerPayersSplit._();

  /// Paid less than due installment (e.g. due 20000, paid 15000).
  static bool isUnderDue(num paid, num due) => paid.toDouble() < due.toDouble();

  /// Paid greater than or equal to due installment.
  static bool isCorrect(num paid, num due) => paid.toDouble() >= due.toDouble();

  static List<Map<String, dynamic>> underDue(Iterable<Map<String, dynamic>> rows) {
    return rows
        .where((r) => isUnderDue(_paid(r), _due(r)))
        .toList(growable: false);
  }

  static List<Map<String, dynamic>> correct(Iterable<Map<String, dynamic>> rows) {
    return rows
        .where((r) => isCorrect(_paid(r), _due(r)))
        .toList(growable: false);
  }

  static double _paid(Map<String, dynamic> r) =>
      (r['amountReceipt'] as num?)?.toDouble() ?? 0;

  static double _due(Map<String, dynamic> r) =>
      (r['amountDaySales'] as num?)?.toDouble() ?? 0;
}
