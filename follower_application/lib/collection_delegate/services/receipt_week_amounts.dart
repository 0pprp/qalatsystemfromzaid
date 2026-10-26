/// Simplified week labels/amounts for follower collection receipts.
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

  /// Printed receipt identity from device count (same as delegate).
  static String printedReceiptNumber(String? countReceiptDevice) {
    final n = int.tryParse((countReceiptDevice ?? '').trim()) ?? 0;
    return '${n <= 0 ? 1 : n + 1}';
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
}
