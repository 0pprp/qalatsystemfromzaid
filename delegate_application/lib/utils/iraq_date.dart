/// Iraq civil calendar helpers (Asia/Baghdad = UTC+3, no DST).
class IraqDate {
  static const Duration iraqOffset = Duration(hours: 3);

  static DateTime nowIraq([DateTime? utcNow]) {
    final utc = (utcNow ?? DateTime.now()).toUtc();
    return utc.add(iraqOffset);
  }

  /// [createdAtUtcIso] is typically CustomerPayment.CreatedAtUtc (ISO-8601 UTC).
  static bool isCreatedOnIraqDay(String? createdAtUtcIso, [DateTime? utcNow]) {
    if (createdAtUtcIso == null || createdAtUtcIso.trim().isEmpty) return false;
    DateTime parsed;
    try {
      parsed = DateTime.parse(createdAtUtcIso.trim());
    } catch (_) {
      return false;
    }
    final iraq = parsed.toUtc().add(iraqOffset);
    final today = nowIraq(utcNow);
    return iraq.year == today.year &&
        iraq.month == today.month &&
        iraq.day == today.day;
  }

  static String formatIraqTime(String? createdAtUtcIso) {
    if (createdAtUtcIso == null || createdAtUtcIso.trim().isEmpty) return '';
    try {
      final iraq = DateTime.parse(createdAtUtcIso.trim()).toUtc().add(iraqOffset);
      final h = iraq.hour.toString().padLeft(2, '0');
      final m = iraq.minute.toString().padLeft(2, '0');
      return '$h:$m';
    } catch (_) {
      return '';
    }
  }

  static String formatIraqDateTime(dynamic value) {
    if (value == null) return '';
    final raw = value is DateTime ? value.toUtc().toIso8601String() : '$value'.trim();
    if (raw.isEmpty) return '';
    try {
      final iraq = DateTime.parse(raw).toUtc().add(iraqOffset);
      final y = iraq.year.toString().padLeft(4, '0');
      final mo = iraq.month.toString().padLeft(2, '0');
      final d = iraq.day.toString().padLeft(2, '0');
      final h = iraq.hour.toString().padLeft(2, '0');
      final mi = iraq.minute.toString().padLeft(2, '0');
      return '$y-$mo-$d $h:$mi';
    } catch (_) {
      return raw;
    }
  }
}
