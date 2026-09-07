import 'package:flutter/services.dart';
import 'package:intl/intl.dart';

class MoneyFormat {
  static final NumberFormat _grouped = NumberFormat('#,##0', 'en_US');
  static const MoneyInputFormatter formatter = MoneyInputFormatter();

  static List<TextInputFormatter> get inputFormatters => [formatter];

  static String iqd(num value) => '${grouped(value)} د.ع';

  static String grouped(num value) => _grouped.format(value.round());

  static num? parse(String? text) {
    final raw = digitsOnly(text);
    if (raw.isEmpty) return null;
    return num.tryParse(raw);
  }

  static String digitsOnly(String? text) {
    if (text == null || text.isEmpty) return '';
    final buffer = StringBuffer();
    for (final unit in text.codeUnits) {
      if (unit >= 48 && unit <= 57) {
        buffer.writeCharCode(unit);
      } else if (unit >= 0x0660 && unit <= 0x0669) {
        buffer.writeCharCode(unit - 0x0660 + 48);
      } else if (unit >= 0x06F0 && unit <= 0x06F9) {
        buffer.writeCharCode(unit - 0x06F0 + 48);
      }
    }
    return buffer.toString();
  }
}

class MoneyInputFormatter extends TextInputFormatter {
  const MoneyInputFormatter();

  static const int maxDigits = 18;

  @override
  TextEditingValue formatEditUpdate(TextEditingValue oldValue, TextEditingValue newValue) {
    final digits = MoneyFormat.digitsOnly(newValue.text);
    if (digits.isEmpty) {
      return const TextEditingValue(text: '', selection: TextSelection.collapsed(offset: 0));
    }

    var trimmed = digits;
    if (trimmed.length > maxDigits) {
      trimmed = trimmed.substring(0, maxDigits);
    }
    trimmed = trimmed.replaceFirst(RegExp(r'^0+(?=\d)'), '');
    final formatted = MoneyFormat.grouped(int.parse(trimmed));
    final digitsBefore = _digitCount(newValue.text, newValue.selection.baseOffset);
    final clampedBefore = digitsBefore > trimmed.length ? trimmed.length : digitsBefore;
    return TextEditingValue(
      text: formatted,
      selection: TextSelection.collapsed(offset: _offsetForDigits(formatted, clampedBefore)),
    );
  }

  static int _digitCount(String text, int cursor) {
    final end = cursor.clamp(0, text.length);
    var count = 0;
    for (var i = 0; i < end; i++) {
      final unit = text.codeUnitAt(i);
      if ((unit >= 48 && unit <= 57) ||
          (unit >= 0x0660 && unit <= 0x0669) ||
          (unit >= 0x06F0 && unit <= 0x06F9)) {
        count++;
      }
    }
    return count;
  }

  static int _offsetForDigits(String formatted, int digits) {
    if (digits <= 0) return 0;
    var seen = 0;
    for (var i = 0; i < formatted.length; i++) {
      final unit = formatted.codeUnitAt(i);
      if (unit >= 48 && unit <= 57) {
        seen++;
        if (seen >= digits) return i + 1;
      }
    }
    return formatted.length;
  }
}

class EvaluationLabels {
  static const map = {
    1: 'مرفوض',
    2: 'مقبول',
    3: 'جيد',
    4: 'جيد جداً',
    5: 'ممتاز',
  };

  static String of(int? level) => map[level] ?? 'غير محدد';
}

class SalesStatusLabels {
  static String of(String? status) {
    switch (status) {
      case 'Rejected':
        return 'مرفوض';
      case 'Completed':
        return 'تم البيع';
      case 'DocumentsReady':
        return 'تم البيع';
      case 'DocumentsPending':
        return 'تم البيع — المستندات قيد التوليد';
      default:
        return 'معلق';
    }
  }
}

class SalesRequestStatusLabels {
  static String of(String? status) => switch (status) {
        'New' => 'طلبات البيع',
        'Assigned' => 'طلبات البيع',
        'Viewed' => 'طلبات البيع',
        'Pending' => 'معلقة',
        'PreparedForSale' => 'جاهز للبيع',
        'InProgress' => 'جاهز للبيع',
        'Returned' => 'طلبات البيع',
        'ConvertedToSale' => 'جاهز للبيع',
        'Inspected' => 'تم الكشف',
        'Completed' => 'تم البيع',
        'Rejected' => 'مرفوض',
        _ => status ?? '',
      };
}

class SalesStaffInventoryFilter {
  static bool isHidden(String? productName) {
    final n = normalizeArabic(productName);
    if (n.isEmpty) return false;
    if (n.contains('تجهيز')) return true;
    final mobile = n.contains('موبايل') || n.contains('موبايلات');
    return mobile && n.contains('خارج');
  }

  static String normalizeArabic(String? value) {
    if (value == null || value.trim().isEmpty) return '';
    return value
        .replaceAll('أ', 'ا')
        .replaceAll('إ', 'ا')
        .replaceAll('آ', 'ا')
        .replaceAll('ة', 'ه')
        .replaceAll('ى', 'ي')
        .replaceAll('ـ', '')
        .trim();
  }
}

String salesApiMessage(int? statusCode, String fallback) {
  switch (statusCode) {
    case 401:
      return 'انتهت الجلسة. سجّل الدخول مرة أخرى.';
    case 403:
      return 'ليس لديك صلاحية لاستخدام هذه الخدمة.';
    case 503:
      return 'بيئة Sales Demo غير جاهزة حالياً.';
    case 500:
      final text = fallback.trim();
      if (text.isNotEmpty && text != 'حدث خطأ عام. حاول لاحقاً.') {
        return text;
      }
      return 'حدث خطأ عام. حاول لاحقاً.';
    default:
      return fallback;
  }
}
