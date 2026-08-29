import 'package:delegate_application/utils/PdfAssetCache.dart';
import 'package:delegate_application/services/DatabaseHelper.dart';
import 'package:delegate_application/utils/PrintUtils.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:intl/intl.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:sqflite/sqflite.dart';
import 'package:pdf/pdf.dart';
import 'package:pdf/widgets.dart' as pw;
import 'dart:typed_data';

class CustomerReceiptReport {
  final String delegateName;
  final String receiptName;
  final int numberOfReceipts;
  final double amountReceiptTotal;
  final List<Map<String, dynamic>> receiptData;

  CustomerReceiptReport({
    required this.delegateName,
    required this.receiptName,
    required this.numberOfReceipts,
    required this.amountReceiptTotal,
    required this.receiptData,
  });

  String _formatNumber(double number) {
    final formatter = NumberFormat('#,###');
    return formatter.format(number);
  }

  Future<void> printReceipt(BuildContext context) async {
    // Show loading
    showDialog(
      context: context,
      barrierDismissible: false,
      builder: (ctx) => const Center(child: CircularProgressIndicator()),
    );

    try {
      await initializeDateFormatting('ar', null);

      final Database db = await DatabaseHelper().database;

      // Fetch all required installments at once
      final List<int> customerIds =
          receiptData.map((e) => e['CustomerId'] as int).toList();
      final List<Map<String, dynamic>> installments = await db.query(
        'Customer',
        columns: ['CustomerId', 'AmountDaySales'],
        where:
            'CustomerId IN (${List.filled(customerIds.length, '?').join(',')})',
        whereArgs: customerIds,
      );

      final Map<int, double> installmentMap = {
        for (var item in installments)
          item['CustomerId'] as int: (item['AmountDaySales'] as num).toDouble()
      };

      // Pre-calculate data rows
      List<List<String>> rows = [];
      double totalAmountDaySale = 0;

      for (var customer in receiptData) {
        int customerId = customer['CustomerId'];
        double amountDaySales = installmentMap[customerId] ?? 0.0;

        if (customer['Amount'] > 0) {
          totalAmountDaySale += amountDaySales;
        }
        rows.add([
          customer['CustomerName'],
          "${_formatNumber(amountDaySales)} دع",
          "${_formatNumber((customer['Amount'] as num).toDouble())} دع",
        ]);
      }

      if (!context.mounted) return;
      Navigator.of(context).pop(); // pop loading

      await PrintUtils.previewPdf(
          context, (format) => _generatePdf(format, rows, totalAmountDaySale));
    } catch (e) {
      if (context.mounted) {
        Navigator.of(context).pop();
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Error generating PDF: $e')),
        );
      }
    }
  }

  Future<Uint8List> _generatePdf(PdfPageFormat format, List<List<String>> rows,
      double totalAmountDaySale) async {
    final pdf = pw.Document();
    final ttf = await PdfAssetCache.getFontRegular();
    final ttfBold = await PdfAssetCache.getFontBold();

    final String currentDate = DateFormat.yMMMMd('ar').format(DateTime.now());

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
                pw.Text("المسددين", style: const pw.TextStyle(fontSize: 14)),
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
                _buildRow("عدد التسديدات", numberOfReceipts.toString()),
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
                    _buildCell("القسط", isHeader: true),
                    _buildCell("المسدد", isHeader: true),
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
            pw.SizedBox(height: 15),
            // Totals Table
            pw.Table(
              border: pw.TableBorder.all(),
              columnWidths: {
                0: const pw.FlexColumnWidth(2),
                1: const pw.FlexColumnWidth(1),
              },
              children: [
                _buildRow(
                    "مجموع القسط", "${_formatNumber(totalAmountDaySale)} دع"),
                _buildRow("مجموع التسديدات",
                    "${_formatNumber(amountReceiptTotal)} دع"),
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
      _buildCell(label, isHeader: true), // Label as header style for background
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
