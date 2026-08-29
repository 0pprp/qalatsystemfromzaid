import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:pdf/pdf.dart';
import 'package:printing/printing.dart';
import 'package:shared_preferences/shared_preferences.dart';

class PrintUtils {
  static const String _printerKey = 'selected_printer_url';
  static const String _pageWidthKey = 'pref_page_width';
  static const String _pageHeightKey = 'pref_page_height';
  static const String _pageMarginKey = 'pref_page_margin';

  // Save preferred page format
  static Future<void> savePageFormat(PdfPageFormat format) async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setDouble(_pageWidthKey, format.width);
    await prefs.setDouble(_pageHeightKey, format.height);
    await prefs.setDouble(_pageMarginKey,
        format.marginBottom); // using marginBottom as proxy for uniform margin
  }

  // Get preferred page format (defaults to A4)
  static Future<PdfPageFormat> getPageFormat() async {
    final prefs = await SharedPreferences.getInstance();
    final width = prefs.getDouble(_pageWidthKey);
    final height = prefs.getDouble(_pageHeightKey);
    final margin = prefs.getDouble(_pageMarginKey) ?? 20.0;

    if (width != null && height != null) {
      return PdfPageFormat(width, height,
          marginBottom: margin,
          marginTop: margin,
          marginLeft: margin,
          marginRight: margin);
    }
    return PdfPageFormat.a4;
  }

  // Helper to ensure height is finite for MultiPage widgets
  static PdfPageFormat ensureFiniteHeight(PdfPageFormat format) {
    if (format.height == double.infinity) {
      // 40 cm is very safe for memory and fits almost all receipts.
      // Long reports will automatically split into multiple 40cm pages.
      return format.copyWith(height: 40 * PdfPageFormat.cm);
    }
    return format;
  }

  static Future<void> previewPdf(
      BuildContext context, LayoutCallback build) async {
    final initialFormat = await getPageFormat();

    if (!context.mounted) return;

    await Navigator.push(
      context,
      MaterialPageRoute(
        builder: (context) => Scaffold(
          appBar: AppBar(
            title: const Text(
              "معاينة ومشاركة الوصل",
              style:
                  TextStyle(fontFamily: 'Cairo', fontWeight: FontWeight.bold),
            ),
            backgroundColor: Theme.of(context).primaryColor,
            centerTitle: true,
          ),
          body: PdfPreview(
            build: build,
            allowSharing: true,
            allowPrinting: true,
            initialPageFormat: initialFormat,
            dynamicLayout:
                false, // Forces the system print dialog to respect our PDF size
            pdfFileName: "receipt_${DateTime.now().millisecondsSinceEpoch}.pdf",
            canChangeOrientation: true,
            canChangePageFormat: true,
            // Add common thermal sizes to the list of formats
            pageFormats: const {
              'A4': PdfPageFormat.a4,
              'رول 80 مم': PdfPageFormat.roll80,
              'رول 58 مم': PdfPageFormat.roll57,
              'A5': PdfPageFormat.a5,
            },
            loadingWidget: const Center(
              child: CircularProgressIndicator(),
            ),
            onPageFormatChanged: (format) {
              savePageFormat(format);
            },
          ),
        ),
      ),
    );
  }

  // Allow user to select a default printer to bypass system dialog in the future
  static Future<void> selectPrinter(BuildContext context) async {
    try {
      final printer = await Printing.pickPrinter(context: context);
      if (printer != null) {
        final prefs = await SharedPreferences.getInstance();
        await prefs.setString(_printerKey, printer.url);
        if (context.mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(
              content: Text("تم تثبيت الطابعة: ${printer.name}",
                  style: const TextStyle(fontFamily: 'Cairo')),
              backgroundColor: Colors.blue,
            ),
          );
        }
      }
    } catch (e) {
      debugPrint("Error picking printer: $e");
    }
  }

  // Helper to reset printer setting
  static Future<void> resetPrinter() async {
    final SharedPreferences prefs = await SharedPreferences.getInstance();
    await prefs.remove(_printerKey);
    await prefs.remove(_pageWidthKey);
    await prefs.remove(_pageHeightKey);
    await prefs.remove(_pageMarginKey);
  }
}
