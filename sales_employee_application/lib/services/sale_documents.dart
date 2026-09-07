import 'package:flutter/services.dart';
import 'package:pdf/pdf.dart';
import 'package:pdf/widgets.dart' as pw;
import 'package:sales_employee_application/data/sales_models.dart';
import 'package:sales_employee_application/services/local_store.dart';
import 'package:sales_employee_application/services/official_contract/contract_text.dart';
import 'package:sales_employee_application/services/official_contract/deposit_receipt_text.dart';
import 'package:sales_employee_application/services/official_contract/official_contract_data.dart';
import 'package:sales_employee_application/services/official_contract/sale_document_map.dart';

/// مولد محلي للاختبارات فقط. الملفات المعتمدة تُنزَّل من OfficialSalesPdfRenderer عبر الـ API.
class SaleDocuments {
  static const double _margin = 48;
  static const double _bodySize = 11.5;
  static const double _titleSize = 20;
  static const double _receiptTitleSize = 22;
  static const double _paragraphGap = 5.5;
  static const double debtorFingerprintHeight = 80;

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

  static pw.PageTheme _theme(_Fonts fonts) => pw.PageTheme(
        pageFormat: PdfPageFormat.a4,
        margin: const pw.EdgeInsets.all(_margin),
        textDirection: pw.TextDirection.rtl,
        theme: fonts.theme,
      );

  static Future<pw.Document> buildContract(OfficialContractData sale) async {
    final fonts = await _fonts();
    final doc = pw.Document();
    doc.addPage(
      pw.Page(
        pageTheme: _theme(fonts),
        build: (context) => _contractPage(sale, fonts),
      ),
    );
    return doc;
  }

  static Future<pw.Document> buildReceipt(OfficialContractData sale) async {
    final fonts = await _fonts();
    final doc = pw.Document();
    doc.addPage(
      pw.Page(
        pageTheme: _theme(fonts),
        build: (context) => _receiptPage(sale, fonts),
      ),
    );
    return doc;
  }

  static Future<pw.Document> buildSaleDocuments(OfficialContractData sale) async {
    final fonts = await _fonts();
    final doc = pw.Document();
    doc.addPage(
      pw.Page(
        pageTheme: _theme(fonts),
        build: (context) => _contractPage(sale, fonts),
      ),
    );
    doc.addPage(
      pw.Page(
        pageTheme: _theme(fonts),
        build: (context) => _receiptPage(sale, fonts),
      ),
    );
    return doc;
  }

  static pw.Widget _contractPage(OfficialContractData sale, _Fonts fonts) {
    final paragraphs = ContractText.buildParagraphs(sale);
    final style = pw.TextStyle(font: fonts.regular, fontSize: _bodySize, height: 1.22);
    final titleStyle = pw.TextStyle(font: fonts.bold, fontSize: _titleSize);
    final signatureStyle = pw.TextStyle(font: fonts.bold, fontSize: _bodySize);
    return pw.Column(
      crossAxisAlignment: pw.CrossAxisAlignment.stretch,
      children: [
        pw.Center(
          child: pw.Container(
            padding: const pw.EdgeInsets.only(bottom: 4),
            decoration: const pw.BoxDecoration(
              border: pw.Border(bottom: pw.BorderSide(width: 1.2, color: PdfColors.black)),
            ),
            child: pw.Text('عقد بيع', style: titleStyle, textDirection: pw.TextDirection.rtl),
          ),
        ),
        pw.SizedBox(height: 10),
        for (final paragraph in paragraphs) ...[
          _paragraph(paragraph.plainText, style),
          pw.SizedBox(height: _paragraphGap),
        ],
        pw.SizedBox(height: 6),
        pw.Row(
          mainAxisAlignment: pw.MainAxisAlignment.spaceBetween,
          children: [
            pw.Text('الطرف الأول', style: signatureStyle, textDirection: pw.TextDirection.rtl),
            pw.Text('أمين الصندوق', style: signatureStyle, textDirection: pw.TextDirection.rtl),
          ],
        ),
        pw.SizedBox(height: 28),
        pw.Row(
          mainAxisAlignment: pw.MainAxisAlignment.spaceBetween,
          children: [
            pw.Text('الطرف الثاني', style: signatureStyle, textDirection: pw.TextDirection.rtl),
            pw.Text('مندوب المبيعات', style: signatureStyle, textDirection: pw.TextDirection.rtl),
          ],
        ),
      ],
    );
  }

