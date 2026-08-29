import 'package:delegate_application/Customer.dart';
import 'package:delegate_application/utils/PdfAssetCache.dart';
import 'package:delegate_application/utils/PrintUtils.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:intl/intl.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:pdf/pdf.dart';
import 'package:pdf/widgets.dart' as pw;
import 'dart:typed_data';

class CustomerNoReceiptReport {
  final String delegateName;
  final String receiptName;
  final List<Client> customerData;

  CustomerNoReceiptReport({
    required this.delegateName,
    required this.receiptName,
    required this.customerData,
  });

  String _formatNumber(double number) {
    final formatter = NumberFormat('#,###');
    return formatter.format(number);
  }

  Future<void> printReport(BuildContext context) async {
    await initializeDateFormatting('ar', null);
    if (!context.mounted) return;
    await PrintUtils.previewPdf(context, (format) => _generatePdf(format));
  }

  Future<Uint8List> _generatePdf(PdfPageFormat format) async {
    final pdf = pw.Document();
    final ttf = await PdfAssetCache.getFontRegular();
    final ttfBold = await PdfAssetCache.getFontBold();

    final String currentDate = DateFormat.yMMMMd('ar').format(DateTime.now());

    // Pre-calculate data rows
    List<List<String>> rows = [];
    double totalDaySales = 0;

    for (var customer in customerData) {
      if (customer.isLegal == 'false') {
        double amountDaySales = customer.dailyInstallment;
        totalDaySales += amountDaySales;
        rows.add([
          customer.name,
          customer.numberOfDayPayment.toString(),
          "${_formatNumber(amountDaySales)} دع",
        ]);
      }
    }

    pdf.addPage(
      pw.MultiPage(
        pageFormat: PrintUtils.ensureFiniteHeight(format),
        margin: const pw.EdgeInsets.all(20),
        theme: pw.ThemeData.withFont(base: ttf, bold: ttfBold),
        textDirection: pw.TextDirection.rtl,
        build: (pw.Context context) {
          return [
            pw.Row(mainAxisAlignment: pw.MainAxisAlignment.center, children: [
              pw.Column(children: [
                pw.Text("شركة قلعة الضمان",
                    style: pw.TextStyle(
                        fontSize: 16, fontWeight: pw.FontWeight.bold)),
                pw.Text("الغير مسددين",
                    style: const pw.TextStyle(fontSize: 14)),
              ])
            ]),
            pw.SizedBox(height: 10),
            // Info Table
            pw.Table(
              border: pw.TableBorder.all(),
              columnWidths: {
                0: const pw.FlexColumnWidth(2),
                1: const pw.FlexColumnWidth(1),
              },
              children: [
                _buildRow("اسم القائمة", delegateName),
                _buildRow("اسم الجابي", receiptName),
                _buildRow("عدد التسديدات", "0"),
                _buildRow("التاريخ", currentDate),
              ],
            ),
            pw.SizedBox(height: 15),
            // Data Table
            pw.Table(
              border: pw.TableBorder.all(),
              children: [
                // Header
                pw.TableRow(
                  decoration: const pw.BoxDecoration(color: PdfColors.grey200),
                  children: [
                    _buildCell("اسم الزبون", isHeader: true),
                    _buildCell("عدد الأيام", isHeader: true),
                    _buildCell("القسط", isHeader: true),
                  ],
                ),
                // Rows
                ...rows.map((row) => pw.TableRow(children: [
                      _buildCell(row[0]),
                      _buildCell(row[1]),
                      _buildCell(row[2]),
                    ]))
              ],
            ),
            pw.SizedBox(height: 15),
            // Totals Table
            pw.Table(
              border: pw.TableBorder.all(),
              columnWidths: {
                0: const pw.FlexColumnWidth(2),
                1: const pw.FlexColumnWidth(1),
              },
              children: [
                _buildRow("عدد العملاء", rows.length.toString()),
                _buildRow("مجموع القسط", "${_formatNumber(totalDaySales)} دع"),
              ],
            ),
          ];
        },
      ),
    );

    return pdf.save();
  }

  pw.TableRow _buildRow(String label, String value) {
    return pw.TableRow(children: [
      _buildCell(value),
      _buildCell(label, isHeader: true),
    ]);
  }

  pw.Widget _buildCell(String text, {bool isHeader = false}) {
    return pw.Container(
      color: isHeader ? PdfColors.grey200 : null,
      padding: const pw.EdgeInsets.all(5),
      alignment: pw.Alignment.center,
      child: pw.Text(
        text,
        textAlign: pw.TextAlign.center,
        style: isHeader
            ? pw.TextStyle(fontWeight: pw.FontWeight.bold, fontSize: 10)
            : const pw.TextStyle(fontSize: 10),
      ),
    );
  }
}
