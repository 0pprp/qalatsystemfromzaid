import 'package:dio/dio.dart';

/// Unified error type surfaced to the UI. [message] is always a user-facing Arabic string.
class ApiException implements Exception {
  ApiException({
    required this.message,
    this.code = 'unknown',
    this.statusCode,
    this.fields = const {},
    this.data = const {},
  });

  final String message;
  final String code;
  final int? statusCode;

  /// Field-level validation errors (`code == "validation"`), keyed by camelCase field name.
  final Map<String, String> fields;

  /// The full error body, for codes that carry extra data
  /// (`retryAfterSeconds`, `attemptsLeft`, `orderNumber`, …).
  final Map<String, dynamic> data;

  bool get isNetwork => code == 'network' || code == 'timeout';
  bool get isUnauthorized => statusCode == 401;

  int? intData(String key) {
    final v = data[key];
    if (v is int) return v;
    if (v is num) return v.toInt();
    if (v is String) return int.tryParse(v);
    return null;
  }

  String? stringData(String key) => data[key]?.toString();

  @override
  String toString() => 'ApiException($code, $statusCode): $message';

  /// Converts anything thrown by the network layer into an [ApiException].
  static ApiException from(Object error) {
    if (error is ApiException) return error;
    if (error is DioException) {
      final inner = error.error;
      if (inner is ApiException) return inner;
      return _fromDio(error);
    }
    return ApiException(message: ErrorMessages.unexpected, code: 'unknown');
  }

  static ApiException _fromDio(DioException e) {
    switch (e.type) {
      case DioExceptionType.connectionTimeout:
      case DioExceptionType.sendTimeout:
      case DioExceptionType.receiveTimeout:
        return ApiException(message: ErrorMessages.timeout, code: 'timeout');
      case DioExceptionType.connectionError:
        return ApiException(message: ErrorMessages.network, code: 'network');
      case DioExceptionType.cancel:
        return ApiException(message: ErrorMessages.cancelled, code: 'cancelled');
      case DioExceptionType.badCertificate:
        return ApiException(message: ErrorMessages.network, code: 'network');
      default:
        break;
    }
    final response = e.response;
    if (response == null) {
      return ApiException(message: ErrorMessages.network, code: 'network');
    }
    final status = response.statusCode;
    final body = response.data;
    if (body is Map) {
      final map = Map<String, dynamic>.from(body);
      final code = map['code']?.toString() ?? _codeForStatus(status);
      final serverMessage = map['error']?.toString();
      final fields = <String, String>{};
      final rawFields = map['fields'];
      if (rawFields is Map) {
        rawFields.forEach((k, v) => fields[k.toString()] = v.toString());
      }
      return ApiException(
        message: (serverMessage != null && serverMessage.trim().isNotEmpty)
            ? serverMessage
            : ErrorMessages.forCode(code, status),
        code: code,
        statusCode: status,
        fields: fields,
        data: map,
      );
    }
    final code = _codeForStatus(status);
    return ApiException(
      message: ErrorMessages.forCode(code, status),
      code: code,
      statusCode: status,
    );
  }

  static String _codeForStatus(int? status) {
    switch (status) {
      case 400:
        return 'validation';
      case 401:
        return 'unauthorized';
      case 403:
        return 'forbidden';
      case 404:
        return 'not_found';
      case 409:
        return 'conflict';
      case 429:
        return 'rate_limited';
      default:
        return (status ?? 0) >= 500 ? 'server' : 'unknown';
    }
  }
}

/// Arabic fallback messages when the server did not send its own `error` text.
abstract final class ErrorMessages {
  static const network = 'تعذّر الاتصال بالخادم. تأكد من اتصالك بالإنترنت ثم حاول مجدداً.';
  static const timeout = 'انتهت مهلة الاتصال. حاول مرة أخرى.';
  static const cancelled = 'تم إلغاء الطلب.';
  static const unexpected = 'حدث خطأ غير متوقع. حاول مرة أخرى.';
  static const server = 'الخدمة غير متاحة حالياً. حاول بعد قليل.';

  static const Map<String, String> _byCode = {
    'invalid_phone': 'رقم الهاتف غير صحيح. أدخل رقماً عراقياً يبدأ بـ 07.',
    'otp_rate_limited': 'طلبت رموزاً كثيرة. انتظر قليلاً ثم حاول مجدداً.',
    'account_suspended': 'تم إيقاف هذا الحساب. تواصل مع خدمة الزبائن.',
    'otp_invalid': 'رمز التحقق غير صحيح.',
    'otp_expired': 'انتهت صلاحية رمز التحقق. اطلب رمزاً جديداً.',
    'otp_too_many_attempts': 'محاولات كثيرة خاطئة. اطلب رمزاً جديداً.',
    'invalid_referral_code': 'رمز الإحالة غير صحيح. امسحه أو صحّحه ثم حاول مجدداً.',
    'duplicate_open_order': 'لديك طلب مفتوح لنفس المنتج بنفس رقم الهاتف.',
    'phone_already_registered': 'هذا الرقم مسجّل مسبقاً في التطبيق.',
    'validation': 'يرجى التحقق من البيانات المدخلة.',
    'unauthorized': 'انتهت الجلسة. سجّل الدخول من جديد.',
    'forbidden': 'لا تملك صلاحية لهذا الإجراء.',
    'not_found': 'العنصر المطلوب غير موجود.',
    'conflict': 'تعذّر تنفيذ العملية بسبب تعارض في البيانات.',
    'rate_limited': 'طلبات كثيرة. انتظر قليلاً ثم حاول مجدداً.',
    'server': server,
  };

  static String forCode(String code, [int? status]) {
    final known = _byCode[code];
    if (known != null) return known;
    if ((status ?? 0) >= 500) return server;
    return unexpected;
  }
}
