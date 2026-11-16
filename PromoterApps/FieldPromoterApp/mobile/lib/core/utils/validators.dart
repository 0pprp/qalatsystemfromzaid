/// Local validation mirroring the API rules (the server re-validates everything).
abstract final class Validators {
  static final RegExp iraqiMobile = RegExp(r'^07[3-9]\d{8}$');

  /// Arabic letters (incl. common extended letters and diacritics), no spaces or digits.
  static final RegExp _arabicWord = RegExp(r'^[ء-غف-ْٰ-ۓ]+$');
  static final RegExp _arabicWords = RegExp(r'^[ء-غف-ْٰ-ۓ ]+$');

  /// Converts Arabic-Indic / Persian digits to ASCII digits.
  static String normalizeDigits(String input) {
    const arabic = '٠١٢٣٤٥٦٧٨٩';
    const persian = '۰۱۲۳۴۵۶۷۸۹';
    final b = StringBuffer();
    for (final ch in input.runes) {
      final c = String.fromCharCode(ch);
      final ai = arabic.indexOf(c);
      final pi = persian.indexOf(c);
      if (ai >= 0) {
        b.write(ai);
      } else if (pi >= 0) {
        b.write(pi);
      } else {
        b.write(c);
      }
    }
    return b.toString();
  }

  /// Normalises what a user may type into the canonical `07XXXXXXXXX` form:
  /// `+964 770 123 4567`, `00964770…`, `964770…`, `770…`, `٠٧٧٠…`.
  /// Returns the digits as-is (not canonical) if it cannot be normalised.
  static String normalizePhone(String input) {
    var d = normalizeDigits(input).replaceAll(RegExp(r'[^\d]'), '');
    if (d.startsWith('00964')) d = d.substring(5);
    if (d.startsWith('964')) d = d.substring(3);
    if (d.length == 10 && d.startsWith('7')) d = '0$d';
    return d;
  }

  static bool isValidIraqiPhone(String input) => iraqiMobile.hasMatch(normalizePhone(input));

  static String? phone(String? value, {bool required = true}) {
    final v = value?.trim() ?? '';
    if (v.isEmpty) return required ? 'رقم الهاتف مطلوب' : null;
    if (!isValidIraqiPhone(v)) {
      return 'أدخل رقماً عراقياً صحيحاً مثل 07701234567';
    }
    return null;
  }

  static String? required(String? value, [String message = 'هذا الحقل مطلوب']) {
    if (value == null || value.trim().isEmpty) return message;
    return null;
  }

  /// First / father / grandfather name: required, a single Arabic word.
  static String? nameWord(String? value, {bool required = true}) {
    final v = value?.trim() ?? '';
    if (v.isEmpty) return required ? 'مطلوب' : null;
    if (v.contains(RegExp(r'\s'))) return 'اكتب اسماً واحداً فقط بدون مسافات';
    if (!_arabicWord.hasMatch(v)) return 'استخدم الحروف العربية فقط';
    if (v.length < 2) return 'الاسم قصير جداً';
    return null;
  }

  /// Surname (اللقب): optional, Arabic letters, may contain spaces (e.g. «آل محمد»).
  static String? surname(String? value) {
    final v = value?.trim() ?? '';
    if (v.isEmpty) return null;
    if (!_arabicWords.hasMatch(v)) return 'استخدم الحروف العربية فقط';
    return null;
  }

  static String? otp(String? value) {
    final v = normalizeDigits(value ?? '');
    if (!RegExp(r'^\d{6}$').hasMatch(v)) return 'أدخل الرمز المكوّن من 6 أرقام';
    return null;
  }
}
