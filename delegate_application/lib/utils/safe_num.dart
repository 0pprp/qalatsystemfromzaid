/// Null-safe numeric coercion for SQLite / JSON customer rows.
/// Production hardening: backend DTOs allow null amounts.
class SafeNum {
  static double toDouble(dynamic value, [double fallback = 0]) {
    if (value == null) return fallback;
    if (value is num) return value.toDouble();
    final raw = value.toString().trim().replaceAll(',', '');
    if (raw.isEmpty) return fallback;
    return double.tryParse(raw) ?? fallback;
  }

  static int toInt(dynamic value, [int fallback = 0]) {
    if (value == null) return fallback;
    if (value is int) return value;
    if (value is num) return value.toInt();
    return int.tryParse(value.toString().trim()) ?? fallback;
  }
}
