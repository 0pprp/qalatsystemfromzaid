class IraqPhone {
  static String normalize(String? raw) {
    var digits = (raw ?? '').replaceAll(RegExp(r'\D'), '');
    if (digits.startsWith('964') && digits.length >= 13) {
      digits = '0${digits.substring(3)}';
    }
    if (digits.length > 11) digits = digits.substring(0, 11);
    return digits;
  }

  static bool isValid(String? raw) => RegExp(r'^07\d{9}$').hasMatch(normalize(raw));

  static String? validator(String? value) {
    final raw = normalize(value);
    if (raw.isEmpty) return 'مطلوب';
    if (!isValid(raw)) return 'يجب أن يكون 11 رقم ويبدأ بـ 07';
    return null;
  }
}
