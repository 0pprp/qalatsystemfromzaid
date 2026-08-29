import 'package:excel/excel.dart';
import 'package:path_provider/path_provider.dart';
import 'package:intl/intl.dart';
import 'dart:io';
import 'package:permission_handler/permission_handler.dart';
import 'formatters.dart';

class ExcelExport {
  /// Generic export: list of maps to Excel
  static Future<void> exportList({
    required List<Map<String, dynamic>> data,
    required List<String> columns, // display header names
    required List<String> keys, // corresponding map keys
    required String fileName,
    Map<String, String Function(dynamic)>? formatters,
  }) async {
    final excel = Excel.createExcel();
    final sheet = excel['Sheet1'];

    // Header row
    for (int i = 0; i < columns.length; i++) {
      final cell = sheet.cell(CellIndex.indexByColumnRow(columnIndex: i, rowIndex: 0));
      cell.value = TextCellValue(columns[i]);
      cell.cellStyle = CellStyle(
        bold: true,
        fontColorHex: ExcelColor.fromHexString('#FFFFFF'),
        backgroundColorHex: ExcelColor.fromHexString('#1e5799'),
        horizontalAlign: HorizontalAlign.Center,
      );
    }

    // Data rows
    for (int r = 0; r < data.length; r++) {
      final row = data[r];
      for (int c = 0; c < keys.length; c++) {
        final cell = sheet.cell(CellIndex.indexByColumnRow(columnIndex: c, rowIndex: r + 1));
        dynamic value = row[keys[c]];

        if (formatters != null && formatters.containsKey(keys[c])) {
          value = formatters[keys[c]]!(value);
        }

        if (value is num) {
          cell.value = DoubleCellValue(value.toDouble());
        } else {
          cell.value = TextCellValue(value?.toString() ?? '');
        }
      }
    }

    await _saveAndOpen(excel, fileName);
  }

  /// Export delegations statistics table
  static Future<void> exportDelegateStats({
    required List<Map<String, dynamic>> data,
    required String fileName,
    bool excludeZeroed = false,
  }) async {
    final columns = excludeZeroed
        ? ['المندوب', 'عدد العملاء', 'سعر البيع', 'الكلفة', 'القسط', 'عدد المواد', 'المستلم']
        : ['المندوب', 'عدد العملاء', 'سعر البيع', 'الكلفة', 'القسط', 'عدد المواد', 'المستلم', 'المصفرين', 'سعر المصفرين', 'قسط المصفرين'];

    final keys = excludeZeroed
        ? ['delegateName', 'numberOfCustomer', 'amountPrice', 'amountCost', 'amountDay', 'numberOfItemSale', 'amountReceipt']
        : ['delegateName', 'numberOfCustomer', 'amountPrice', 'amountCost', 'amountDay', 'numberOfItemSale', 'amountReceipt', 'numberOfCustomerZero', 'amountPriceZero', 'amountDayZero'];

    await exportList(data: data, columns: columns, keys: keys, fileName: fileName, formatters: {
      'amountPrice': (v) => Formatters.formatCurrencyNoZero(v),
      'amountCost': (v) => Formatters.formatCurrencyNoZero(v),
      'amountDay': (v) => Formatters.formatCurrencyNoZero(v),
      'amountReceipt': (v) => Formatters.formatCurrencyNoZero(v),
      'amountPriceZero': (v) => Formatters.formatCurrencyNoZero(v),
      'amountDayZero': (v) => Formatters.formatCurrencyNoZero(v),
    });
  }

  /// Export customers list
  static Future<void> exportCustomers(List<Map<String, dynamic>> data, {required String fileName}) async {
    await exportList(data: data, columns: [
      'الزبون', 'المندوب', 'الهاتف', 'سعر البيع', 'القسط', 'الواصل', 'الباقي', 'آخر تسديد'
    ], keys: [
      'customerName', 'delegateName', 'phoneNumber', 'amountTotalSales', 'amountDaySales', 'receiptsTotal', 'amountRemaining', 'lastPaymentDate'
    ], fileName: fileName, formatters: {
      'amountTotalSales': (v) => Formatters.formatCurrency(v),
      'amountDaySales': (v) => Formatters.formatCurrency(v),
      'receiptsTotal': (v) => Formatters.formatCurrency(v),
      'amountRemaining': (v) => Formatters.formatCurrency(v),
      'lastPaymentDate': (v) => v?.toString().isNotEmpty == true ? v.toString().split('T')[0] : '',
    });
  }

  /// Export payments
  static Future<void> exportPayments(List<Map<String, dynamic>> data, {required String fileName}) async {
    await exportList(data: data, columns: [
      'الزبون', 'المندوب', 'المبلغ', 'تاريخ التسديد'
    ], keys: [
      'customerName', 'delegateName', 'amountDenar', 'paymentDate'
    ], fileName: fileName, formatters: {
      'amountDenar': (v) => Formatters.formatCurrency(v),
      'paymentDate': (v) => v?.toString().isNotEmpty == true ? v.toString().split('T')[0] : '',
    });
  }

  /// Export sales
  static Future<void> exportSales(List<Map<String, dynamic>> data, {required String fileName}) async {
    await exportList(data: data, columns: [
      'رقم السند', 'الزبون', 'المندوب', 'المخزن', 'الأصناف', 'العدد', 'التاريخ', 'السعر الكلي', 'بعد الخصم', 'التقسيط', 'بعد خصم التقسيط', 'المستلم', 'المتبقي'
    ], keys: [
      'boundNumber', 'customerName', 'delegateName', 'storeName', 'itemsNames', 'numberOfItemsSales', 'dateCreate', 'amountTotalDenar', 'amountTotalSalesDenar', 'amountTotalDayDenar', 'amountDaySalesDenar', 'receiptsTotal', 'amountRemaining'
    ], fileName: fileName, formatters: {
      'amountTotalDenar': (v) => Formatters.formatCurrency(v),
      'amountTotalSalesDenar': (v) => Formatters.formatCurrency(v),
      'amountTotalDayDenar': (v) => Formatters.formatCurrency(v),
      'amountDaySalesDenar': (v) => Formatters.formatCurrency(v),
      'receiptsTotal': (v) => Formatters.formatCurrency(v),
      'amountRemaining': (v) => Formatters.formatCurrency(v),
      'dateCreate': (v) => v?.toString().isNotEmpty == true ? v.toString().split('T')[0] : '',
    });
  }

  /// Save and optionally share
  static Future<void> _saveAndOpen(Excel excel, String fileName) async {
    try {
      // Check permissions on Android
      if (await Permission.storage.request().isGranted || await Permission.manageExternalStorage.request().isGranted) {
        final dir = await getApplicationDocumentsDirectory();
        final file = File('${dir.path}/$fileName.xlsx');
        await file.writeAsBytes(excel.encode()!);
        // Success - file saved
      }
    } catch (_) {
      // Fallback: try downloads directory
      try {
        final dir = Directory('/storage/emulated/0/Download');
        if (await dir.exists()) {
          final file = File('${dir.path}/$fileName.xlsx');
          await file.writeAsBytes(excel.encode()!);
        }
      } catch (_) {}
    }
  }
}
