import 'package:flutter/services.dart';
import 'package:pdf/widgets.dart' as pw;

class PdfAssetCache {
  static pw.Font? _fontRegular;
  static pw.Font? _fontBold;
  static pw.MemoryImage? _logo;

  static Future<void> precache() async {
    await Future.wait([
      getFontRegular(),
      getFontBold(),
    ]);
  }

  static Future<pw.Font> getFontRegular() async {
    if (_fontRegular != null) return _fontRegular!;
    final data = await rootBundle.load("assets/fonts/Cairo-Regular.ttf");
    _fontRegular = pw.Font.ttf(data);
    return _fontRegular!;
  }

  static Future<pw.Font> getFontBold() async {
    if (_fontBold != null) return _fontBold!;
    final data = await rootBundle.load("assets/fonts/Cairo-Bold.ttf");
    _fontBold = pw.Font.ttf(data);
    return _fontBold!;
  }

  static Future<pw.MemoryImage> getLogo() async {
    if (_logo != null) return _logo!;
    final data = await rootBundle.load("assets/icons/LogoCompany.png");
    _logo = pw.MemoryImage(data.buffer.asUint8List());
    return _logo!;
  }
}
