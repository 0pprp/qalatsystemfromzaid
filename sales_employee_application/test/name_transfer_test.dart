import 'package:flutter_test/flutter_test.dart';
import 'package:sales_employee_application/data/sales_models.dart';

void main() {
  test('canTransferName blocks terminal statuses', () {
    SalesWorkRequest row(String status) => SalesWorkRequest(
          id: 1,
          customerName: 'ز',
          status: status,
          createdAtUtc: DateTime.utc(2026, 1, 1),
        );

    expect(row('New').canTransferName, isTrue);
    expect(row('Assigned').canTransferName, isTrue);
    expect(row('Pending').canTransferName, isTrue);
    expect(row('PreparedForSale').canTransferName, isTrue);
    expect(row('InProgress').canTransferName, isTrue);
    expect(row('ConvertedToSale').canTransferName, isTrue);
    expect(row('Inspected').canTransferName, isTrue);
    expect(row('Completed').canTransferName, isFalse);
    expect(row('Rejected').canTransferName, isFalse);
  });

  test('fromJson maps latest name transfer', () {
    final row = SalesWorkRequest.fromJson({
      'id': 9,
      'customerName': 'زبون',
      'status': 'Assigned',
      'createdAtUtc': '2026-01-01T00:00:00Z',
      'latestNameTransfer': {
        'fromEmployeeName': 'أحمد',
        'toEmployeeName': 'علي',
        'transferReason': 'خارج المنطقة',
        'transferredAtUtc': '2026-03-01T10:00:00Z',
      },
      'nameTransfers': [
        {
          'fromEmployeeName': 'أحمد',
          'toEmployeeName': 'علي',
          'transferReason': 'خارج المنطقة',
          'transferredAtUtc': '2026-03-01T10:00:00Z',
        }
      ],
    });

    expect(row.isTransferred, isTrue);
    expect(row.latestNameTransfer!.fromEmployeeName, 'أحمد');
    expect(row.latestNameTransfer!.transferReason, 'خارج المنطقة');
    expect(row.nameTransfers, hasLength(1));
    expect(row.availableActions, contains('transfer'));
  });
}
