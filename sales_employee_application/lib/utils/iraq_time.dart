class IraqTime {
  static DateTime now() {
    return DateTime.now().toUtc().add(const Duration(hours: 3));
  }

  static DateTime businessDate([DateTime? iraqNow]) {
    final local = iraqNow ?? now();
    final date = DateTime(local.year, local.month, local.day);
    if (local.hour < 3) {
      return date.subtract(const Duration(days: 1));
    }
    return date;
  }

  static DateTime shiftEnd([DateTime? iraqNow]) {
    return businessDate(iraqNow).add(const Duration(days: 1, hours: 3));
  }

  static String dateKey([DateTime? iraqNow]) {
    final d = businessDate(iraqNow);
    return '${d.year.toString().padLeft(4, '0')}-'
        '${d.month.toString().padLeft(2, '0')}-'
        '${d.day.toString().padLeft(2, '0')}';
  }

  static bool isSameBusinessDay(String? storedKey) {
    return storedKey != null && storedKey == dateKey();
  }
}
