/// Date helpers without `intl`: the gateway sends UTC ISO-8601 strings.
class AppDate {
  static DateTime? parseUtc(dynamic value) {
    if (value == null) return null;
    if (value is DateTime) return value;
    final raw = value.toString().trim();
    if (raw.isEmpty) return null;
    final parsed = DateTime.tryParse(raw.endsWith('Z') ? raw : '${raw}Z');
    return parsed?.toLocal();
  }

  static String two(int n) => n.toString().padLeft(2, '0');

  static String day(DateTime value) =>
      '${value.year}/${two(value.month)}/${two(value.day)}';

  static String isoDate(DateTime value) =>
      '${value.year}-${two(value.month)}-${two(value.day)}';

  static String formatScore(double value) {
    var text = value.toStringAsFixed(2);
    if (!text.contains('.')) return text;
    text = text.replaceFirst(RegExp(r'0+$'), '');
    if (text.endsWith('.')) text = text.substring(0, text.length - 1);
    return text;
  }

  static String format(DateTime? value) {
    if (value == null) return '-';
    return '${day(value)} ${two(value.hour)}:${two(value.minute)}';
  }

  /// Iraq (+03) 12-hour clock with صباح/مساء.
  static String iraqDateTime12h(DateTime? value) {
    if (value == null) return '-';
    final utc = value.isUtc ? value : value.toUtc();
    final iraq = utc.add(const Duration(hours: 3));
    final h24 = iraq.hour;
    final period = h24 >= 12 ? 'مساء' : 'صباح';
    var h12 = h24 % 12;
    if (h12 == 0) h12 = 12;
    return '${day(iraq)} ${two(h12)}:${two(iraq.minute)} $period';
  }

  static String relative(DateTime? value, {DateTime? now}) {
    if (value == null) return '-';
    final reference = now ?? DateTime.now();
    final diff = reference.difference(value);
    if (diff.inMinutes < 1) return 'الآن';
    if (diff.inMinutes < 60) return 'قبل ${diff.inMinutes} دقيقة';
    if (diff.inHours < 24) return 'قبل ${diff.inHours} ساعة';
    if (diff.inDays < 30) return 'قبل ${diff.inDays} يوم';
    return format(value);
  }
}
