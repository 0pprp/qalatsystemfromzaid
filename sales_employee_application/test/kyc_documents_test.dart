import 'package:flutter_test/flutter_test.dart';
import 'package:sales_employee_application/data/mock_sales_repository.dart';
import 'package:sales_employee_application/data/sales_models.dart';

void main() {
  test('residence card front and back are independent document types', () async {
    final repo = MockSalesRepository();
    await repo.uploadCustomerDocument(7, SalesCustomerKycDocument.residenceCardFront, [1], 'front.jpg');
    await repo.uploadCustomerDocument(7, SalesCustomerKycDocument.residenceCardBack, [2], 'back.jpg');

    final rows = await repo.listCustomerDocuments(7);
    expect(rows.length, 2);
    expect(rows.map((r) => r.documentType), containsAll([
      SalesCustomerKycDocument.residenceCardFront,
      SalesCustomerKycDocument.residenceCardBack,
    ]));
    expect(
      rows.firstWhere((r) => r.documentType == SalesCustomerKycDocument.residenceCardFront).typeLabel,
      'بطاقة السكن - أمامية',
    );
    expect(
      rows.firstWhere((r) => r.documentType == SalesCustomerKycDocument.residenceCardBack).typeLabel,
      'بطاقة السكن - خلفية',
    );
  });

  test('uploading residence card front does not replace the back', () async {
    final repo = MockSalesRepository();
    final back = await repo.uploadCustomerDocument(9, SalesCustomerKycDocument.residenceCardBack, [1], 'back.jpg');
    await repo.uploadCustomerDocument(9, SalesCustomerKycDocument.residenceCardFront, [2], 'front.jpg');
    await repo.uploadCustomerDocument(9, SalesCustomerKycDocument.residenceCardFront, [3], 'front2.jpg');

    final rows = await repo.listCustomerDocuments(9);
    expect(rows.length, 2);
    expect(rows.where((r) => r.documentType == SalesCustomerKycDocument.residenceCardBack).single.id, back.id);
    expect(rows.where((r) => r.documentType == SalesCustomerKycDocument.residenceCardFront).length, 1);
  });

  test('legacy residence card remains labeled as old', () {
    expect(SalesCustomerKycDocument.labelFor(SalesCustomerKycDocument.residenceCardLegacy), 'بطاقة السكن - قديمة');
    expect(SalesCustomerKycDocument.labelFor(SalesCustomerKycDocument.residenceCardFront), 'بطاقة السكن - أمامية');
    expect(SalesDocument(type: 'SaleDocuments', fileName: 'x.pdf', downloadUrl: '').displayTitle, 'عقد البيع + وصل الأمانة');
  });
}
