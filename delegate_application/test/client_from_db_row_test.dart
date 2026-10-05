import 'package:delegate_application/client.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('Client.fromDbRow never throws on null numeric fields', () {
    final c = Client.fromDbRow({
      'CustomerId': null,
      'CustomerName': 'A',
      'AmountTotalSales': null,
      'AmountDaySales': null,
      'ReceiptsTotal': null,
      'AmountRemaining': null,
      'Amount1': null,
      'Amount2': null,
      'Amount3': null,
      'Amount4': null,
      'Amount5': null,
      'Amount6': null,
      'Amount7': null,
    });
    expect(c.customerId, 0);
    expect(c.salePrice, 0);
    expect(c.remaining, 0);
    expect(c.amount1, 0);
    expect(c.name, 'A');
  });
}
