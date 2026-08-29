import 'package:flutter/services.dart';
import 'package:pdf/pdf.dart';
import 'package:pdf/widgets.dart' as pw;
import 'package:printing/printing.dart';

class TrustReceiptPdfService {
  static Future<void> printReceipt(Map<String, dynamic> data) async {
    final pdf = pw.Document();

    final fontData = await rootBundle.load("assets/fonts/Cairo-Regular.ttf");
    final ttf = pw.Font.ttf(fontData);
    final fontBoldData = await rootBundle.load("assets/fonts/Cairo-Bold.ttf");
    final ttfBold = pw.Font.ttf(fontBoldData);

    pw.ImageProvider? logoImage;
    try {
      final logoData = await rootBundle.load('assets/icons/logo_last.png');
      logoImage = pw.MemoryImage(logoData.buffer.asUint8List());
    } catch (_) {}

    pdf.addPage(pw.Page(
      pageFormat: PdfPageFormat.a4,
      margin: const pw.EdgeInsets.all(22),
      textDirection: pw.TextDirection.rtl,
      theme: pw.ThemeData.withFont(base: ttf, bold: ttfBold),
      build: (ctx) => _buildContractPage(ctx, data, logoImage),
    ));

    pdf.addPage(pw.Page(
      pageFormat: PdfPageFormat.a4,
      margin: const pw.EdgeInsets.all(22),
      textDirection: pw.TextDirection.rtl,
      theme: pw.ThemeData.withFont(base: ttf, bold: ttfBold),
      build: (ctx) => _buildTrustReceiptPage(ctx, data, logoImage),
    ));

    await Printing.layoutPdf(
      onLayout: (PdfPageFormat format) async => pdf.save(),
      name: "Trust_Receipt_${data['contractNumber'] ?? 'Doc'}.pdf",
    );
  }

  static String _f(dynamic s) => s?.toString() ?? '';
  static String _dd(dynamic d) => d != null ? (d is String ? d.substring(0, 10) : d.toString().substring(0, 10)) : '';