  static pw.Widget _receiptPage(OfficialContractData sale, _Fonts fonts) {
    final bodyParagraphs = DepositReceiptText.buildBodyParagraphs(contract: sale);
    final style = pw.TextStyle(font: fonts.regular, fontSize: _bodySize, height: 1.22);
    final titleStyle = pw.TextStyle(font: fonts.bold, fontSize: _receiptTitleSize);
    final witnessTitleStyle = pw.TextStyle(font: fonts.bold, fontSize: 14);
    return pw.Column(
      crossAxisAlignment: pw.CrossAxisAlignment.stretch,
      children: [
        pw.Center(child: pw.Text('وصل أمانة', style: titleStyle, textDirection: pw.TextDirection.rtl)),
        pw.SizedBox(height: 14),
        for (final paragraph in bodyParagraphs) ...[
          _paragraph(paragraph.plainText, style),
          pw.SizedBox(height: _paragraphGap),
        ],
        pw.SizedBox(height: 10),
        pw.Row(
          children: [
            pw.Expanded(
              flex: 2,
              child: pw.Align(
                alignment: pw.Alignment.centerRight,
                child: pw.Column(
                  crossAxisAlignment: pw.CrossAxisAlignment.end,
                  children: [
                    pw.Text('بصمة المدين:', style: style, textDirection: pw.TextDirection.rtl),
                    pw.SizedBox(height: 6),
                    pw.Container(
                      height: debtorFingerprintHeight,
                      decoration: pw.BoxDecoration(
                        border: pw.Border.all(width: 0.8, color: PdfColors.black),
                      ),
                    ),
                  ],
                ),
              ),
            ),
            pw.Expanded(
              flex: 3,
              child: pw.Center(
                child: pw.Column(
                  children: [
                    pw.Text('توقيع المدين:', style: style, textDirection: pw.TextDirection.rtl),
                    pw.SizedBox(height: 40),
                  ],
                ),
              ),
            ),
          ],
        ),
        pw.SizedBox(height: 28),
        _witnessBlock('الشاهد الأول', witnessTitleStyle, style),
        pw.SizedBox(height: 16),
        _witnessBlock('الشاهد الثاني', witnessTitleStyle, style),
      ],
    );
  }

  static pw.Widget _paragraph(String text, pw.TextStyle style) {
    return pw.Text(
      _pdfSafe(text),
      style: style,
      textAlign: pw.TextAlign.right,
      textDirection: pw.TextDirection.rtl,
    );
  }

  static pw.Widget _witnessBlock(String title, pw.TextStyle titleStyle, pw.TextStyle bodyStyle) {
    return pw.Column(
      crossAxisAlignment: pw.CrossAxisAlignment.end,
      children: [
        pw.Container(
          width: 120,
          padding: const pw.EdgeInsets.only(bottom: 3),
          decoration: const pw.BoxDecoration(
            border: pw.Border(bottom: pw.BorderSide(width: 0.8, color: PdfColors.black)),
          ),
          child: pw.Text(title, style: titleStyle, textAlign: pw.TextAlign.center, textDirection: pw.TextDirection.rtl),
        ),
        pw.SizedBox(height: 8),
        pw.Text('الأسم:', style: bodyStyle, textDirection: pw.TextDirection.rtl),
        pw.SizedBox(height: 28),
        pw.Text('التوقيع:', style: bodyStyle, textDirection: pw.TextDirection.rtl),
      ],
    );
  }

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
