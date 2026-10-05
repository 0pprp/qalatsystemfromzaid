import 'package:delegate_application/utils/thousands_separator_input_formatter.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('formatDigits inserts commas every 3 digits', () {
    expect(ThousandsSeparatorInputFormatter.formatDigits('1'), '1');
    expect(ThousandsSeparatorInputFormatter.formatDigits('12'), '12');
    expect(ThousandsSeparatorInputFormatter.formatDigits('123'), '123');
    expect(ThousandsSeparatorInputFormatter.formatDigits('1234'), '1,234');
    expect(ThousandsSeparatorInputFormatter.formatDigits('2150000'), '2,150,000');
    expect(ThousandsSeparatorInputFormatter.formatDigits('150000'), '150,000');
  });

  test('parseMoney strips commas', () {
    expect(ThousandsSeparatorInputFormatter.parseMoney('2,150,000'), 2150000);
    expect(ThousandsSeparatorInputFormatter.parseMoney('150,000'), 150000);
    expect(ThousandsSeparatorInputFormatter.parseMoney(''), isNull);
  });

  test('formatter updates text while typing', () {
    final f = ThousandsSeparatorInputFormatter();
    final next = f.formatEditUpdate(
      const TextEditingValue(text: '215'),
      const TextEditingValue(
        text: '2150',
        selection: TextSelection.collapsed(offset: 4),
      ),
    );
    expect(next.text, '2,150');
  });
}
