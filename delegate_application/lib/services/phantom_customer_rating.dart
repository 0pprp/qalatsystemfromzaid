import 'package:delegate_application/client.dart';
import 'package:delegate_application/utils/iraq_date.dart';
import 'package:delegate_application/utils/safe_num.dart';

/// Phantom sale-date rating (التقييم الوهمي) for delegate UI.
///
/// Per-customer score is one of: 10 / 5 / 0 / -5 / -10.
/// Overall «تقييمك» = sum(customer scores) / count — same formula as
/// BE_Company EmployeeSalesRating.Snapshot (no 30-day exclusion).
class PhantomCustomerRating {
  static const excellent = 'ممتاز';
  static const good = 'جيد';
  static const weak = 'ضعيف';
  static const rejected = 'متلكئ';
  static const bad = 'سيء';

  static const excellentRate = 7.1;
  static const goodRate = 6.25;
  static const weakRate = 1000.0 / 180.0;
  static const laggingMinRate = 1000.0 / 200.0;

  static const excellentMaxDays = 140;
  static const goodMaxDays = 160;
  static const weakMaxDaysExclusive = 180;
  static const laggingMaxDaysExclusive = 200;

  static final DateTime _floor = DateTime(2026, 5, 1);
  static final DateTime _boostEnd = DateTime(2026, 8, 31);

  /// Lockstep with BE_Company PhantomSaleDate / follower calculator.
  static DateTime adjustSaleDate(DateTime d) {
    final day = DateTime(d.year, d.month, d.day);
    if (day.isBefore(_floor)) return _floor;
    if (!day.isAfter(_boostEnd)) return day.add(const Duration(days: 30));
    return day;
  }

  static int diffDaysInclusive(DateTime from, DateTime to) =>
      to.difference(from).inDays + 1;

  static (String label, int score) classifySettledDays(int days) {
    if (days <= excellentMaxDays) return (excellent, 10);
    if (days <= goodMaxDays) return (good, 5);
    if (days < weakMaxDaysExclusive) return (weak, 0);
    if (days < laggingMaxDaysExclusive) return (rejected, -5);
    return (bad, -10);
  }

  static (String label, int score) classifyActiveRate(double rate) {
    if (rate >= excellentRate) return (excellent, 10);
    if (rate >= goodRate) return (good, 5);
    if (rate >= weakRate) return (weak, 0);
    if (rate >= laggingMinRate) return (rejected, -5);
    return (bad, -10);
  }

  static DateTime? parseDate(String? raw) {
    if (raw == null || raw.trim().isEmpty) return null;
    final t = raw.trim();
    final dt = DateTime.tryParse(t);
    if (dt != null) return DateTime(dt.year, dt.month, dt.day);
    final parts = t.split(RegExp(r'[T\s]')).first.split(RegExp(r'[-/.]'));
    if (parts.length == 3) {
      final a = int.tryParse(parts[0]);
      final b = int.tryParse(parts[1]);
      final c = int.tryParse(parts[2]);
      if (a == null || b == null || c == null) return null;
      if (parts[0].length == 4) return DateTime(a, b, c);
      if (parts[2].length == 4) return DateTime(c, b, a);
    }
    return null;
  }

  /// Null when not enough data to show a meaningful rating on the card.
  /// Score is one of: 10, 5, 0, -5, -10.
  static int? scoreForClient(Client client, [DateTime? asOf]) {
    final sale = parseDate(client.dateSaleDevice);
    if (client.salePrice <= 0 || sale == null) return null;
    return evaluate(
      amountTotalSales: client.salePrice,
      receiptsTotal: client.receipt,
      amountRemaining: client.remaining,
      dateSaleDevice: sale,
      lastPaymentDate: parseDate(client.lastPaymentDate),
      asOf: asOf ?? IraqDate.calendarDate(),
    ).score;
  }

