import 'package:intl/intl.dart';
import 'package:sales_employee_application/services/official_contract/official_contract_data.dart';
import 'package:sales_employee_application/services/official_contract/pdf_rich_paragraph.dart';

/// نص العقد الكامل وفق القالب الرسمي.
/// المصدر: `Sales contract tool/lib/pdf/contract_text.dart`
class ContractText {
  static String formatDate(DateTime date) {
    return DateFormat('yyyy/MM/dd').format(date);
  }

  /// تنسيق الرقم بالفواصل كما في الأداة المرجعية: 1000 → 1,000
  static String formatWithCommas(String input) {
    final digits = input.replaceAll(RegExp(r'[^\d]'), '');
    if (digits.isEmpty) return '';
    final buffer = StringBuffer();
    for (var i = 0; i < digits.length; i++) {
      if (i > 0 && (digits.length - i) % 3 == 0) {
        buffer.write(',');
      }
      buffer.write(digits[i]);
    }
    return buffer.toString();
  }

  /// جميع فقرات العقد بالترتيب — تُملأ الفراغات من بيانات العقد والملف الشخصي.
  static List<PdfRichParagraph> buildParagraphs(OfficialContractData contract) {
    final date = formatDate(contract.contractDate);
    final totalNum = formatWithCommas(contract.totalPriceNumeric);
    final dailyNum = formatWithCommas(contract.dailyInstallmentNumeric);

    return [
      _intro(date),
      _static('الطرف الأول شركة قلعة الضمان للتجارة العامة محدودة المسؤولية.'),
      _partyTwoBlock(contract),
      _contactBlock(contract),
      _saleClause(contract, totalNum),
      _installmentClause(contract, dailyNum),
      _static(
        '3. على الطرف الثاني اشعار الطرف الأول عند انتقال محل عمله او سكنه '
        'ويبلغ الطرف الأول بعنوان محل عمله او سكنه الجديد.',
      ),
      _static(
        '4. يسلم الطرف الثاني الى الطرف الأول نسخة ملونة من مستمسكاته الاصلية ',
        parens: 'هويته وبطاقة سكنه وبطاقة تموينه',
      ),
      _static(
        '5. الطرف الثاني مسؤول عن فحص البضاعة عند استلامها قبل توقيع العقد '
        'للتأكد من خلوها من أي عيب أو تلف.',
      ),
      _static(
        '6. يتحمل الطرف المتخلف عن الالتزام بهذا العقد كافة تكاليف الدعوى '
        'بما فيها الرسوم وأتعاب المحاماة.',
      ),
      _static(
        '7. في حال إخلال الطرف الثاني بأحد شروط هذا العقد، يلزم دفع تعويض '
        'للطرف الآخر مبلغ قدره نفس قيمة العقد.',
      ),
      _static(
        '8. في حال تخلف الطرف الثاني عن التسديد ل7 مرات متوالية او متقطعة '
        'يترتب عليه استحقاق كامل المبلغ المتبقي فورا.',
      ),
      _static(
        '9. تختص محكمة النجف الأشرف بأي نزاع مدني أو جزائي بخصوص الدعاوى '
        'الناشئة عن هذا العقد.',
      ),
      _static('10. يكون هذا العقد ملزم للطرفين وورثتهم.'),
      salesRepDeclaration(contract),
      cashierWitnessLine(contract),
    ];
  }

  static PdfRichParagraph _static(String text, {String? parens}) {
    if (parens == null) {
      return PdfRichParagraph([PdfTextPart(text)]);
    }
    final b = PdfParagraphBuilder()
      ..text(text)
      ..staticParens(parens);
    return b.build();
  }

  static PdfRichParagraph _intro(String date) {
    final b = PdfParagraphBuilder()
      ..text(
        'حرصاً من الطرفين على إتمام عملية البيع والشراء وفقاً للأحكام والشروط '
        'المتفق عليها، فقد تم تحرير هذا العقد بتاريخ ',
      )
      ..fieldInParens(date)
      ..text('.');
    return b.build();
  }

  static PdfRichParagraph _partyTwoBlock(OfficialContractData contract) {
    final b = PdfParagraphBuilder()
      ..text('الطرف الثاني ')
      ..fieldInParens(contract.customerName)
      ..text(' والذي يحمل البطاقة الوطنية المرقمة ')
      ..fieldInParens(contract.nationalId)
      ..text(' والساكن في محافظة ')
      ..fieldInParens(contract.province);
    return b.build();
  }

  static PdfRichParagraph _contactBlock(OfficialContractData contract) {
    final b = PdfParagraphBuilder()
      ..text('أقرب نقطة دالة ')
      ..fieldInParens(contract.address)
      ..text(' رقم الهاتف ')
      ..fieldInParens(contract.phone)
      ..text(' واتساب ')
      ..fieldInParens(contract.whatsapp)
      ..text(' اسم المختار ')
      ..fieldInParens(contract.guarantorName)
      ..text(' رقم مركز التموين ')
      ..fieldInParens(contract.rationCenterNumber);
    return b.build();
  }

  static PdfRichParagraph _saleClause(
    OfficialContractData contract,
    String totalNum,
  ) {
    final b = PdfParagraphBuilder()
      ..text('1. باع الطرف الأول إلى الطرف الثاني بضاعة نوع ')
      ..fieldInParens(contract.goodsType)
      ..text(' بسعر كلي والبالغ قدره رقما ')
      ..fieldInParens(totalNum)
      ..text(' كتابة ')
      ..fieldInParens(contract.totalPriceWords);
    return b.build();
  }

  static PdfRichParagraph _installmentClause(
    OfficialContractData contract,
    String dailyNum,
  ) {
    final b = PdfParagraphBuilder()
      ..text(
        '2. يلتزم الطرف الثاني بتسديد المبلغ الكلي على شكل دفعات بقسط يومي قدرة '
        'رقما ',
      )
      ..fieldInParens(dailyNum)
      ..text(' كتابة ')
      ..fieldInParens(contract.dailyInstallmentWords)
      ..text(
        ' يبدا القسط الأول من تاريخ توقيع هذا العقد ويستمر دون توقف الى '
        'نهاية تسديد كامل المبلغ.',
      );
    return b.build();
  }

  static PdfRichParagraph salesRepDeclaration(OfficialContractData contract) {
    final b = PdfParagraphBuilder()
      ..text('أني الموقع ادناه ')
      ..fieldInParens(contract.salesRepName)
      ..text(
        ' أعمل مندوب مبيعات الشركة أتعهد بأني باشرت ببيع البضاعة إلى الطرف '
        'الثاني وقد تم استلامها من قبله وفقاً لتعليمات الشركة، وقد تأكدت من '
        'جميع معلومات الزبون المذكورة في هذا العقد أعلاه ومنها المستمسكات '
        'الأصلية وعنوانه المثبت أعلاه ورقم موبايله، وعليه وقعت.',
      );
    return b.build();
  }

  static PdfRichParagraph cashierWitnessLine(OfficialContractData contract) {
    final b = PdfParagraphBuilder()
      ..text('أنى ')
      ..fieldInParens(contract.cashierName)
      ..text(' أمين صندوق الفرع اشهد بان مندوب المبيعات قد وقع امامي.');
    return b.build();
  }
}
