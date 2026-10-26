import 'dart:typed_data';

import 'package:flutter/services.dart';
import 'package:pdf/widgets.dart' as pw;

class PdfAssetCache {
  static Uint8List? _fontRegularBytes;
  static Uint8List? _fontBoldBytes;
  static pw.Font? _fontRegular;
  static pw.Font? _fontBold;
  static pw.MemoryImage? _logo;

  /// Loads font bytes only (cheap). Does NOT parse TTF on the UI isolate.
  static Future<void> precache() async {
    await Future.wait([
      getFontRegularBytes(),
      getFontBoldBytes(),
    ]);
  }

  static Future<Uint8List> getFontRegularBytes() async {
    if (_fontRegularBytes != null) return _fontRegularBytes!;
    final data = await rootBundle.load('assets/fonts/Cairo-Regular.ttf');
    // Standalone copy — ByteData views cannot be sent to Isolates.
    _fontRegularBytes = Uint8List.fromList(
      data.buffer.asUint8List(data.offsetInBytes, data.lengthInBytes),
    );
    return _fontRegularBytes!;
  }

  static Future<Uint8List> getFontBoldBytes() async {
    if (_fontBoldBytes != null) return _fontBoldBytes!;
    final data = await rootBundle.load('assets/fonts/Cairo-Bold.ttf');
    _fontBoldBytes = Uint8List.fromList(
      data.buffer.asUint8List(data.offsetInBytes, data.lengthInBytes),
    );
    return _fontBoldBytes!;
  }

  static Future<pw.Font> getFontRegular() async {
    if (_fontRegular != null) return _fontRegular!;
    final bytes = await getFontRegularBytes();
    _fontRegular = pw.Font.ttf(ByteData.sublistView(bytes));
    return _fontRegular!;
  }

  static Future<pw.Font> getFontBold() async {
    if (_fontBold != null) return _fontBold!;
    final bytes = await getFontBoldBytes();
    _fontBold = pw.Font.ttf(ByteData.sublistView(bytes));
    return _fontBold!;
  }

  static Future<pw.MemoryImage> getLogo() async {
    if (_logo != null) return _logo!;
    final data = await rootBundle.load('assets/icons/LogoCompany.png');
    _logo = pw.MemoryImage(data.buffer.asUint8List());
    return _logo!;
  }
}
