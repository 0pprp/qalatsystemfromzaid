import 'package:delegate_application/utils/PdfAssetCache.dart';
import 'package:delegate_application/utils/PrintUtils.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:intl/intl.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:pdf/pdf.dart';
import 'package:pdf/widgets.dart' as pw;
import 'dart:typed_data';

class ReportReceipt {
  final String customerName;
  final String receiptName;
  final String delegateName;
  final String itemsNames;
  final double amountTotalSales;
  final double receiptsTotal;
  final double amountRemaining;
  final double amountPush;
  final String phoneNumberCompany;
  final List<String> lastSevenDays;
  final List<double> lastSevenAmounts;
  final String countReceiptDevice;

  ReportReceipt({
    required this.customerName,
    required this.receiptName,
    required this.delegateName,
    required this.itemsNames,
    required this.amountTotalSales,
    required this.receiptsTotal,
    required this.amountRemaining,
    required this.amountPush,
    required this.phoneNumberCompany,
    required this.lastSevenDays,
    required this.lastSevenAmounts,
    required this.countReceiptDevice,
  });

  String _formatNumber(double number) {
    final formatter = NumberFormat('#,###');
    return formatter.format(number);
  }

  Future<void> printReceipt(BuildContext context) async {
    await initializeDateFormatting('ar', null);
    if (!context.mounted) return;
    await PrintUtils.previewPdf(context, (format) => _generatePdf(format));
  }

  Future<Uint8List> _generatePdf(PdfPageFormat format) async {
    final pdf = pw.Document();
    final ttf = await PdfAssetCache.getFontRegular();
    final ttfBold = await PdfAssetCache.getFontBold();

    final String currentDate = DateFormat.yMMMMd('ar').format(DateTime.now());
    final String currentTime = DateFormat.jm().format(DateTime.now());

    pdf.addPage(
      pw.Page(
        pageFormat: PrintUtils.ensureFiniteHeight(format),
        margin: const pw.EdgeInsets.all(20),
        theme: pw.ThemeData.withFont(base: ttf, bold: ttfBold),
        build: (pw.Context context) {
          return pw.Directionality(
            textDirection: pw.TextDirection.rtl,
            child: pw.Column(
              children: [
                pw.Text("شركة المنهاج الذهبي",
                    style: pw.TextStyle(
                        fontSize: 18, fontWeight: pw.FontWeight.bold)),
                pw.Text("وصل استلام", style: const pw.TextStyle(fontSize: 14)),
                pw.SizedBox(height: 10),
                pw.Table(
                  border: pw.TableBorder.all(),
                  columnWidths: {
                    0: const pw.FlexColumnWidth(2), // Value
                    1: const pw.FlexColumnWidth(1), // Label
                  },
                  children: [
                    _buildRow("رقم الوصل",
                        (int.parse(countReceiptDevice) + 1).toString()),
                    _buildRow("الزبون", customerName),
                    _buildRow("المادة المباعة", itemsNames),
                    _buildRow("المبلغ الكلي", _formatNumber(amountTotalSales),
                        isPrice: true),
                    _buildRow("المبلغ الواصل", _formatNumber(receiptsTotal),
                        isPrice: true),
                    _buildRow("المبلغ المتبقي", _formatNumber(amountRemaining),
                        isPrice: true),
                    _buildRow("المبلغ المدفوع", _formatNumber(amountPush),
                        isPrice: true),
                    _buildRow("تاريخ الدفع", currentDate),
                    _buildRow("الوقت", currentTime),
                    _buildRow("اسم القائمة", delegateName),
                    _buildRow("اسم الجابي", receiptName),
                  ],
                ),
                pw.SizedBox(height: 10),
                pw.Align(
                    alignment: pw.Alignment.centerRight,
                    child: pw.Text("تسديدات اخر سبعة ايام",
                        style: const pw.TextStyle(fontSize: 12))),
                pw.Table(
                  border: pw.TableBorder.all(),
                  columnWidths: {
                    0: const pw.FlexColumnWidth(2),
                    1: const pw.FlexColumnWidth(1),
                  },
                  children: List.generate(lastSevenDays.length, (index) {
                    return _buildRow(lastSevenDays[index],
                        _formatNumber(lastSevenAmounts[index]),
                        isPrice: true);
                  }),
                ),
                pw.SizedBox(height: 20),
                pw.Text("رقم الشكاوى: $phoneNumberCompany",
                    style: const pw.TextStyle(fontSize: 10)),
                pw.Text("وقت الدوام: من الساعة 9 صباحا الى الساعة 8 مساء",
                    style: const pw.TextStyle(fontSize: 10)),
              ],
            ),
          );
        },
      ),
    );
    return pdf.save();
  }

  pw.TableRow _buildRow(String label, String value, {bool isPrice = false}) {
    return pw.TableRow(children: [
      pw.Container(
        padding: const pw.EdgeInsets.all(4),
        alignment: pw.Alignment.center,
        child: pw.Text(isPrice ? "$value دع" : value,
            textAlign: pw.TextAlign.center,
            style: const pw.TextStyle(fontSize: 10)),
      ),
      pw.Container(
        color: PdfColors.grey200,
        padding: const pw.EdgeInsets.all(4),
        alignment: pw.Alignment.center,
        child: pw.Text(label,
            style: pw.TextStyle(fontWeight: pw.FontWeight.bold, fontSize: 10),
            textAlign: pw.TextAlign.center),
      ),
    ]);
  }
}
