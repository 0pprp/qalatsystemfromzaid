import 'package:flutter/services.dart';
import 'package:intl/intl.dart';
import 'package:pdf/pdf.dart';
import 'package:pdf/widgets.dart' as pw;
import 'package:sales_employee_application/data/sales_models.dart';
import 'package:sales_employee_application/services/local_store.dart';
import 'package:sales_employee_application/utils/iraqi_dinar_words.dart';

class SaleDocumentInput {
  const SaleDocumentInput({
    required this.customerName,
    required this.nationalCard,
    required this.province,
    required this.landmark,
    required this.phone,
    required this.mukhtar,
    required this.address,
    required this.goods,
    required this.total,
    required this.installment,
    required this.date,
    this.ration,
    this.employeeName,
  });

  final String customerName;
  final String nationalCard;
  final String province;
  final String landmark;
  final String phone;
  final String mukhtar;
  final String address;
  final String? ration;
  final String goods;
  final num total;
  final num installment;
  final DateTime date;
  final String? employeeName;

  bool get hasRation => ration != null && ration!.trim().isNotEmpty;

  factory SaleDocumentInput.fromDraft(SalesDraft sale) {
    final goods = sale.items
        .map((i) => '${i.productName ?? i.productId} عدد ${i.quantity}')
        .join('، ');
    return SaleDocumentInput(
      customerName: sale.fullName,
      nationalCard: sale.nationalCardNumber ?? '',
      province: sale.province ?? '',
      landmark: sale.nearestLandmark ?? '',
      phone: sale.phone ?? '',
      mukhtar: sale.mukhtarName ?? '',
      address: sale.address ?? '',
      ration: sale.rationCenterNumber,
      goods: goods,
      total: sale.finalSalePrice,
      installment: sale.dailyInstallment,
      date: sale.completedAt ?? sale.createdAt,
      employeeName: sale.employeeName,
    );
  }

  factory SaleDocumentInput.fromLegacyMap(Map<String, dynamic> sale) {
    final items = (sale['contents'] as List?) ?? [];
    final goods = items.map((raw) {
      final item = raw is Map ? Map<String, dynamic>.from(raw) : <String, dynamic>{};
      return '${item['itemName'] ?? item['ItemName'] ?? ''} عدد ${item['quantity'] ?? item['Quantity'] ?? ''}';
    }).join('، ');
    final dateRaw = sale['dateCreate']?.toString();
    return SaleDocumentInput(
      customerName: '${sale['customerName'] ?? ''}',
      nationalCard: '${sale['nationalCardNumber'] ?? sale['cardNumber'] ?? ''}',
      province: '${sale['province'] ?? sale['address'] ?? ''}',
      landmark: '${sale['nearestFunctionPoint'] ?? sale['nearestLandmark'] ?? ''}',
      phone: '${sale['phoneNumber'] ?? sale['phone'] ?? ''}',
      mukhtar: '${sale['mukhtarName'] ?? sale['saleName'] ?? ''}',
      address: '${sale['address'] ?? ''}',
      ration: '${sale['rationCenterNumber'] ?? ''}',
      goods: goods,
      total: _asNum(sale['amountPriceTotalFinal'] ?? sale['amountPriceTotal']),
      installment: _asNum(sale['amountDayTotalFinal'] ?? sale['amountDayTotal']),
      date: DateTime.tryParse(dateRaw ?? '') ?? DateTime.now(),
      employeeName: sale['employeeName']?.toString(),
    );
  }

  static num _asNum(dynamic value) {
    if (value is num) return value;
    return num.tryParse('$value') ?? 0;
  }
}

class SaleDocuments {
  static Future<void> printContract(Map<String, dynamic> sale) async {
    await _open(await buildContract(SaleDocumentInput.fromLegacyMap(sale)), 'sale-contract.pdf');
  }

  static Future<void> shareContract(Map<String, dynamic> sale) async {
    await printContract(sale);
  }

  static Future<void> printTrustReceipt(Map<String, dynamic> sale) async {
    await _open(await buildReceipt(SaleDocumentInput.fromLegacyMap(sale)), 'trust-receipt.pdf');
  }

  static Future<void> shareTrustReceipt(Map<String, dynamic> sale) async {
    await printTrustReceipt(sale);
  }

  static Future<List<int>> contractBytesFromDraft(SalesDraft sale) async {
    return (await buildContract(SaleDocumentInput.fromDraft(sale))).save();
  }

