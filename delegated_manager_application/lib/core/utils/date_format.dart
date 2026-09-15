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

  static String format(DateTime? value) {
    if (value == null) return '-';
    String two(int n) => n.toString().padLeft(2, '0');
    return '${value.year}/${two(value.month)}/${two(value.day)}'
        ' ${two(value.hour)}:${two(value.minute)}';
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
