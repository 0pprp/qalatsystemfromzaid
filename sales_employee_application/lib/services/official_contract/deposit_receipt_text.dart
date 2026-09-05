import 'package:sales_employee_application/services/official_contract/contract_text.dart';
import 'package:sales_employee_application/services/official_contract/official_contract_data.dart';
import 'package:sales_employee_application/services/official_contract/pdf_rich_paragraph.dart';

/// نص وصل الأمانة — الصفحة الثانية من ملف PDF.
/// المصدر: `Sales contract tool/lib/pdf/deposit_receipt_text.dart`
class DepositReceiptText {
  static String formatToday(DateTime date) => ContractText.formatDate(date);

  static List<PdfRichParagraph> buildBodyParagraphs({
    required OfficialContractData contract,
  }) {
    final totalNum =
        ContractText.formatWithCommas(contract.totalPriceNumeric);
    final date = formatToday(contract.contractDate);

    return [
      _labeledField('المبلغ رقما: ', totalNum),
      _labeledField('كتابــــــــــة: ', contract.totalPriceWords),
      _debtParagraph(date),
      _labeledField('أسم المستلم: ', contract.customerName),
      _labeledField('رقم البطاقة الوطنية: ', contract.nationalId),
      _labeledField('أسم المختار: ', contract.guarantorName),
      _labeledField('العنوان: ', contract.address),
    ];
  }

  static PdfRichParagraph _labeledField(String label, String value) {
    final b = PdfParagraphBuilder()
      ..text(label)
      ..fieldInParens(value);
    return b.build();
  }

  static PdfRichParagraph _debtParagraph(String date) {
    final b = PdfParagraphBuilder()
      ..text(
        'إني الموقع أدناه أقر واعترف باني مدين لشركة قلعة الضمان للتجارة العامة '
        'محدودة المسؤولية بالمبلغ أعلاه واتعهد بان أعيده متى ما طلبت الشركة '
        'مني ولأجله وقعت بتاريخ ',
      )
      ..fieldInParens(date);
    return b.build();
  }
}