  static Future<List<int>> receiptBytesFromDraft(SalesDraft sale) async {
    return (await buildReceipt(SaleDocumentInput.fromDraft(sale))).save();
  }

  static Future<void> _open(pw.Document pdf, String name) async {
    await LocalStore.instance.writeBytes(name, await pdf.save());
    await LocalStore.instance.openNamedFile(name);
  }

  static Future<pw.Document> buildContract(SaleDocumentInput sale) async {
    final fonts = await _fonts();
    final doc = pw.Document();
    final date = DateFormat('yyyy/MM/dd').format(sale.date);
    final total = NumberFormat('#,##0').format(sale.total);
    final totalWords = IraqiDinarWords.toArabic(sale.total);
    final installment = NumberFormat('#,##0').format(sale.installment);
    final installmentWords = IraqiDinarWords.toArabic(sale.installment);
    final employee = (sale.employeeName ?? '').trim();

    doc.addPage(
      pw.Page(
        pageFormat: PdfPageFormat.a4,
        margin: const pw.EdgeInsets.all(22),
        theme: fonts.theme,
        textDirection: pw.TextDirection.rtl,
        build: (_) {
          return pw.Container(
            padding: const pw.EdgeInsets.all(14),
            decoration: pw.BoxDecoration(border: pw.Border.all(width: 1.5)),
            child: pw.Column(
              crossAxisAlignment: pw.CrossAxisAlignment.stretch,
              children: [
                pw.Row(
                  mainAxisAlignment: pw.MainAxisAlignment.spaceBetween,
                  children: [
                    _sigHead('الطرف الأول'),
                    _sigHead('الطرف الثاني'),
                    _sigHead('مندوب المبيعات'),
                    _sigHead('أمين الصندوق'),
                  ],
                ),
                pw.SizedBox(height: 10),
                pw.Text('عقد بيع',
                    textAlign: pw.TextAlign.center,
                    style: pw.TextStyle(font: fonts.bold, fontSize: 16)),
                pw.SizedBox(height: 10),
                _rich(fonts, [
                  _t('حرصاً من الطرفين على إتمام عملية البيع والشراء وفقاً للأحكام والشروط المتفق عليها، فقد تم تحرير هذا العقد بتاريخ '),
                  _b(date),
                  _t('.'),
                ]),
                pw.SizedBox(height: 8),
                _rich(fonts, [
                  _t('الطرف الأول: '),
                  _b('شركة قلعة الضمان للتجارة العامة محدودة المسؤولية'),
                  _t('.'),
                ]),
                pw.SizedBox(height: 6),
                _rich(fonts, [
                  _t('الطرف الثاني: '),
                  _b(sale.customerName),
                ]),
                _rich(fonts, [
                  _t('والذي يحمل البطاقة الوطنية المرقمة '),
                  _b(sale.nationalCard),
                  _t(' والساكن في محافظة '),
                  _b(sale.province),
                  _t('.'),
                ]),
                _rich(fonts, [
                  _t('أقرب نقطة دالة '),
                  _b(sale.landmark),
                  _t('  رقم الهاتف '),
                  _b(sale.phone),
                  _t('  واتساب '),
                  _b(sale.phone),
                ]),
                _rich(fonts, [
                  _t('اسم المختار '),
                  _b(sale.mukhtar),
                  if (sale.hasRation) ...[
                    _t('  رقم مركز التموين '),
                    _b(sale.ration!.trim()),
                  ],
                ]),
                pw.SizedBox(height: 8),
                _rich(fonts, [
                  _t('1. باع الطرف الأول إلى الطرف الثاني بضاعة نوع '),
                  _b(sale.goods),
                  _t(' بسعر كلي والبالغ قدره رقماً '),
                  _b(total),
                  _t(' كتابة '),
                  _b(totalWords),
                  _t('.'),
                ]),
                pw.SizedBox(height: 4),
                _rich(fonts, [
                  _t('2. يلتزم الطرف الثاني بتسديد المبلغ الكلي على شكل دفعات بقسط يومي قدره رقماً '),
                  _b(installment),
                  _t(' كتابة '),
                  _b(installmentWords),
                  _t('.'),
                ]),
                _tLine(fonts, 'يبدأ القسط الأول من تاريخ توقيع هذا العقد ويستمر دون توقف إلى نهاية تسديد كامل المبلغ.'),
                pw.SizedBox(height: 4),
                _tLine(fonts, '3. على الطرف الثاني إشعار الطرف الأول عند انتقال محل عمله أو سكنه ويبلغ الطرف الأول بعنوان محل عمله أو سكنه الجديد.'),
                pw.SizedBox(height: 4),
                _tLine(
                  fonts,
                  sale.hasRation
                      ? '4. يسلم الطرف الثاني إلى الطرف الأول نسخة ملونة من مستمسكاته الأصلية (هويته وبطاقة سكنه وبطاقة تموينه).'
                      : '4. يسلم الطرف الثاني إلى الطرف الأول نسخة ملونة من مستمسكاته الأصلية (هويته وبطاقة سكنه).',
                ),
                pw.SizedBox(height: 4),
                _tLine(fonts, '5. الطرف الثاني مسؤول عن فحص البضاعة عند استلامها قبل توقيع العقد للتأكد من خلوها من أي عيب أو تلف.'),
                pw.SizedBox(height: 4),
                _tLine(fonts, '6. يتحمل الطرف المتخلف عن الالتزام بهذا العقد كافة تكاليف الدعوى بما فيها الرسوم وأتعاب المحاماة.'),
                pw.SizedBox(height: 4),
                _tLine(fonts, '7. في حال إخلال الطرف الثاني بأحد شروط هذا العقد، يلزم دفع تعويض للطرف الآخر مبلغ قدره نفس قيمة العقد.'),
                pw.SizedBox(height: 4),
                _tLine(fonts, '8. في حال تخلف الطرف الثاني عن التسديد لـ 7 مرات متوالية أو متقطعة يترتب عليه استحقاق كامل المبلغ المتبقي فوراً.'),
                pw.SizedBox(height: 4),
                _tLine(fonts, '9. تختص محكمة النجف الأشرف بأي نزاع مدني أو جزائي بخصوص الدعاوى الناشئة عن هذا العقد.'),
                pw.SizedBox(height: 4),
                _tLine(fonts, '10. يكون هذا العقد ملزم للطرفين وورثتهم.'),
                pw.SizedBox(height: 10),
                _rich(fonts, [
                  _t('أني الموقع أدناه أعمل مندوب مبيعات الشركة '),
                  if (employee.isNotEmpty) _b(employee),
                  _t('.'),
                ]),
                _tLine(fonts, 'أتعهد بأني باشرت ببيع البضاعة إلى الطرف الثاني وقد تم استلامها من قبله وفقاً لتعليمات الشركة، وقد تأكدت من جميع معلومات الزبون المذكورة في هذا العقد أعلاه ومنها المستمسكات الأصلية وعنوانه المثبت أعلاه ورقم موبايله، وعليه وقعت.'),
                pw.SizedBox(height: 8),
                _tLine(fonts, 'أني أمين صندوق الفرع أشهد بأن مندوب المبيعات قد وقع أمامي.'),
              ],
            ),
          );
        },
      ),
    );
    return doc;
  }

