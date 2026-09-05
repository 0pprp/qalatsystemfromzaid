import 'package:flutter_test/flutter_test.dart';
import 'package:sales_employee_application/data/sales_models.dart';
import 'package:sales_employee_application/services/official_contract/contract_text.dart';
import 'package:sales_employee_application/services/official_contract/deposit_receipt_text.dart';
import 'package:sales_employee_application/services/official_contract/sale_document_map.dart';

void main() {
  SalesDraft draft() => SalesDraft(
        saleId: 1,
        fullName: 'أحمد علي محمد',
        phone: '07701234567',
        province: 'النجف',
        nationalCardNumber: 'N1234567',
        address: 'حي الأنصار',
        nearestLandmark: 'قرب جامع الأنصار',
        mukhtarName: 'حسن كاظم',
        rationCenterNumber: '4412',
        employeeName: 'موظف تجريبي',
        status: 'Completed',
        evaluationLevel: 5,
        evaluationNote: 'جيد',
        baseSalePrice: 1500000,
        finalSalePrice: 1500000,
        dailyInstallment: 25000,
        createdAt: DateTime(2026, 9, 2),
        completedAt: DateTime(2026, 9, 5),
        items: [
          SalesDraftItem(productId: 5, quantity: 1, productName: 'ثلاجة سامسونج 18 قدم'),
        ],
      );

  test('maps sale data into official contract fields without blanks', () {
    final mapped = SaleDocumentMap.fromDraft(draft());
    expect(mapped.customerName, 'أحمد علي محمد');
    expect(mapped.phone, '07701234567');
    expect(mapped.whatsapp, '07701234567');
    expect(mapped.nationalId, 'N1234567');
    expect(mapped.province, 'النجف');
    expect(mapped.address, 'حي الأنصار قرب جامع الأنصار');
    expect(mapped.guarantorName, 'حسن كاظم');
    expect(mapped.rationCenterNumber, '4412');
    expect(mapped.goodsType, 'ثلاجة سامسونج 18 قدم عدد 1');
    expect(mapped.totalPriceNumeric, '1500000');
    expect(mapped.dailyInstallmentNumeric, '25000');
    expect(mapped.salesRepName, 'موظف تجريبي');
    expect(mapped.cashierName, isEmpty);
    expect(mapped.totalPriceWords, isNotEmpty);
    expect(mapped.dailyInstallmentWords, isNotEmpty);
  });

  test('contract paragraphs keep official legal text and sale values', () {
    final text = ContractText.buildParagraphs(SaleDocumentMap.fromDraft(draft()))
        .map((p) => p.plainText)
        .join('\n');
    expect(text, contains('الطرف الأول شركة قلعة الضمان للتجارة العامة محدودة المسؤولية.'));
    expect(text, contains('أحمد علي محمد'));
    expect(text, contains('N1234567'));
    expect(text, contains('النجف'));
    expect(text, contains('حي الأنصار قرب جامع الأنصار'));
    expect(text, contains('07701234567'));
    expect(text, contains('حسن كاظم'));
    expect(text, contains('4412'));
    expect(text, contains('ثلاجة سامسونج 18 قدم عدد 1'));
    expect(text, contains('1,500,000'));
    expect(text, contains('25,000'));
    expect(text, contains('هويته وبطاقة سكنه وبطاقة تموينه'));
    expect(text, contains('أني الموقع ادناه'));
    expect(text, contains('موظف تجريبي'));
    expect(text, contains('أمين صندوق الفرع اشهد بان مندوب المبيعات قد وقع امامي.'));
    expect(text, isNot(contains('الطرف الأول:')));
  });

  test('receipt paragraphs keep official labels and sale values', () {
    final text = DepositReceiptText.buildBodyParagraphs(contract: SaleDocumentMap.fromDraft(draft()))
        .map((p) => p.plainText)
        .join('\n');
    expect(text, contains('المبلغ رقما: '));
    expect(text, contains('كتابــــــــــة: '));
    expect(text, contains('أسم المستلم: '));
    expect(text, contains('أحمد علي محمد'));
    expect(text, contains('N1234567'));
    expect(text, contains('حسن كاظم'));
    expect(text, contains('حي الأنصار قرب جامع الأنصار'));
    expect(text, contains('إني الموقع أدناه أقر واعترف باني مدين'));
  });
}
