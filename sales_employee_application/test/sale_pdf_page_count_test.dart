import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:sales_employee_application/data/sales_models.dart';
import 'package:sales_employee_application/services/official_contract/sale_document_map.dart';
import 'package:sales_employee_application/services/sale_documents.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  SalesDraft draft({bool longData = false}) => SalesDraft(
        saleId: longData ? 11 : 1,
        fullName: longData ? 'أحمد علي محمد حسن الجابري الخفاجي النجفي' : 'أحمد علي محمد',
        phone: '07701234567',
        province: longData ? 'النجف الأشرف' : 'النجف',
        nationalCardNumber: longData ? 'N1234567890123' : 'N1234567',
        address: longData ? 'محلة الأنصار زقاق 14 دار 27 قرب سوق الحسينية الجديدة' : 'حي الأنصار',
        nearestLandmark: longData ? 'بجانب جامع الأنصار مقابل مدرسة الفرات' : 'قرب جامع الأنصار',
        mukhtarName: longData ? 'حسن كاظم عبد الأمير الموسوي' : 'حسن كاظم',
        rationCenterNumber: longData ? '44127890' : '4412',
        employeeName: longData ? 'مندوب المبيعات عبد الله حسين كاظم الشمري' : 'موظف تجريبي',
        status: 'Completed',
        evaluationLevel: 5,
        evaluationNote: 'جيد',
        baseSalePrice: longData ? 12500000 : 1500000,
        finalSalePrice: longData ? 12500000 : 1500000,
        dailyInstallment: longData ? 85000 : 25000,
        downPayment: longData ? 625000 : 75000,
        createdAt: DateTime(2026, 9, 2),
        completedAt: DateTime(2026, 9, 5),
        items: longData
            ? [
                SalesDraftItem(productId: 1, quantity: 1, productName: 'ثلاجة سامسونج 18 قدم ستيل مع ضمان الشركة'),
                SalesDraftItem(productId: 2, quantity: 1, productName: 'غسالة ملابس أوتوماتيك 8 كيلو إل جي'),
                SalesDraftItem(productId: 3, quantity: 2, productName: 'مكيف هواء سبليت 1.5 طن بارد حار'),
              ]
            : [
                SalesDraftItem(productId: 5, quantity: 1, productName: 'ثلاجة سامسونج 18 قدم'),
              ],
      );

  test('contract and receipt PDFs are a single A4 page for normal data', () async {
    final mapped = SaleDocumentMap.fromDraft(draft());
    final contract = await (await SaleDocuments.buildContract(mapped)).save();
    final receipt = await (await SaleDocuments.buildReceipt(mapped)).save();
    expect(pdfPageCount(contract), 1);
    expect(pdfPageCount(receipt), 1);
  });

  test('combined sale documents PDF has two A4 pages', () async {
    final mapped = SaleDocumentMap.fromDraft(draft());
    final combined = await (await SaleDocuments.buildSaleDocuments(mapped)).save();
    expect(pdfPageCount(combined), 2);
    expect(SaleDocuments.debtorFingerprintHeight, 80);
  });

  test('receipt fingerprint space remains without a border box', () {
    final candidates = [
      File('lib/services/sale_documents.dart'),
      File('../lib/services/sale_documents.dart'),
    ];
    final srcFile = candidates.firstWhere((f) => f.existsSync());
    final src = srcFile.readAsStringSync();
    expect(src.contains('pw.Border.all'), isFalse);
    expect(src.contains('height: debtorFingerprintHeight'), isTrue);
    expect(src.contains('pw.SizedBox(height: 28)'), isTrue);
  });

  test('contract and receipt PDFs are a single A4 page for long data', () async {
    final mapped = SaleDocumentMap.fromDraft(draft(longData: true));
    final contract = await (await SaleDocuments.buildContract(mapped)).save();
    final receipt = await (await SaleDocuments.buildReceipt(mapped)).save();
    expect(pdfPageCount(contract), 1);
    expect(pdfPageCount(receipt), 1);
  });
}

int pdfPageCount(List<int> bytes) {
  final text = latin1.decode(bytes, allowInvalid: true);
  final spaced = RegExp(r'/Type\s*/Page(?!s)').allMatches(text).length;
  final compact = RegExp(r'/Type/Page(?!s)').allMatches(text).length;
  final count = spaced > compact ? spaced : compact;
  return count == 0 ? 1 : count;
}