  static Future<pw.Document> buildReceipt(SaleDocumentInput sale) async {
    final fonts = await _fonts();
    final doc = pw.Document();
    final date = DateFormat('yyyy/MM/dd').format(sale.date);
    final amount = NumberFormat('#,##0').format(sale.total);
    final words = IraqiDinarWords.toArabic(sale.total);

    doc.addPage(
      pw.Page(
        pageFormat: PdfPageFormat.a4,
        margin: const pw.EdgeInsets.all(22),
        theme: fonts.theme,
        textDirection: pw.TextDirection.rtl,
        build: (_) {
          return pw.Container(
            padding: const pw.EdgeInsets.all(18),
            decoration: pw.BoxDecoration(border: pw.Border.all(width: 1.5)),
            child: pw.Column(
              crossAxisAlignment: pw.CrossAxisAlignment.stretch,
              children: [
                pw.Text('وصل أمانة',
                    textAlign: pw.TextAlign.center,
                    style: pw.TextStyle(font: fonts.bold, fontSize: 18)),
                pw.SizedBox(height: 22),
                _rich(fonts, [_t('المبلغ رقماً: '), _b(amount)], size: 12),
                pw.SizedBox(height: 8),
                _rich(fonts, [_t('كتابة: '), _b(words)], size: 12),
                pw.SizedBox(height: 16),
                _rich(fonts, [
                  _t('إني الموقع أدناه أقر وأعترف بأني مدين لشركة قلعة الضمان للتجارة العامة محدودة المسؤولية بالمبلغ أعلاه وأتعهد بأن أعيده متى ما طلبت الشركة مني ولأجله وقعت بتاريخ '),
                  _b(date),
                  _t('.'),
                ], size: 12),
                pw.SizedBox(height: 18),
                _rich(fonts, [_t('اسم المستلم: '), _b(sale.customerName)], size: 12),
                pw.SizedBox(height: 8),
                _rich(fonts, [_t('رقم البطاقة الوطنية: '), _b(sale.nationalCard)], size: 12),
                pw.SizedBox(height: 8),
                _rich(fonts, [_t('اسم المختار: '), _b(sale.mukhtar)], size: 12),
                pw.SizedBox(height: 8),
                _rich(fonts, [_t('العنوان: '), _b(sale.address)], size: 12),
                pw.SizedBox(height: 28),
                pw.Row(
                  mainAxisAlignment: pw.MainAxisAlignment.spaceBetween,
                  children: [
                    pw.Text('بصمة المدين: ....................',
                        style: pw.TextStyle(font: fonts.regular, fontSize: 12)),
                    pw.Text('توقيع المدين: ....................',
                        style: pw.TextStyle(font: fonts.regular, fontSize: 12)),
                  ],
                ),
                pw.SizedBox(height: 36),
                pw.Row(children: [
                  pw.Expanded(child: _witness(fonts, 'الأول الشاهد')),
                  pw.SizedBox(width: 28),
                  pw.Expanded(child: _witness(fonts, 'الثاني الشاهد')),
                ]),
              ],
            ),
          );
        },
      ),
    );
    return doc;
  }

