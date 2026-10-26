import 'dart:isolate';
import 'dart:typed_data';

import 'package:delegate_application/utils/PdfAssetCache.dart';
import 'package:delegate_application/utils/PrintUtils.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:pdf/pdf.dart';
import 'package:pdf/widgets.dart' as pw;

/// كشف حساب زبون — منفصل عن وصل التسديد (عرض/مشاركة فقط).
class AccountStatementPdf {
  AccountStatementPdf({
    required this.customerName,
    required this.saleDate,
    required this.salePrice,
    required this.receiptsTotal,
    required this.remaining,
    required this.lastPaymentDate,
    required this.paymentRows,
  });

  final String customerName;
  final String saleDate;
  final double salePrice;
  final double receiptsTotal;
  final double remaining;
  final String lastPaymentDate;
  final List<({String date, double amount})> paymentRows;

  /// Builds PDF off the UI isolate when possible.
  Future<Uint8List> buildBytes() async {
    final sw = Stopwatch()..start();
    final regular = await PdfAssetCache.getFontRegularBytes();
    final bold = await PdfAssetCache.getFontBoldBytes();

    // Only sendable primitives / lists / Uint8List copies.
    final args = <String, Object>{
      'customerName': customerName,
      'saleDate': saleDate,
      'salePrice': salePrice,
      'receiptsTotal': receiptsTotal,
      'remaining': remaining,
      'lastPaymentDate': lastPaymentDate,
      'paymentRows': <List<Object>>[
        for (final r in paymentRows) <Object>[r.date, r.amount],
      ],
      'fontRegular': regular,
      'fontBold': bold,
    };

    try {
      final bytes =
          await Isolate.run(() => buildAccountStatementPdfBytes(args));
      // ignore: avoid_print
      print('AccountStatementPdf OK isolate ${sw.elapsedMilliseconds}ms '
          '${bytes.length}b rows=${paymentRows.length}');
      return bytes;
    } catch (e, st) {
      // ignore: avoid_print
      print('AccountStatementPdf isolate failed: $e\n$st');
      try {
        final bytes = await compute(buildAccountStatementPdfBytes, args);
        // ignore: avoid_print
        print('AccountStatementPdf OK compute ${sw.elapsedMilliseconds}ms');
        return bytes;
      } catch (e2, st2) {
        // ignore: avoid_print
        print('AccountStatementPdf compute failed: $e2\n$st2');
        // Last resort: main isolate (spinner already showing).
        final bytes = await buildAccountStatementPdfBytes(args);
        // ignore: avoid_print
        print('AccountStatementPdf OK main ${sw.elapsedMilliseconds}ms');
        return bytes;
      }
    }
  }

  Future<void> preview(BuildContext context) async {
    await PrintUtils.previewPdf(context, (_) => buildBytes());
  }
}

String _fmtNum(double n) {
  final s = n.round().abs().toString();
  final buf = StringBuffer();
  for (var i = 0; i < s.length; i++) {
    if (i > 0 && (s.length - i) % 3 == 0) buf.write(',');
    buf.write(s[i]);
  }
  final body = buf.toString();
  return n < 0 ? '-$body' : body;
}

/// Top-level — safe for Isolate.run / compute.
Future<Uint8List> buildAccountStatementPdfBytes(Map<String, Object> p) async {
  final regular = p['fontRegular'] as Uint8List;
  final bold = p['fontBold'] as Uint8List;
  final ttf = pw.Font.ttf(ByteData.sublistView(regular));
  final ttfBold = pw.Font.ttf(ByteData.sublistView(bold));

  final rawRows = (p['paymentRows'] as List)
      .map((e) => e as List)
      .map((e) => (date: '${e[0]}', amount: (e[1] as num).toDouble()))
      .toList();

  final pdf = pw.Document();
  final theme = pw.ThemeData.withFont(base: ttf, bold: ttfBold);

  pw.Widget line(String k, String v) => pw.Padding(
        padding: const pw.EdgeInsets.only(bottom: 5),
        child: pw.Row(
          mainAxisAlignment: pw.MainAxisAlignment.spaceBetween,
          children: [
            pw.Expanded(
              child: pw.Text(v, style: const pw.TextStyle(fontSize: 11)),
            ),
            pw.SizedBox(width: 8),
            pw.Text(k,
                style: pw.TextStyle(
                    fontSize: 11, fontWeight: pw.FontWeight.bold)),
          ],
        ),
      );

  final children = <pw.Widget>[
    pw.Text('كشف حساب',
        style: pw.TextStyle(fontSize: 18, fontWeight: pw.FontWeight.bold),
        textAlign: pw.TextAlign.center),
    pw.SizedBox(height: 12),
    line('اسم الزبون', '${p['customerName']}'),
    line(
        'تاريخ المبيع',
        '${p['saleDate']}'.isEmpty ? '—' : '${p['saleDate']}'),
    line('سعر البيع', '${_fmtNum((p['salePrice'] as num).toDouble())} دع'),
    line('الواصل', '${_fmtNum((p['receiptsTotal'] as num).toDouble())} دع'),
    line('الباقي', '${_fmtNum((p['remaining'] as num).toDouble())} دع'),
    line(
        'آخر تسديد',
        '${p['lastPaymentDate']}'.isEmpty
            ? '—'
            : '${p['lastPaymentDate']}'),
    pw.SizedBox(height: 10),
    pw.Text('التسديدات',
        style: pw.TextStyle(fontSize: 13, fontWeight: pw.FontWeight.bold)),
    pw.SizedBox(height: 6),
  ];

  if (rawRows.isEmpty) {
    children.add(pw.Text('لا توجد تسديدات مسجّلة',
        style: const pw.TextStyle(fontSize: 11)));
  } else {
    // One widget per payment so MultiPage can split across pages.
    for (final pay in rawRows) {
      children.add(
        pw.Padding(
          padding: const pw.EdgeInsets.only(bottom: 3),
          child: pw.Row(
            mainAxisAlignment: pw.MainAxisAlignment.spaceBetween,
            children: [
              pw.Text('${_fmtNum(pay.amount)} دع',
                  style: const pw.TextStyle(fontSize: 10)),
              pw.Text(pay.date, style: const pw.TextStyle(fontSize: 10)),
            ],
          ),
        ),
      );
    }
  }

  pdf.addPage(
    pw.MultiPage(
      pageFormat: PdfPageFormat.a4,
      margin: const pw.EdgeInsets.all(24),
      theme: theme,
      textDirection: pw.TextDirection.rtl,
      build: (ctx) => children,
    ),
  );

  return pdf.save();
}
