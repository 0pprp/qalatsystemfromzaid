import 'package:delegate_application/utils/iraq_date.dart';

/// Builds last-7-day receipt amounts from dated facts + local unsynced payment.
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

  /// Merges [paymentAmount] into **today's slot (index 0)**.
  /// Never doubles when [alreadyIncluded] is true.
  ///
  /// Optional [dayLabels] are for display only; financial today is always index 0
  /// per server Amount1 contract — label heuristics must not relocate the amount.
  static List<double> mergeLocalPayment({
    required List<double> serverAmounts,
    required List<String> dayLabels,
    required double paymentAmount,
    DateTime? utcNow,
    bool alreadyIncluded = false,
  }) {
    final amounts = ensureAmounts(serverAmounts);
    // Keep analyzer/API compat — labels do not move the today slot.
    ensureDayLabels(dayLabels);
    if (paymentAmount <= 0 || alreadyIncluded) {
      return amounts;
    }

    // Absolute rule: Amount1 / first receipt row = today.
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
      // Local collection not reflected in snapshot — show the paid amount today.
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

  /// Safe printed receipt number: prefer parsed device count + 1 once.
  static String printedReceiptNumber(String? countReceiptDevice) {
    final raw = (countReceiptDevice ?? '').trim();
    if (raw.isEmpty) return '1';
    final n = int.tryParse(raw);
    if (n == null) return raw; // UUID / non-numeric — print as-is, never int.parse
    return (n + 1).toString();
  }

  /// Business-date key for diagnostics / future dated merges.
  static String todayBusinessKey([DateTime? utcNow]) =>
      IraqDate.businessDateKey(utcNow);
}
