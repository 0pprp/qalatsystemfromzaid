import 'package:flutter/services.dart';
import 'package:pdf/pdf.dart';
import 'package:pdf/widgets.dart' as pw;
import 'package:sales_employee_application/data/sales_models.dart';
import 'package:sales_employee_application/services/local_store.dart';
import 'package:sales_employee_application/services/official_contract/contract_text.dart';
import 'package:sales_employee_application/services/official_contract/deposit_receipt_text.dart';
import 'package:sales_employee_application/services/official_contract/official_contract_data.dart';
import 'package:sales_employee_application/services/official_contract/pdf_rich_paragraph.dart';
import 'package:sales_employee_application/services/official_contract/sale_document_map.dart';

/// مولد PDF للعقد ووصل الأمانة.
/// التخطيط منسوخ من `Sales contract tool/lib/pdf/contract_pdf_builder.dart`
/// مع الإبقاء على `package:pdf` الموجود مسبقاً.
class SaleDocuments {
  static Future<void> printContract(Map<String, dynamic> sale) async {
    await _open(await buildContract(SaleDocumentMap.fromLegacyMap(sale)), 'sale-contract.pdf');
  }

  static Future<void> shareContract(Map<String, dynamic> sale) async {
    await printContract(sale);
  }

  static Future<void> printTrustReceipt(Map<String, dynamic> sale) async {
    await _open(await buildReceipt(SaleDocumentMap.fromLegacyMap(sale)), 'trust-receipt.pdf');
  }

  static Future<void> shareTrustReceipt(Map<String, dynamic> sale) async {
    await printTrustReceipt(sale);
  }

  static Future<List<int>> contractBytesFromDraft(SalesDraft sale) async {
    return (await buildContract(SaleDocumentMap.fromDraft(sale))).save();
  }

  static Future<List<int>> receiptBytesFromDraft(SalesDraft sale) async {
    return (await buildReceipt(SaleDocumentMap.fromDraft(sale))).save();
  }

  static Future<void> _open(pw.Document pdf, String name) async {
    await LocalStore.instance.writeBytes(name, await pdf.save());
    await LocalStore.instance.openNamedFile(name);
  }

