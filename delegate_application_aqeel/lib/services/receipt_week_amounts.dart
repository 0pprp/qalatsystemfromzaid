import 'package:delegate_application/utils/iraq_date.dart';

/// Builds last-7-day receipt amounts from dated facts + local unsynced payments.
///
/// Contract (matches GetDateWeek / View_WeekFinal):
/// index 0 = today, 1 = yesterday, … 6 = six days ago.
class ReceiptWeekAmounts {
  static const arabicDayLabels = <String>[
    'اليوم',
    'أمس',
    'قبل يومين',
    'قبل 3 أيام',
    'قبل 4 أيام',
    'قبل 5 أيام',
    'قبل 6 أيام',
  ];

  /// Ensures 7 display labels; prefers DateWeek dates when present, else Arabic.
  static List<String> ensureDayLabels(List<String?>? raw) {
    final out = <String>[];
    for (var i = 0; i < 7; i++) {
      final v = (raw != null && i < raw.length) ? (raw[i] ?? '').trim() : '';
      out.add(v.isNotEmpty ? v : arabicDayLabels[i]);
    }
    return out;
  }

  static List<double> ensureAmounts(List<double?>? raw) {
    final out = <double>[];
    for (var i = 0; i < 7; i++) {
      final v = (raw != null && i < raw.length) ? (raw[i] ?? 0) : 0;
      out.add(v.isFinite ? v.toDouble() : 0.0);
    }
    return out;
  }

  /// Sum of local queue amounts that belong on the **today** business day.
  static double sumLocalTodayPayments({
    required Iterable<Map<String, dynamic>> localPayments,
    required int customerId,
    DateTime? utcNow,
  }) {
    final day = IraqDate.businessDateKey(utcNow);
    var sum = 0.0;
    for (final row in localPayments) {
      final cid = int.tryParse(row['CustomerId']?.toString() ?? '') ?? 0;
      if (cid != customerId) continue;
      final created = row['CreatedAtUtc']?.toString();
      DateTime? at;
      if (created != null && created.isNotEmpty) {
        at = DateTime.tryParse(created)?.toUtc();
      }
      // Missing CreatedAtUtc → treat as today (just inserted locally).
      final key = at == null ? day : IraqDate.businessDateKey(at);
      if (key != day) continue;
      final amount = (row['Amount'] as num?)?.toDouble() ?? 0;
      if (amount > 0) sum += amount;
    }
    return sum;
  }

  /// Server week + all local today payments for this customer (idempotent vs snapshot).
  ///
  /// If [alreadyIncludedInServerToday] is true (server Amount1 already contains this
  /// payment), only other local pending rows are added.
  static List<double> buildWeekForReceipt({
    required List<double> serverAmounts,
    required List<String> dayLabels,
    required Iterable<Map<String, dynamic>> localPayments,
    required int customerId,
    double? extraPaymentJustSaved,
    bool assumeExtraAlreadyInLocalRows = true,
    DateTime? utcNow,
  }) {
    ensureDayLabels(dayLabels);
    final amounts = ensureAmounts(serverAmounts);
    final localToday = sumLocalTodayPayments(
      localPayments: localPayments,
      customerId: customerId,
      utcNow: utcNow,
    );

    // Prefer max(serverToday, localToday) then add any just-saved amount not yet in query.
    var today = amounts[0];
    if (localToday > today) {
      today = localToday;
    }
    if (extraPaymentJustSaved != null &&
        extraPaymentJustSaved > 0 &&
        !assumeExtraAlreadyInLocalRows) {
      today += extraPaymentJustSaved;
    }
    amounts[0] = today;
    return amounts;
  }

  /// Merges [paymentAmount] into **today's slot (index 0)**.
  /// Never doubles when [alreadyIncluded] is true.
  static List<double> mergeLocalPayment({
    required List<double> serverAmounts,
    required List<String> dayLabels,
    required double paymentAmount,
    DateTime? utcNow,
    bool alreadyIncluded = false,
  }) {
    final amounts = ensureAmounts(serverAmounts);
    ensureDayLabels(dayLabels);
    if (paymentAmount <= 0 || alreadyIncluded) {
      return amounts;
    }
    amounts[0] = amounts[0] + paymentAmount;
    return amounts;
  }

  /// Final safety for PDF: today's row must show at least [amountPush].
  static List<double> guaranteeTodayShowsPayment({
    required List<double> weekAmounts,
    required double amountPush,
  }) {
    final amounts = ensureAmounts(weekAmounts);
    if (amountPush > 0 && amounts[0] + 0.0001 < amountPush) {
      amounts[0] = amountPush;
    }
    return amounts;
  }

  /// Formats IQD for display/PDF without rounding to thousands.
  static String formatExactIqd(double amount) {
    final whole = amount.round();
    final s = whole.toString();
    final buf = StringBuffer();
    for (var i = 0; i < s.length; i++) {
      final fromEnd = s.length - i;
      buf.write(s[i]);
      if (fromEnd > 1 && fromEnd % 3 == 1) buf.write(',');
    }
    return buf.toString();
  }

  static String printedReceiptNumber(String? countReceiptDevice) {
    final raw = (countReceiptDevice ?? '').trim();
    if (raw.isEmpty) return '1';
    final n = int.tryParse(raw);
    if (n == null) return raw;
    return (n + 1).toString();
  }

  static String todayBusinessKey([DateTime? utcNow]) =>
      IraqDate.businessDateKey(utcNow);
}
