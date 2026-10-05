import 'dart:convert';

import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';

import 'package:delegate_application/ui/shop_evaluation_dialog.dart';

class FirstCollectionEvalService {
  /// Posts shop evaluation for a new-sale first تسديد.
  /// Returns null on success; Arabic error message on failure.
  static Future<String?> submit({
    required int customerId,
    required int listId,
    required ShopEvaluationResult eval,
    double? saleAmount,
  }) async {
    if (customerId <= 0) return 'معرف الزبون مفقود';
    final prefs = await SharedPreferences.getInstance();
    final asyncId = prefs.getString('AsyncId') ?? '';
    var link = prefs.getString('LinkDelegate') ?? '';
    if (asyncId.isEmpty || link.isEmpty) {
      return 'جلسة المندوب غير جاهزة — أعد تسجيل الدخول';
    }
    if (!link.endsWith('/')) link = '$link/';

    final uri = Uri.parse('${link}Delegates/FirstCollectionEvaluation');
    try {
      final response = await http
          .post(
            uri,
            headers: {'Content-Type': 'application/json'},
            body: json.encode({
              'asyncId': asyncId,
              'customerId': customerId,
              'listId': listId,
              'rating': eval.rating,
              'note': eval.note,
              'shopStockEstimatedValue': eval.shopStockEstimatedValue,
              'estimatedDailyRevenue': eval.estimatedDailyRevenue,
              if (saleAmount != null && saleAmount > 0) 'saleAmount': saleAmount,
            }),
          )
          .timeout(const Duration(seconds: 30));

      if (response.statusCode == 200) return null;

      final msg = _messageFromBody(response.body);
      return msg ?? 'تعذّر حفظ تقييم المحل (${response.statusCode})';
    } catch (_) {
      return 'تعذّر الاتصال لحفظ تقييم المحل';
    }
  }

  static String? _messageFromBody(String body) {
    try {
      final decoded = json.decode(body);
      if (decoded is Map) {
        final msg =
            decoded['message'] ?? decoded['Message'] ?? decoded['title'];
        if (msg != null && '$msg'.trim().isNotEmpty) return '$msg'.trim();
      }
    } catch (_) {}
    return null;
  }
}
