/// Iraq civil calendar + collection business-day helpers.
/// Asia/Baghdad = UTC+3 (no DST).
///
/// Two independent clocks:
/// - [businessDayStartHour] 03:00 — UI «تسديدات اليوم» boundary
/// - [paymentSyncGateHour] 16:00 — PAYMENT HTTP upload gate (not UI day)
class IraqDate {
  static const Duration iraqOffset = Duration(hours: 3);

  /// Local hour (Baghdad) when a new business day begins.
  static const int businessDayStartHour = 3;

  /// Local hour (Baghdad) when pending payments become eligible for server upload.
  static const int paymentSyncGateHour = 16;

  static DateTime nowIraq([DateTime? utcNow]) {
    final utc = (utcNow ?? DateTime.now()).toUtc();
    return utc.add(iraqOffset);
  }

  /// Calendar Y-M-D for the collection business day in Baghdad.
  ///
  /// Window: [03:00, next-day 02:59:59].
  /// Example: 2026-09-17 02:30 → 2026-09-16; 03:00 → 2026-09-17.
  static DateTime businessDate([DateTime? utcNow]) {
    final iraq = nowIraq(utcNow);
    final localDay = DateTime(iraq.year, iraq.month, iraq.day);
    if (iraq.hour < businessDayStartHour) {
      return localDay.subtract(const Duration(days: 1));
    }
    return localDay;
  }

  static String formatBusinessDate([DateTime? utcNow]) {
    final d = businessDate(utcNow);
    final y = d.year.toString().padLeft(4, '0');
    final m = d.month.toString().padLeft(2, '0');
    final day = d.day.toString().padLeft(2, '0');
    return '$y-$m-$day';
  }

  /// Stable key for SharedPreferences / comparisons (yyyy-MM-dd).
  static String businessDateKey([DateTime? utcNow]) => formatBusinessDate(utcNow);

  /// True when pending payments may be HTTP-uploaded (Baghdad local time ≥ 16:00).
  /// Window each calendar day: [16:00, 23:59:59]. Closed [00:00, 15:59:59].
  static bool isPaymentSyncGateOpen([DateTime? utcNow]) {
    final iraq = nowIraq(utcNow);
    return iraq.hour >= paymentSyncGateHour;
  }

  /// Duration until the next Baghdad 16:00 (zero if gate already open).
  /// Uses wall-clock arithmetic only — never mixes DateTime kinds / device TZ.
  static Duration delayUntilPaymentSyncGate([DateTime? utcNow]) {
    final iraq = nowIraq(utcNow);
    if (iraq.hour >= paymentSyncGateHour) {
      return Duration.zero;
    }
    return Duration(
      hours: paymentSyncGateHour - iraq.hour,
      minutes: -iraq.minute,
      seconds: -iraq.second,
      milliseconds: -iraq.millisecond,
    );
  }

  /// [createdAtUtcIso] is typically CustomerPayment.CreatedAtUtc (ISO-8601 UTC).
  /// Matches the **business day** (03:00 boundary), not midnight calendar day.
  static bool isCreatedOnIraqDay(String? createdAtUtcIso, [DateTime? utcNow]) {
    if (createdAtUtcIso == null || createdAtUtcIso.trim().isEmpty) return false;
    DateTime parsed;
    try {
      parsed = DateTime.parse(createdAtUtcIso.trim());
    } catch (_) {
      return false;
    }
    final paymentBiz = businessDate(parsed.toUtc());
    final todayBiz = businessDate(utcNow);
    return paymentBiz.year == todayBiz.year &&
        paymentBiz.month == todayBiz.month &&
        paymentBiz.day == todayBiz.day;
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
