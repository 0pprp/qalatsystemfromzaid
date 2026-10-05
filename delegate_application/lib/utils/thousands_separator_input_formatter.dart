import 'package:flutter/services.dart';

/// Formats digits with a comma every 3 places while typing (e.g. 2150000 → 2,150,000).
class ThousandsSeparatorInputFormatter extends TextInputFormatter {
  @override
  TextEditingValue formatEditUpdate(
    TextEditingValue oldValue,
    TextEditingValue newValue,
  ) {
    final digits = newValue.text.replaceAll(RegExp(r'[^0-9]'), '');
    if (digits.isEmpty) {
      return const TextEditingValue(
        text: '',
        selection: TextSelection.collapsed(offset: 0),
      );
    }

    final formatted = formatDigits(digits);
    return TextEditingValue(
      text: formatted,
      selection: TextSelection.collapsed(offset: formatted.length),
    );
  }

  /// `2150000` → `2,150,000`
  static String formatDigits(String digits) {
    final raw = digits.replaceAll(RegExp(r'[^0-9]'), '');
    if (raw.isEmpty) return '';
    final buf = StringBuffer();
    for (var i = 0; i < raw.length; i++) {
      final fromEnd = raw.length - i;
      buf.write(raw[i]);
      if (fromEnd > 1 && fromEnd % 3 == 1) buf.write(',');
    }
    return buf.toString();
  }

  /// Parse typed money that may contain commas/spaces.
  static double? parseMoney(String? raw) {
    final t = (raw ?? '').trim().replaceAll(',', '').replaceAll(' ', '');
    if (t.isEmpty) return null;
    return double.tryParse(t);
  }
}