  // ──────────────────── PAGE 1: عقد بيع ────────────────────
  static pw.Widget _buildContractPage(
      pw.Context _, Map<String, dynamic> d, pw.ImageProvider? logo) {
    final s = const pw.TextStyle(fontSize: 10.5, height: 1.65);
    final sb = const pw.TextStyle(fontSize: 10.5, height: 1.65, fontWeight: pw.FontWeight.bold);
    final title = const pw.TextStyle(fontSize: 15, fontWeight: pw.FontWeight.bold);

    return pw.Container(
      padding: const pw.EdgeInsets.all(14),
      decoration: pw.BoxDecoration(border: pw.Border.all(width: 1.5)),
      child: pw.Column(crossAxisAlignment: pw.CrossAxisAlignment.stretch, children: [
        if (logo != null) pw.Center(child: pw.Image(logo, height: 40)),
        pw.SizedBox(height: 4),
        pw.Center(child: pw.Text("عقد بيع", style: title)),
        pw.SizedBox(height: 8),
        pw.RichText(text: pw.TextSpan(children: [
          pw.TextSpan(text: "حرصًا من الطرفين على إتمام عملية البيع والشراء وفقًا للأحكام والشروط المتفق عليها، فقد تم تحرير هذا العقد بتاريخ ", style: s),
          pw.TextSpan(text: _dd(d['contractDate']), style: sb),
        ])),
        pw.SizedBox(height: 10),
        pw.Text("الطرف الأول: شركة قمة الضمان للتجارة العامة محدودة المسؤولية.", style: sb),
        pw.SizedBox(height: 8),
        _r("الطرف الثاني:", _f(d['buyerName'])),
        _r("البطاقة الوطنية:", _f(d['buyerNationalCardNumber'])),
        _r("محافظة:", _f(d['buyerGovernorate'])),
        _r("أقرب نقطة دالة:", _f(d['buyerNearestLandmark'])),
        _r("رقم الهاتف:", _f(d['buyerPhoneNumber'])),
        _r("الانتساب:", _f(d['buyerAffiliation'])),
        _r("اسم المختار:", _f(d['buyerMukhtarName'])),
        _r("رقم مركز التموين:", _f(d['buyerRationCenterNumber'])),
        pw.SizedBox(height: 8),
        pw.Text("1. باع الطرف الأول إلى الطرف الثاني بضاعة نوع ${_f(d['productType'])} ${_f(d['productName'])}", style: s),
        pw.Text("   بسعر كلي والبالغ قدره رقمًا ${_f(d['totalAmountNumber'])}   كتابة ${_f(d['totalAmountText'])}", style: s),
        pw.SizedBox(height: 5),
        pw.Text("2. يلتزم الطرف الثاني بتسديد المبلغ الكلي على شكل دفعات بقسط يومي وقدره رقمًا ${_f(d['installmentAmount'])}   كتابة ${_f(d['firstInstallmentAmount'])}", style: s),
        pw.Text("   ويبدأ القسط الأول من تاريخ توقيع هذا العقد ويستمر دون توقف إلى نهاية تسديد كامل المبلغ.", style: s),
        pw.SizedBox(height: 5),
        pw.Text("3. على الطرف الثاني إشعار الطرف الأول عند انتقال محل عمله أو سكنه ويبلغ الطرف الأول بعنوان محل عمله أو سكنه الجديد.", style: s),
        pw.SizedBox(height: 4),
        pw.Text("4. يسلم الطرف الثاني إلى الطرف الأول أمانة مثبتة مقدارها ${_f(d['receiptAmountNumber'])} وذلك بحوالة أو وصل أمانة منظمة قانونيًا.", style: s),
        pw.SizedBox(height: 4),
        pw.Text("5. الطرف الثاني مسؤول عن فحص البضاعة عند استلامها قبل توقيع العقد للتأكد من خلوها من أي عيب أو تلف.", style: s),
        pw.SizedBox(height: 4),
        pw.Text("6. يتحمل الطرف المخالف عن الالتزام بهذا العقد كافة تكاليف الدعوى بما فيها الرسوم وأتعاب المحاماة.", style: s),
        pw.SizedBox(height: 4),
        pw.Text("7. في حال إخلال الطرف الثاني بأي شرط من شروط هذا العقد، يلتزم بدفع تعويض للطرف الآخر مبلغ قدره نفس قيمة العقد.", style: s),
        pw.SizedBox(height: 4),
        pw.Text("8. في حال تخلف الطرف الثاني عن التسديد 7 مرات متوالية أو متقطعة يترتب عليه استحقاق كامل المبلغ المتبقي فورًا.", style: s),
        pw.SizedBox(height: 4),
        pw.Text("9. يتحمل محكمة النجف الأشرف في أي نزاع مدني أو جزائي بخصوص الدعاوى الناشئة عن هذا العقد.", style: s),
        pw.SizedBox(height: 4),
        pw.Text("10. يكون هذا العقد ملزم للطرفين وورثتهم.", style: s),
        pw.SizedBox(height: 10),
        pw.Text("أني الموقع أدناه ${_f(d['salesRepresentativeName'])} أتعهد بأني اشتريت البضاعة إلى الطرف الثاني وقد تم استلامها من قبله وفقًا لتعليمات الشركة، وقد تأكدت من جميع معلومات الزبون المذكورة في هذا العقد أعلاه ومنها المستمسكات الأصلية وعنوانه المثبت أعلاه ورقم موبايله، وعليه وقعت.", style: s),
        pw.SizedBox(height: 8),
        pw.Text("أني ${_f(d['cashierName'])} أمين صندوق الفرع أشهد بأن مندوب المبيعات قد وقع أمامي.", style: s),
        pw.SizedBox(height: 30),
        // 4 signatures row
        pw.Row(mainAxisAlignment: pw.MainAxisAlignment.spaceBetween, children: [
          _sig("الطرف الأول", _f(d['firstPartySignature'])),
          _sig("أمين الصندوق", _f(d['cashierSignature'])),
          _sig("الطرف الثاني", _f(d['secondPartySignature'])),
          _sig("مندوب المبيعات", _f(d['salesRepresentativeSignature'])),
        ]),
      ]),
    );
  }

  static pw.Widget _sig(String label, String val) {
    return pw.SizedBox(width: 85, child: pw.Column(children: [
      pw.Text(label, style: const pw.TextStyle(fontSize: 9, fontWeight: pw.FontWeight.bold), textAlign: pw.TextAlign.center),
      pw.SizedBox(height: 25),
      pw.Text(val.isNotEmpty ? val : "..................", style: const pw.TextStyle(fontSize: 8), textAlign: pw.TextAlign.center),
    ]));
  }

