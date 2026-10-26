import "dart:io";
import "dart:typed_data";
import "dart:isolate";
import "package:flutter_test/flutter_test.dart";
import "package:delegate_application/services/account_statement_pdf.dart";

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  test("statement pdf builds in isolate", () async {
    final reg = File("assets/fonts/Cairo-Regular.ttf").readAsBytesSync();
    final bold = File("assets/fonts/Cairo-Bold.ttf").readAsBytesSync();
    final args = <String, Object>{
      "customerName": "اختبار",
      "saleDate": "2026/01/01",
      "salePrice": 1000.0,
      "receiptsTotal": 100.0,
      "remaining": 900.0,
      "lastPaymentDate": "2026/01/02",
      "paymentRows": <List<Object>>[
        <Object>["2026/01/02", 100.0],
      ],
      "fontRegular": Uint8List.fromList(reg),
      "fontBold": Uint8List.fromList(bold),
    };
    final bytes = await Isolate.run(() => buildAccountStatementPdfBytes(args));
    expect(bytes.length, greaterThan(100));
  });

  test("statement pdf includes all payments across pages", () async {
    final reg = File("assets/fonts/Cairo-Regular.ttf").readAsBytesSync();
    final bold = File("assets/fonts/Cairo-Bold.ttf").readAsBytesSync();
    final many = <List<Object>>[
      for (var i = 0; i < 80; i++)
        <Object>["2026/${((i ~/ 28) % 12) + 1}/${(i % 28) + 1}", 1000.0 + i],
    ];
    final args = <String, Object>{
      "customerName": "زبون كثير التسديدات",
      "saleDate": "2025/01/01",
      "salePrice": 100000.0,
      "receiptsTotal": 80000.0,
      "remaining": 20000.0,
      "lastPaymentDate": "2026/03/01",
      "paymentRows": many,
      "fontRegular": Uint8List.fromList(reg),
      "fontBold": Uint8List.fromList(bold),
    };
    final bytes = await Isolate.run(() => buildAccountStatementPdfBytes(args));
    expect(bytes.length, greaterThan(1000));
    final text = String.fromCharCodes(bytes);
    final pageCount = RegExp(r"/Type\s*/Page(?!s)").allMatches(text).length;
    expect(pageCount, greaterThanOrEqualTo(2),
        reason: "expected multi-page PDF for 80 payments, pages=$pageCount");
  });
}