  /// Same score as the customer card, from a local SQLite Customer row.
  /// Null when sale data is missing (row must not enter the average).
  static int? scoreForRow(Map<String, dynamic> row, [DateTime? asOf]) {
    final sale = parseDate(row['DateSaleDevice']?.toString());
    final total = SafeNum.toDouble(row['AmountTotalSales']);
    if (sale == null || total <= 0) return null;
    return evaluate(
      amountTotalSales: total,
      receiptsTotal: SafeNum.toDouble(row['ReceiptsTotal']),
      amountRemaining: SafeNum.toDouble(row['AmountRemaining']),
      dateSaleDevice: sale,
      lastPaymentDate: parseDate(row['LastPaymentDate']?.toString()),
      asOf: asOf ?? IraqDate.calendarDate(),
    ).score;
  }

  static ({String label, int score}) evaluate({
    required double amountTotalSales,
    required double receiptsTotal,
    required double amountRemaining,
    required DateTime dateSaleDevice,
    DateTime? lastPaymentDate,
    required DateTime asOf,
  }) {
    if (amountRemaining < 0 || amountTotalSales <= 0) {
      return (label: weak, score: 0);
    }

    final saleDate = adjustSaleDate(dateSaleDevice);
    final asOfDay = DateTime(asOf.year, asOf.month, asOf.day);

    if (amountRemaining == 0) {
      if (lastPaymentDate == null) return (label: weak, score: 0);
      final days = diffDaysInclusive(saleDate, lastPaymentDate);
      if (days <= 0) return (label: weak, score: 0);
      final c = classifySettledDays(days);
      return (label: c.$1, score: c.$2);
    }

    final daysSinceSale = diffDaysInclusive(saleDate, asOfDay);
    if (daysSinceSale <= 0) return (label: weak, score: 0);
    final rate =
        receiptsTotal * 1000.0 / (amountTotalSales * daysSinceSale);
    final c = classifyActiveRate(rate);
    return (label: c.$1, score: c.$2);
  }

  /// Cohort for «تقييمك»: open balance + last payment within 30 days
  /// (same as BE ActiveCustomerRules / Follower ListStats).
  static bool isInOverallRatingCohort(
    Map<String, dynamic> row, [
    DateTime? asOf,
  ]) {
    final asOfDay = asOf ?? IraqDate.calendarDate();
    final remaining = SafeNum.toDouble(row['AmountRemaining']);
    if (remaining <= 0) return false;
    final lastPay = parseDate(row['LastPaymentDate']?.toString());
    if (lastPay == null) return false;
    final cutoff = asOfDay.subtract(const Duration(days: 30));
    return !lastPay.isBefore(cutoff);
  }

  /// Overall «تقييمك»: مجموع تقييم الزبائن (داخل المعادلة) ÷ عددهم.
  ///
  /// Only customers with remaining &gt; 0 and a payment in the last 30 days.
  /// No recent payment → excluded (does not drag the average). Empty → 0.
  static double averageCustomerScore(
    Iterable<Map<String, dynamic>> customerRows, [
    DateTime? asOf,
  ]) {
    final asOfDay = asOf ?? IraqDate.calendarDate();
    var sum = 0;
    var count = 0;
    for (final row in customerRows) {
      if (!isInOverallRatingCohort(row, asOfDay)) continue;
      final score = scoreForRow(row, asOfDay);
      if (score == null) continue;
      sum += score;
      count++;
    }
    if (count == 0) return 0;
    return sum / count;
  }

  /// Discrete band mapping for labels only — never use this to replace «تقييمك».
  static int scoreFromAverage(double averageScore) {
    if (averageScore >= 7) return 10;
    if (averageScore >= 3) return 5;
    if (averageScore >= 0) return 0;
    if (averageScore > -7.5) return -5;
    return -10;
  }

  static String classifyAverage(double averageScore) {
    final s = scoreFromAverage(averageScore);
    if (s == 10) return excellent;
    if (s == 5) return good;
    if (s == 0) return weak;
    if (s == -5) return rejected;
    return bad;
  }

  /// Formats average for home: whole numbers without decimals (e.g. -10), else 2 dp.
  static String formatAverageScore(double average) {
    final rounded = double.parse(average.toStringAsFixed(2));
    if (rounded == rounded.roundToDouble()) {
      return rounded.round().toString();
    }
    return rounded.toStringAsFixed(2);
  }

  /// Customer card text: «التقييم : -10».
  static String formatScore(int score) => 'التقييم : $score';
}
