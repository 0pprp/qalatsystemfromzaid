import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sales_employee_application/utils/sales_format.dart';

void main() {
  test('groups thousands for display', () {
    expect(MoneyFormat.grouped(1000), '1,000');
    expect(MoneyFormat.grouped(10000), '10,000');
    expect(MoneyFormat.grouped(1000000), '1,000,000');
    expect(MoneyFormat.grouped(12500000), '12,500,000');
    expect(MoneyFormat.iqd(1000), '1,000 د.ع');
  });

  test('parses grouped and arabic digits', () {
    expect(MoneyFormat.parse('1,000'), 1000);
    expect(MoneyFormat.parse('12,500,000'), 12500000);
    expect(MoneyFormat.parse('١٢٣٤'), 1234);
  });

  test('sales request inspected label', () {
    expect(SalesRequestStatusLabels.of('Inspected'), 'تم الكشف');
  });

  test('formatter keeps digit cursor', () {
    const formatter = MoneyInputFormatter();
    final next = formatter.formatEditUpdate(
      const TextEditingValue(text: '1,000', selection: TextSelection.collapsed(offset: 5)),
      const TextEditingValue(text: '1,0000', selection: TextSelection.collapsed(offset: 6)),
    );
    expect(next.text, '10,000');
    expect(next.selection.baseOffset, 6);
  });
}
