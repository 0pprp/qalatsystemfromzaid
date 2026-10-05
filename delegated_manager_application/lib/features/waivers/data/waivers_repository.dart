import 'package:delegated_manager_application/core/models/paged_result.dart';
import 'package:delegated_manager_application/core/network/api_client.dart';
import 'package:delegated_manager_application/core/utils/json_read.dart';
import 'package:delegated_manager_application/features/waivers/domain/customer_waiver.dart';

class WaiversRepository {
  static const String _base = 'delegated-manager/waivers';

  static Future<PagedResult<WaiverSummary>> list({
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
    return PagedResult.fromJson(json, WaiverSummary.fromJson);
  }

  static Future<WaiverDetail> detail(String id) async {
    final json = await ApiClient.get('$_base/$id');
    return WaiverDetail.fromJson(JsonRead.map(json));
  }

  static Future<WaiverRequest> decide({
    required String id,
    required String decision,
    String? note,
    double? approvedAmount,
  }) async {
    final json = await ApiClient.put('$_base/$id/decision', body: {
      'decision': decision,
      if ((note ?? '').trim().isNotEmpty) 'note': note!.trim(),
      if (approvedAmount != null) 'approvedAmount': approvedAmount,
    });
    return WaiverRequest.fromJson(JsonRead.map(json));
  }
}
