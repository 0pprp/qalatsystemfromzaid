import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:follower_application/utils/thousand_separator_formatter.dart';

void main() {
  test('adds comma after 3 digits', () {
    final f = ThousandSeparatorInputFormatter();
    final out = f.formatEditUpdate(
      TextEditingValue.empty,
      const TextEditingValue(
        text: '1234',
        selection: TextSelection.collapsed(offset: 4),
      ),
    );
    expect(out.text, '1,234');
  });

  test('parse strips commas', () {
    expect(ThousandSeparatorInputFormatter.parse('1,234,567'), 1234567);
  });
}
