/// Mirrors `BE_SalesEmployee.DelegatedManager.Domain.MobileUpdateKind`.
enum MobileUpdateKind { none, optional, mandatory }

/// Client-side copy of `MobileUpdateRules.Evaluate` so the app can decide even
/// when the gateway response omits `updateKind`.
class MobileUpdateRules {
  static MobileUpdateKind evaluate({
    required int currentVersionCode,
    required int latestVersionCode,
    required int minimumSupportedVersionCode,
    required bool forceUpdate,
  }) {
    if (currentVersionCode >= latestVersionCode) return MobileUpdateKind.none;
    if (currentVersionCode < minimumSupportedVersionCode) {
      return MobileUpdateKind.mandatory;
    }
    if (forceUpdate) return MobileUpdateKind.mandatory;
    return MobileUpdateKind.optional;
  }

  static MobileUpdateKind parseKind(String? value) {
    switch (value?.trim().toLowerCase()) {
      case 'none':
        return MobileUpdateKind.none;
      case 'optional':
        return MobileUpdateKind.optional;
      case 'mandatory':
        return MobileUpdateKind.mandatory;
    }
    return MobileUpdateKind.none;
  }

  static String arabic(MobileUpdateKind kind) {
    switch (kind) {
      case MobileUpdateKind.none:
        return 'التطبيق محدّث';
      case MobileUpdateKind.optional:
        return 'يتوفر تحديث اختياري';
      case MobileUpdateKind.mandatory:
        return 'يتوفر تحديث إلزامي';
    }
  }

  /// Compares dotted version names (`1.2.10` > `1.2.9`). Returns a negative
  /// number when [left] is older, zero when equal, positive when newer.
  static int compareVersionNames(String left, String right) {
    final a = _segments(left);
    final b = _segments(right);
    final length = a.length > b.length ? a.length : b.length;
    for (var i = 0; i < length; i++) {
      final x = i < a.length ? a[i] : 0;
      final y = i < b.length ? b[i] : 0;
      if (x != y) return x < y ? -1 : 1;
    }
    return 0;
  }

  /// Build/prerelease metadata (`1.0.0+3`, `1.0.0-rc1`) is ignored: only the
  /// dotted release numbers are compared.
  static List<int> _segments(String value) {
    var text = value.trim();
    final cut = text.indexOf(RegExp(r'[+\-]'));
    if (cut > 0) text = text.substring(0, cut);
    return text
        .split('.')
        .map((part) =>
            int.tryParse(part.replaceAll(RegExp(r'[^0-9]'), '')) ?? 0)
        .toList();
  }
}