  static pw.Widget _r(String label, String value) {
    return pw.Row(children: [
      pw.Text(label, style: const pw.TextStyle(fontSize: 10.5, fontWeight: pw.FontWeight.bold)),
      pw.SizedBox(width: 6),
      pw.Expanded(child: pw.Text(value, style: const pw.TextStyle(fontSize: 10.5))),
    ]);
  }

  // ──────────────────── PAGE 2: وصل أمانة ────────────────────
  static pw.Widget _buildTrustReceiptPage(
      pw.Context _, Map<String, dynamic> d, pw.ImageProvider? logo) {
    final s = const pw.TextStyle(fontSize: 12, height: 2.0);
    final sb = const pw.TextStyle(fontSize: 12, height: 2.0, fontWeight: pw.FontWeight.bold);
    final title = const pw.TextStyle(fontSize: 18, fontWeight: pw.FontWeight.bold);

    return pw.Container(
      padding: const pw.EdgeInsets.all(14),
      decoration: pw.BoxDecoration(border: pw.Border.all(width: 1.5)),
      child: pw.Column(crossAxisAlignment: pw.CrossAxisAlignment.stretch, children: [
        if (logo != null) pw.Center(child: pw.Image(logo, height: 40)),
        pw.SizedBox(height: 6),
        pw.Center(child: pw.Text("وصل أمانة", style: title)),
        pw.SizedBox(height: 20),

        pw.Text("المبلغ قيمة: ${_f(d['receiptAmountNumber'])}", style: sb),
        pw.SizedBox(height: 6),
        pw.Text("كتابة: ${_f(d['receiptAmountText'])}", style: sb),
        pw.SizedBox(height: 22),

        pw.Text("إني الموقع أدناه وبحضوري الشركة قمة الضمان للتجارة العامة محدودة المسؤولية بمبلغ ${_f(d['receiptAmountNumber'])} إعلاه أتعهد بأن أعيده متى ما طلبت الشركة مني ولا يحق لي التأخير.", style: s),
        pw.SizedBox(height: 20),

        _l("اسم المسلم:", _f(d['delivererName'])),
        _l("رقم البطاقة أو الجنسية:", _f(d['identityDocumentNumber'])),
        _l("اسم المختار:", _f(d['buyerMukhtarName'])),
        _l("العنوان:", _f(d['address'])),

        pw.SizedBox(height: 20),
        pw.Text("بصمة المسلم:   ....................................................", style: s),
        pw.SizedBox(height: 10),
        pw.Text("توقيع المسلم: ....................................................", style: s),

        pw.SizedBox(height: 35),

        // Witnesses
        pw.Row(mainAxisAlignment: pw.MainAxisAlignment.spaceBetween, children: [
          pw.Expanded(child: pw.Column(crossAxisAlignment: pw.CrossAxisAlignment.start, children: [
            pw.Text("الشاهد الأول", style: sb),
            pw.SizedBox(height: 8),
            pw.Text("الاسم: ${_f(d['firstWitnessName'])}", style: s),
            pw.SizedBox(height: 4),
            pw.Text("التوقيع: ${_f(d['firstWitnessSignature']).isNotEmpty ? _f(d['firstWitnessSignature']) : '................................'}",
                style: s),
          ])),
          pw.SizedBox(width: 40),
          pw.Expanded(child: pw.Column(crossAxisAlignment: pw.CrossAxisAlignment.start, children: [
            pw.Text("الشاهد الثاني", style: sb),
            pw.SizedBox(height: 8),
            pw.Text("الاسم: ${_f(d['secondWitnessName'])}", style: s),
            pw.SizedBox(height: 4),
            pw.Text("التوقيع: ${_f(d['secondWitnessSignature']).isNotEmpty ? _f(d['secondWitnessSignature']) : '................................'}",
                style: s),
          ])),
        ]),
      ]),
    );
  }

  static pw.Widget _l(String label, String value) {
    return pw.Padding(
      padding: const pw.EdgeInsets.symmetric(vertical: 5),
      child: pw.Row(children: [
        pw.Text(label, style: const pw.TextStyle(fontSize: 12, fontWeight: pw.FontWeight.bold)),
        pw.SizedBox(width: 8),
        pw.Expanded(child: pw.Text(value, style: const pw.TextStyle(fontSize: 12))),
      ]),
    );
  }
}
