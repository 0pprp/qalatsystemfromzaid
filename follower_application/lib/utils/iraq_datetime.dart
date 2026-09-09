/// Safe date/number display helpers — never calls DateFormat/NumberFormat,
/// so LocaleDataException cannot crash the UI regardless of main() init / hot reload.
class IraqDateTime {
  /// Baghdad is UTC+3 year-round (no DST). Asia/Baghdad.
  static const iraqOffset = Duration(hours: 3);

  /// Formats a UTC (or parseable) instant for note display. Never throws.
  static String formatClock(dynamic utc) {
    try {
      final raw = utc?.toString().trim();
      if (raw == null || raw.isEmpty) return '—';
      final parsed = DateTime.tryParse(raw)?.toUtc();
      if (parsed == null) return '—';
      return _manualAr(parsed.add(iraqOffset));
    } catch (_) {
      return '—';
    }
  }

  /// yyyy-MM-dd for API / filters. Never throws.
  static String formatYmd(DateTime date) {
    try {
      final y = date.year.toString().padLeft(4, '0');
      final m = date.month.toString().padLeft(2, '0');
      final d = date.day.toString().padLeft(2, '0');
      return '$y-$m-$d';
    } catch (_) {
      return '—';
    }
  }

  static String _manualAr(DateTime iraq) {
    final y = iraq.year.toString().padLeft(4, '0');
    final m = iraq.month.toString().padLeft(2, '0');
    final d = iraq.day.toString().padLeft(2, '0');
    var hour = iraq.hour % 12;
    if (hour == 0) hour = 12;
    final min = iraq.minute.toString().padLeft(2, '0');
    final period = iraq.hour >= 12 ? 'مساءً' : 'صباحاً';
    final h = hour.toString().padLeft(2, '0');
    return '$y/$m/$d $h:$min $period';
  }

  /// Number formatting that never crashes the page.
  static String formatNumber(dynamic v) {
    try {
      final n = double.tryParse('${v ?? ''}');
      if (n == null) return '—';
      final asInt = n == n.roundToDouble();
      final raw = asInt ? n.round().toString() : n.toStringAsFixed(0);
      final buf = StringBuffer();
      final chars = raw.split('').reversed.toList();
      for (var i = 0; i < chars.length; i++) {
        if (i > 0 && i % 3 == 0) buf.write(',');
        buf.write(chars[i]);
      }
      return buf.toString().split('').reversed.join();
    } catch (_) {
      return '—';
    }
  }
}
