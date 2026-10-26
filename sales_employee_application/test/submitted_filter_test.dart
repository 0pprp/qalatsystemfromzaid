import 'package:flutter_test/flutter_test.dart';
import 'package:sales_employee_application/data/sales_models.dart';

bool isSold(SalesWorkRequest r) => r.status == 'Completed' || r.isSold;
bool isRejected(SalesWorkRequest r) => r.status == 'Rejected';
bool isInProgress(SalesWorkRequest r) => !isSold(r) && !isRejected(r);

SalesWorkRequest req(String status) => SalesWorkRequest(
      id: 1,
      customerName: 'Test',
      status: status,
      createdAtUtc: DateTime.utc(2026, 1, 1),
    );

void main() {
  test('submitted history filters: sold / rejected / in progress', () {
    final rows = [
      req('Completed'),
      req('Rejected'),
      req('New'),
      req('Assigned'),
      req('PreparedForSale'),
      req('Pending'),
      req('Inspected'),
    ];

    expect(rows.where(isSold).length, 1);
    expect(rows.where(isRejected).length, 1);
    expect(rows.where(isInProgress).length, 5);
  });
}
