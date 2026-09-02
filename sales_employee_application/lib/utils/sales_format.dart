import 'package:intl/intl.dart';

class MoneyFormat {
  static final NumberFormat _iqd = NumberFormat.decimalPattern('en');

  static String iqd(num value) => '${_iqd.format(value.round())} د.ع';
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
        return 'مكتمل';
      case 'DocumentsReady':
        return 'مكتمل';
      case 'DocumentsPending':
        return 'مكتمل — المستندات قيد التوليد';
      default:
        return 'معلق';
    }
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
      return 'حدث خطأ عام. حاول لاحقاً.';
    default:
      return fallback;
  }
}