  static pw.Widget _witness(_Fonts fonts, String title) {
    return pw.Column(
      crossAxisAlignment: pw.CrossAxisAlignment.start,
      children: [
        pw.Text(title, style: pw.TextStyle(font: fonts.bold, fontSize: 12)),
        pw.SizedBox(height: 10),
        pw.Text('الاسم: ....................', style: pw.TextStyle(font: fonts.regular, fontSize: 12)),
        pw.SizedBox(height: 8),
        pw.Text('التوقيع: ....................', style: pw.TextStyle(font: fonts.regular, fontSize: 12)),
      ],
    );
  }

  static pw.Widget _sigHead(String label) {
    return pw.SizedBox(
      width: 90,
      child: pw.Column(children: [
        pw.Text(label, textAlign: pw.TextAlign.center, style: const pw.TextStyle(fontSize: 9, fontWeight: pw.FontWeight.bold)),
        pw.SizedBox(height: 18),
        pw.Text('..................', textAlign: pw.TextAlign.center, style: const pw.TextStyle(fontSize: 8)),
      ]),
    );
  }

  static _Span _t(String text) => _Span(text, false);
  static _Span _b(String text) => _Span(text, true);

  static pw.Widget _rich(_Fonts fonts, List<_Span> parts, {double size = 10.5}) {
    return pw.RichText(
      textAlign: pw.TextAlign.justify,
      text: pw.TextSpan(
        children: [
          for (final part in parts)
            if (part.text.isNotEmpty)
              pw.TextSpan(
                text: part.text,
                style: pw.TextStyle(
                  font: part.bold ? fonts.bold : fonts.regular,
                  fontSize: size,
                  height: 1.65,
                ),
              ),
        ],
      ),
    );
  }

  static pw.Widget _tLine(_Fonts fonts, String text) {
    return _rich(fonts, [_t(text)]);
  }

  static Future<_Fonts> _fonts() async {
    final regular = pw.Font.ttf(await rootBundle.load('assets/fonts/Cairo-Regular.ttf'));
    final bold = pw.Font.ttf(await rootBundle.load('assets/fonts/Cairo-Bold.ttf'));
    return _Fonts(regular, bold, pw.ThemeData.withFont(base: regular, bold: bold));
  }
}

class _Span {
  const _Span(this.text, this.bold);
  final String text;
  final bool bold;
}

class _Fonts {
  const _Fonts(this.regular, this.bold, this.theme);
  final pw.Font regular;
  final pw.Font bold;
  final pw.ThemeData theme;
}
