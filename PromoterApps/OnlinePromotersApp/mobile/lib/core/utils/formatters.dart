import 'package:intl/intl.dart';

/// Money and date formatting. Money is integer IQD everywhere in the contract.
abstract final class Fmt {
  static final NumberFormat _number = NumberFormat('#,##0', 'en_US');

  static const String currency = 'د.ع';

  /// `650000` → `650,000`
  static String number(num value) => _number.format(value);

  /// `650000` → `650,000 د.ع`
  static String money(num value) {
    final negative = value < 0;
    final text = '${_number.format(value.abs())} $currency';
    return negative ? '- $text' : text;
  }

  /// Iraqi (Levantine) month names, as commonly used in Iraq.
  static const List<String> iraqiMonths = [
    'كانون الثاني',
    'شباط',
    'آذار',
    'نيسان',
    'أيار',
    'حزيران',
    'تموز',
    'آب',
    'أيلول',
    'تشرين الأول',
    'تشرين الثاني',
    'كانون الأول',
  ];

  /// Iraq time (UTC+3 all year, no daylight saving), whatever time zone the phone is set to.
  static DateTime iraq(DateTime value) => value.toUtc().add(const Duration(hours: 3));

  /// `2026-11-03T10:15:00Z` → `3 تشرين الثاني 2026` (Iraq time).
  static String date(DateTime? value) {
    if (value == null) return '—';
    final d = iraq(value);
    return '${d.day} ${iraqiMonths[d.month - 1]} ${d.year}';
  }

  /// `3 تشرين الثاني 2026 - 1:15 م`
  static String dateTime(DateTime? value) {
    if (value == null) return '—';
    final d = iraq(value);
    final h12 = d.hour % 12 == 0 ? 12 : d.hour % 12;
    final minute = d.minute.toString().padLeft(2, '0');
    final period = d.hour < 12 ? 'ص' : 'م';
    return '${d.day} ${iraqiMonths[d.month - 1]} ${d.year} - $h12:$minute $period';
  }

  /// `8:05 ص` (Iraq time).
  static String time(DateTime? value) {
    if (value == null) return '—';
    final d = iraq(value);
    final h12 = d.hour % 12 == 0 ? 12 : d.hour % 12;
    return '$h12:${d.minute.toString().padLeft(2, '0')} ${d.hour < 12 ? 'ص' : 'م'}';
  }

  /// Worked time: `5 س 07 د`, `45 د`.
  static String duration(Duration d) {
    final minutes = d.inMinutes < 0 ? 0 : d.inMinutes;
    final h = minutes ~/ 60;
    final m = minutes % 60;
    return h == 0 ? '$m د' : '$h س ${m.toString().padLeft(2, '0')} د';
  }

  /// `2026-11` → `تشرين الثاني 2026`
  static String month(String yyyyMm) {
    final parts = yyyyMm.split('-');
    if (parts.length < 2) return yyyyMm;
    final y = int.tryParse(parts[0]);
    final m = int.tryParse(parts[1]);
    if (y == null || m == null || m < 1 || m > 12) return yyyyMm;
    return '${iraqiMonths[m - 1]} $y';
  }

  /// Relative time for inbox items: "الآن", "قبل 5 دقائق", "أمس", or a date.
  static String relative(DateTime? value, {DateTime? now}) {
    if (value == null) return '';
    final n = iraq(now ?? DateTime.now());
    final d = iraq(value);
    final diff = n.difference(d);
    if (diff.inMinutes < 1) return 'الآن';
    if (diff.inMinutes < 60) return 'قبل ${_plural(diff.inMinutes, 'دقيقة', 'دقيقتين', 'دقائق')}';
    if (diff.inHours < 24) return 'قبل ${_plural(diff.inHours, 'ساعة', 'ساعتين', 'ساعات')}';
    if (diff.inDays == 1) return 'أمس';
    if (diff.inDays < 7) return 'قبل ${_plural(diff.inDays, 'يوم', 'يومين', 'أيام')}';
    return date(value);
  }

  static String _plural(int n, String one, String two, String few) {
    if (n == 1) return one;
    if (n == 2) return two;
    if (n >= 3 && n <= 10) return '$n $few';
    return '$n $one';
  }
}
