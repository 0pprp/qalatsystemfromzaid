import 'package:intl/intl.dart';

class Formatters {
  static final NumberFormat _numberFormat = NumberFormat('#,###');
  static final NumberFormat _currencyFormat = NumberFormat('#,###');

  static String formatNumber(dynamic num) {
    if (num == null || num == 0) return '0';
    if (num is double && num == num.toInt().toDouble()) {
      return _numberFormat.format(num.toInt());
    }
    return _numberFormat.format(num);
  }

  static String formatCurrency(dynamic num) {
    if (num == null || num == 0) return '0 د.ع';
    final formatted = formatNumber(num);
    return '$formatted د.ع';
  }

  static String formatCurrencyNoZero(dynamic num) {
    if (num == null || num == 0) return 'لا يوجد';
    final formatted = formatNumber(num);
    return '$formatted د.ع';
  }

  static String formatDate(String? dateStr) {
    if (dateStr == null || dateStr.isEmpty) return 'لا يوجد';
    try {
      final date = DateTime.parse(dateStr);
      return DateFormat('yyyy-MM-dd').format(date);
    } catch (_) {
      return dateStr;
    }
  }

  static String formatArabicDate(String? dateStr) {
    if (dateStr == null || dateStr.isEmpty) return 'لا يوجد';
    try {
      final date = DateTime.parse(dateStr);
      return DateFormat('dd MMMM yyyy', 'ar').format(date);
    } catch (_) {
      return dateStr;
    }
  }

  static String todayEnCA() {
    return DateFormat('yyyy-MM-dd').format(DateTime.now());
  }

  static String daysAgoEnCA(int days) {
    return DateFormat('yyyy-MM-dd').format(DateTime.now().subtract(Duration(days: days)));
  }
}
