import 'package:flutter/services.dart';
import 'package:intl/intl.dart';
import 'package:pdf/pdf.dart';
import 'package:pdf/widgets.dart' as pw;
import 'package:sales_employee_application/services/local_store.dart';

class SaleDocuments {
  static Future<void> printContract(Map<String, dynamic> sale) async {
    await _open(await _build('عقد بيع', sale, isReceipt: false), 'sale-contract.pdf');
  }

  static Future<void> shareContract(Map<String, dynamic> sale) async {
    await printContract(sale);
  }

  static Future<void> printTrustReceipt(Map<String, dynamic> sale) async {
    await _open(await _build('وصل أمانة', sale, isReceipt: true), 'trust-receipt.pdf');
  }

  static Future<void> shareTrustReceipt(Map<String, dynamic> sale) async {
    await printTrustReceipt(sale);
  }

  static Future<void> _open(pw.Document pdf, String name) async {
    await LocalStore.instance.writeBytes(name, await pdf.save());
    await LocalStore.instance.openNamedFile(name);
  }

  static Future<pw.Document> _build(
    String title,
    Map<String, dynamic> sale, {
    required bool isReceipt,
  }) async {
    final regular =
        pw.Font.ttf(await rootBundle.load('assets/fonts/Cairo-Regular.ttf'));
    final bold =
        pw.Font.ttf(await rootBundle.load('assets/fonts/Cairo-Bold.ttf'));
    final theme = pw.ThemeData.withFont(base: regular, bold: bold);
    final doc = pw.Document();
    final items = (sale['contents'] as List?) ?? [];
    final fmt = NumberFormat('#,##0');
    final date = sale['dateCreate']?.toString() ??
        DateFormat('yyyy-MM-dd').format(DateTime.now());

    doc.addPage(
      pw.Page(
        pageFormat: PdfPageFormat.a4,
        theme: theme,
        textDirection: pw.TextDirection.rtl,
        build: (context) {
          return pw.Column(
            crossAxisAlignment: pw.CrossAxisAlignment.stretch,
            children: [
              pw.Text('قلعة الضمان',
                  textAlign: pw.TextAlign.center,
                  style: pw.TextStyle(
                      font: bold, fontSize: 22, color: PdfColors.teal800)),
              pw.SizedBox(height: 8),
              pw.Text(title,
                  textAlign: pw.TextAlign.center,
                  style: pw.TextStyle(font: bold, fontSize: 18)),
              pw.SizedBox(height: 16),
              _row('التاريخ', date, bold, regular),
              _row('اسم الزبون', '${sale['customerName'] ?? ''}', bold, regular),
              _row('الهاتف', '${sale['phoneNumber'] ?? ''}', bold, regular),
              _row('العنوان', '${sale['address'] ?? ''}', bold, regular),
              _row('اسم المحل', '${sale['shopName'] ?? ''}', bold, regular),
              _row('أقرب نقطة دالة', '${sale['nearestFunctionPoint'] ?? ''}',
                  bold, regular),
              _row('اسم المبيع / الكفيل', '${sale['saleName'] ?? ''}', bold,
                  regular),
              _row('اسم الوصل', '${sale['receiptName'] ?? ''}', bold, regular),
              if (sale['ratingLabel'] != null)
                _row('التقييم', '${sale['ratingLabel']}', bold, regular),
              pw.SizedBox(height: 16),
              pw.TableHelper.fromTextArray(
                headers: [
                  'المادة',
                  'الكمية',
                  'السعر',
                  'القسط اليومي',
                  'المجموع'
                ],
                headerStyle: pw.TextStyle(font: bold, fontSize: 10),
                cellStyle: pw.TextStyle(font: regular, fontSize: 10),
                cellAlignment: pw.Alignment.center,
                data: items.map((raw) {
                  final item = raw is Map ? Map<String, dynamic>.from(raw) : <String, dynamic>{};
                  return [
                    '${item['itemName'] ?? item['ItemName'] ?? ''}',
                    '${item['quantity'] ?? item['Quantity'] ?? ''}',
                    fmt.format(_num(
                        item['itemPriceDenar'] ?? item['ItemPriceDenar'])),
                    fmt.format(_num(
                        item['amountDayDenar'] ?? item['AmountDayDenar'])),
                    fmt.format(_num(item['totalItemPriceDenar'] ??
                        item['TotalItemPriceDenar'])),
                  ];
                }).toList(),
              ),
              pw.SizedBox(height: 12),
              _row(
                  'مجموع السعر',
                  fmt.format(_num(sale['amountPriceTotalFinal'] ??
                      sale['amountPriceTotal'])),
                  bold,
                  regular),
              _row(
                  'مجموع القسط اليومي',
                  fmt.format(_num(
                      sale['amountDayTotalFinal'] ?? sale['amountDayTotal'])),
                  bold,
                  regular),
              if ((sale['notes'] ?? '').toString().isNotEmpty)
                _row('ملاحظات', '${sale['notes']}', bold, regular),
              pw.Spacer(),
              pw.Text(
                isReceipt
                    ? 'وصل أمانة بالمواد المذكورة أعلاه، وتبقى أمانة في عهدة الزبون لحين تسديد كامل المبلغ.'
                    : 'تم الاتفاق على البيع وفق الأسعار والكميات أعلاه، ويقر الطرفان بالموافقة.',
                style: pw.TextStyle(font: regular, fontSize: 11),
              ),
              pw.SizedBox(height: 28),
              pw.Row(
                mainAxisAlignment: pw.MainAxisAlignment.spaceBetween,
                children: [
                  pw.Text('توقيع الموظف: ____________',
                      style: pw.TextStyle(font: regular, fontSize: 11)),
                  pw.Text('توقيع الزبون: ____________',
                      style: pw.TextStyle(font: regular, fontSize: 11)),
                ],
              ),
            ],
          );
        },
      ),
    );
    return doc;
  }

  static pw.Widget _row(
      String label, String value, pw.Font bold, pw.Font regular) {
    return pw.Padding(
      padding: const pw.EdgeInsets.only(bottom: 4),
      child: pw.Row(children: [
        pw.Text('$label: ', style: pw.TextStyle(font: bold, fontSize: 11)),
        pw.Expanded(
            child:
                pw.Text(value, style: pw.TextStyle(font: regular, fontSize: 11))),
      ]),
    );
  }

  static num _num(dynamic value) {
    if (value is num) return value;
    return num.tryParse('$value') ?? 0;
  }
}
