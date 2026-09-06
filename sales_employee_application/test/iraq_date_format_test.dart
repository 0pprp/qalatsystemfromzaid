import 'package:flutter_test/flutter_test.dart';
import 'package:sales_employee_application/utils/iraq_time.dart';

void main() {
  test('formatDate uses YYYY/MM/DD without time', () {
    expect(IraqTime.formatDate(DateTime(2026, 9, 6, 18, 19)), '2026/09/06');
    expect(
      IraqTime.formatDate(DateTime.utc(2026, 9, 5, 21, 0)),
      '2026/09/06',
    );
    expect(IraqTime.formatDate(null), '');
  });
}
