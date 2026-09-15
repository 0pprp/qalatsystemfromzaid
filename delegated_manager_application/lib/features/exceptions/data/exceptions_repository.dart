import 'package:delegated_manager_application/core/models/paged_result.dart';
import 'package:delegated_manager_application/core/network/api_client.dart';
import 'package:delegated_manager_application/core/utils/json_read.dart';
import 'package:delegated_manager_application/features/exceptions/domain/sales_exception.dart';

class ExceptionsRepository {
  static const String _base = 'delegated-manager/exceptions';

  static Future<PagedResult<ExceptionSummary>> list({
    String? status,
    String? cityValue,
    int page = 1,
    int pageSize = 20,
  }) async {
    final json = await ApiClient.get(_base, {
      'page': page.toString(),
      'pageSize': pageSize.toString(),
      if ((status ?? '').isNotEmpty) 'status': status!,
      if ((cityValue ?? '').isNotEmpty) 'cityValue': cityValue!,
    });
    return PagedResult.fromJson(json, ExceptionSummary.fromJson);
  }

  static Future<ExceptionDetail> detail(String id) async {
    final json = await ApiClient.get('$_base/$id');
    return ExceptionDetail.fromJson(JsonRead.map(json));
  }

  /// `decision` is `Approved` or `Rejected`; the gateway rejects any other
  /// value and returns 409 when the request was already decided.
  static Future<ExceptionRequest> decide({
    required String id,
    required String decision,
    String? note,
  }) async {
    final json = await ApiClient.put('$_base/$id/decision', body: {
      'decision': decision,
      if ((note ?? '').trim().isNotEmpty) 'note': note!.trim(),
    });
    return ExceptionRequest.fromJson(JsonRead.map(json));
  }
}
