/// Shared presentation formatters for Delegated Manager business UI.
class DmFormat {
  /// DATE ONLY (no time), Iraq-local when timestamp has zone.
  static String dateOnly(DateTime? value) {
    if (value == null) return 'غير متوفر';
    final local = value.isUtc ? value.toLocal() : value;
    final d = local.day.toString().padLeft(2, '0');
    final m = local.month.toString().padLeft(2, '0');
    final y = local.year.toString();
    return '$d/$m/$y';
  }

  /// Thousands separators + optional IQD suffix.
  static String money(num? value, {bool withCurrency = true}) {
    if (value == null) return 'غير متوفر';
    final n = value.round();
    final negative = n < 0;
    final digits = negative ? (-n).toString() : n.toString();
    final buf = StringBuffer();
    for (var i = 0; i < digits.length; i++) {
      final fromEnd = digits.length - i;
      buf.write(digits[i]);
      if (fromEnd > 1 && fromEnd % 3 == 1) buf.write(',');
    }
    final formatted = '${negative ? '-' : ''}$buf';
    return withCurrency ? '$formatted د.ع' : formatted;
  }

  /// Never show DatabaseCompany* / internal keys.
  static String friendlyCity(String? cityName, [String? cityValue]) {
    final candidates = [cityName, cityValue];
    for (final c in candidates) {
      final t = (c ?? '').trim();
      if (t.isEmpty) continue;
      if (_isInternal(t)) continue;
      return t;
    }
    final mapped = _mapLegacy(cityValue) ?? _mapLegacy(cityName);
    return mapped ?? 'غير متوفر';
  }

  static bool _isInternal(String text) {
    if (text.toLowerCase().startsWith('database')) return true;
    if (RegExp(r'[-_]demo$', caseSensitive: false).hasMatch(text) &&
        !RegExp(r'[\u0600-\u06FF]').hasMatch(text)) {
      return true;
    }
    // Bare latin city slugs (najaf, basra, …) when they map via legacy table.
    if (!RegExp(r'[\u0600-\u06FF]').hasMatch(text) && _mapLegacy(text) != null) {
      return true;
    }
    return false;
  }

  static String? _mapLegacy(String? value) {
    if (value == null || value.trim().isEmpty) return null;
    var key = value.trim();
    if (RegExp(r'_DEMO$', caseSensitive: false).hasMatch(key)) {
      key = key.substring(0, key.length - 5);
    }
    switch (key.toLowerCase()) {
      case 'databasecompanynajaf':
      case 'najaf-demo':
      case 'najaf':
        return 'النجف';
      case 'databasecompanybasra':
      case 'basra-demo':
      case 'basra':
        return 'البصرة';
      case 'databasecompanybaghdadkarak':
      case 'karkh-demo':
      case 'karkh':
        return 'الكرخ';
      case 'databasecompanybaghdadrosafa':
      case 'rusafa-demo':
      case 'rosafa':
      case 'rusafa':
        return 'الرصافة';
      case 'databasecompanykarbala':
      case 'karbala-demo':
      case 'karbala':
        return 'كربلاء';
      default:
        return null;
    }
  }
}