  static Future<pw.Document> buildContract(OfficialContractData sale) async {
    final fonts = await _fonts();
    final doc = pw.Document();
    final paragraphs = ContractText.buildParagraphs(sale);
    // ~23mm A4 margins (64pt ≈ 22.6mm)
    const pageMargin = pw.EdgeInsets.fromLTRB(64, 68, 64, 64);
    final style = pw.TextStyle(
      font: fonts.regular,
      fontSize: 12,
      lineSpacing: 1.8,
      wordSpacing: 0.4,
    );
    final fieldStyle = pw.TextStyle(
      font: fonts.bold,
      fontSize: 12,
      lineSpacing: 1.8,
      wordSpacing: 0.4,
    );
    final titleStyle = pw.TextStyle(font: fonts.bold, fontSize: 22);
    final signatureLabelStyle = pw.TextStyle(font: fonts.bold, fontSize: 12);

    doc.addPage(
      pw.Page(
        pageTheme: pw.PageTheme(
          pageFormat: PdfPageFormat.a4,
          margin: pageMargin,
          textDirection: pw.TextDirection.rtl,
          theme: fonts.theme,
        ),
        build: (context) => pw.Column(
          crossAxisAlignment: pw.CrossAxisAlignment.stretch,
          children: [
            _contractTitle(titleStyle),
            pw.SizedBox(height: 14),
            pw.Expanded(
              flex: 7,
              child: pw.Column(
                mainAxisAlignment: pw.MainAxisAlignment.spaceBetween,
                crossAxisAlignment: pw.CrossAxisAlignment.stretch,
                children: [
                  for (final paragraph in paragraphs)
                    _richParagraph(paragraph, bodyStyle: style, fieldStyle: fieldStyle),
                ],
              ),
            ),
            pw.SizedBox(height: 18),
            pw.Expanded(
              flex: 2,
              child: pw.Column(
                mainAxisAlignment: pw.MainAxisAlignment.spaceBetween,
                children: [
                  pw.Row(
                    mainAxisAlignment: pw.MainAxisAlignment.spaceBetween,
                    children: [
                      _signatureSpace('الطرف الأول', signatureLabelStyle),
                      _signatureSpace('أمين الصندوق', signatureLabelStyle, width: 240),
                    ],
                  ),
                  pw.Row(
                    mainAxisAlignment: pw.MainAxisAlignment.spaceBetween,
                    children: [
                      _signatureSpace('الطرف الثاني', signatureLabelStyle),
                      _signatureSpace('مندوب المبيعات', signatureLabelStyle, width: 240),
                    ],
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
    return doc;
  }

  static Future<pw.Document> buildReceipt(OfficialContractData sale) async {
    final fonts = await _fonts();
    final doc = pw.Document();
    final bodyParagraphs = DepositReceiptText.buildBodyParagraphs(contract: sale);
    const pageMargin = pw.EdgeInsets.fromLTRB(64, 68, 64, 64);
    final style = pw.TextStyle(
      font: fonts.regular,
      fontSize: 13,
      lineSpacing: 2,
      wordSpacing: 0.4,
    );
    final fieldStyle = pw.TextStyle(
      font: fonts.bold,
      fontSize: 13,
      lineSpacing: 2,
      wordSpacing: 0.4,
    );
    final titleStyle = pw.TextStyle(font: fonts.bold, fontSize: 24);
    final witnessTitleStyle = pw.TextStyle(font: fonts.bold, fontSize: 15);

    doc.addPage(
      pw.Page(
        pageTheme: pw.PageTheme(
          pageFormat: PdfPageFormat.a4,
          margin: pageMargin,
          textDirection: pw.TextDirection.rtl,
          theme: fonts.theme,
        ),
        build: (context) => pw.Column(
          crossAxisAlignment: pw.CrossAxisAlignment.stretch,
          children: [
            pw.Center(child: pw.Text('وصل أمانة', style: titleStyle)),
            pw.SizedBox(height: 22),
            pw.Expanded(
              flex: 5,
              child: pw.Column(
                mainAxisAlignment: pw.MainAxisAlignment.spaceBetween,
                crossAxisAlignment: pw.CrossAxisAlignment.stretch,
                children: [
                  for (final paragraph in bodyParagraphs)
                    _richParagraph(paragraph, bodyStyle: style, fieldStyle: fieldStyle),
                  pw.Row(
                    children: [
                      pw.Expanded(
                        flex: 2,
                        child: pw.Align(
                          alignment: pw.Alignment.centerRight,
                          child: pw.Text('بصمة المدين:', style: style),
                        ),
                      ),
                      pw.Expanded(
                        flex: 3,
                        child: pw.Center(
                          child: pw.Text('توقيع المدين:', style: style),
                        ),
                      ),
                    ],
                  ),
                ],
              ),
            ),
            pw.SizedBox(height: 28),
            pw.Expanded(
              flex: 4,
              child: pw.Directionality(
                textDirection: pw.TextDirection.rtl,
                child: pw.Column(
                  crossAxisAlignment: pw.CrossAxisAlignment.end,
                  children: [
                    pw.Expanded(child: _witnessBlock('الشاهد الأول', witnessTitleStyle, style)),
                    pw.SizedBox(height: 16),
                    pw.Expanded(child: _witnessBlock('الشاهد الثاني', witnessTitleStyle, style)),
                  ],
                ),
              ),
            ),
          ],
        ),
      ),
    );
    return doc;
  }

  static pw.Widget _contractTitle(pw.TextStyle titleStyle) {
    const lineHeight = 28.0;
    const wordGap = 8.0;

    pw.Widget wordBox(String text) => pw.Container(
          height: lineHeight,
          alignment: pw.Alignment.center,
          child: pw.Text(text, style: titleStyle),
        );

    return pw.Center(
      child: pw.Container(
        padding: const pw.EdgeInsets.only(bottom: 3),
        decoration: const pw.BoxDecoration(
          border: pw.Border(
            bottom: pw.BorderSide(width: 1.5, color: PdfColors.black),
          ),
        ),
        child: pw.Row(
          mainAxisSize: pw.MainAxisSize.min,
          mainAxisAlignment: pw.MainAxisAlignment.center,
          crossAxisAlignment: pw.CrossAxisAlignment.center,
          children: [
            wordBox('عقد'),
            pw.SizedBox(width: wordGap),
            wordBox('بيع'),
          ],
        ),
      ),
    );
  }

  static pw.Widget _richParagraph(
    PdfRichParagraph paragraph, {
    required pw.TextStyle bodyStyle,
    required pw.TextStyle fieldStyle,
  }) {
    return pw.RichText(
      textAlign: pw.TextAlign.right,
      text: pw.TextSpan(
        children: [
          for (final part in paragraph.parts)
            pw.TextSpan(
              text: _pdfSafe(part.text),
              style: part.isUserField ? fieldStyle : bodyStyle,
            ),
        ],
      ),
    );
  }

  static pw.Widget _signatureSpace(
    String label,
    pw.TextStyle style, {
    double width = 200,
  }) {
    return pw.SizedBox(
      width: width,
      child: pw.Align(
        alignment: pw.Alignment.topRight,
        child: pw.Text(
          label,
          style: style,
          textAlign: pw.TextAlign.right,
        ),
      ),
    );
  }

  static pw.Widget _witnessBlock(
    String title,
    pw.TextStyle titleStyle,
    pw.TextStyle bodyStyle,
  ) {
    return pw.Directionality(
      textDirection: pw.TextDirection.rtl,
      child: pw.Column(
        crossAxisAlignment: pw.CrossAxisAlignment.end,
        children: [
          pw.Align(
            alignment: pw.Alignment.centerRight,
            child: pw.Container(
              width: 110,
              padding: const pw.EdgeInsets.only(bottom: 3),
              decoration: const pw.BoxDecoration(
                border: pw.Border(
                  bottom: pw.BorderSide(width: 0.8, color: PdfColors.black),
                ),
              ),
              child: pw.Text(
                title,
                style: titleStyle,
                textAlign: pw.TextAlign.right,
                textDirection: pw.TextDirection.rtl,
              ),
            ),
          ),
          pw.SizedBox(height: 10),
          pw.Align(
            alignment: pw.Alignment.centerRight,
            child: pw.Text(
              'الأسم:',
              style: bodyStyle,
              textAlign: pw.TextAlign.right,
              textDirection: pw.TextDirection.rtl,
            ),
          ),
          pw.Spacer(),
          pw.Align(
            alignment: pw.Alignment.centerRight,
            child: pw.Text(
              'التوقيع:',
              style: bodyStyle,
              textAlign: pw.TextAlign.right,
              textDirection: pw.TextDirection.rtl,
            ),
          ),
          pw.Spacer(),
        ],
      ),
    );
  }

  /// Cairo لا يحتوي علامات الاتجاه المخفية؛ حذفها يمنع � و□ حول القيم.
  static String _pdfSafe(String text) {
    return text.replaceAll(
      RegExp(r'[\u200E\u200F\u202A-\u202E\u2066-\u2069\uFEFF\uFFFD]'),
      '',
    );
  }

  static Future<_Fonts> _fonts() async {
    final regular = pw.Font.ttf(await rootBundle.load('assets/fonts/Cairo-Regular.ttf'));
    final bold = pw.Font.ttf(await rootBundle.load('assets/fonts/Cairo-Bold.ttf'));
    return _Fonts(regular, bold, pw.ThemeData.withFont(base: regular, bold: bold));
  }
}

class _Fonts {
  const _Fonts(this.regular, this.bold, this.theme);
  final pw.Font regular;
  final pw.Font bold;
  final pw.ThemeData theme;
}
