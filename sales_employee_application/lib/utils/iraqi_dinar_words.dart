class IraqiDinarWords {
  static const _ones = [
    '', 'واحد', 'اثنان', 'ثلاثة', 'أربعة', 'خمسة', 'ستة', 'سبعة', 'ثمانية', 'تسعة',
    'عشرة', 'أحد عشر', 'اثنا عشر', 'ثلاثة عشر', 'أربعة عشر', 'خمسة عشر', 'ستة عشر',
    'سبعة عشر', 'ثمانية عشر', 'تسعة عشر',
  ];
  static const _tens = [
    '', '', 'عشرون', 'ثلاثون', 'أربعون', 'خمسون', 'ستون', 'سبعون', 'ثمانون', 'تسعون',
  ];

  static String toArabic(num amount) {
    var value = amount.truncate();
    if (value < 0) value = 0;
    if (value == 0) return 'صفر دينار عراقي';
    return '${_convert(value)} دينار عراقي';
  }

  static String _convert(int n) {
    if (n < 20) return _ones[n];
    if (n < 100) {
      final ten = n ~/ 10;
      final one = n % 10;
      return one == 0 ? _tens[ten] : '${_ones[one]} و${_tens[ten]}';
    }
    if (n < 1000) {
      final h = n ~/ 100;
      final rest = n % 100;
      final hundred = switch (h) {
        1 => 'مائة',
        2 => 'مائتان',
        _ => '${_ones[h]}مائة',
      };
      return rest == 0 ? hundred : '$hundred و${_convert(rest)}';
    }
    if (n < 1000000) {
      final thousands = n ~/ 1000;
      final rest = n % 1000;
      final word = switch (thousands) {
        1 => 'ألف',
        2 => 'ألفان',
        >= 3 && <= 10 => '${_convert(thousands)} آلاف',
        _ => '${_convert(thousands)} ألف',
      };
      return rest == 0 ? word : '$word و${_convert(rest)}';
    }
    if (n < 1000000000) {
      final millions = n ~/ 1000000;
      final rest = n % 1000000;
      final word = switch (millions) {
        1 => 'مليون',
        2 => 'مليونان',
        >= 3 && <= 10 => '${_convert(millions)} ملايين',
        _ => '${_convert(millions)} مليون',
      };
      return rest == 0 ? word : '$word و${_convert(rest)}';
    }
    final billions = n ~/ 1000000000;
    final remainder = n % 1000000000;
    final billionWord = billions == 1 ? 'مليار' : '${_convert(billions)} مليار';
    return remainder == 0 ? billionWord : '$billionWord و${_convert(remainder)}';
  }
}
