import 'package:delegated_manager_application/core/models/paged_result.dart';
import 'package:delegated_manager_application/core/network/api_client.dart';
import 'package:delegated_manager_application/core/utils/json_read.dart';
import 'package:delegated_manager_application/features/complaints/domain/complaint.dart';

class ComplaintsRepository {
  static const String _base = 'delegated-manager/complaints';

  static Future<PagedResult<ComplaintSummary>> inbox({
    int page = 1,
    int pageSize = 20,
    bool unreadOnly = false,
    String? cityValue,
    String? sourceApp,
    String? search,
  }) async {
    final json = await ApiClient.get(_base, {
      'page': page.toString(),
      'pageSize': pageSize.toString(),
      if (unreadOnly) 'unreadOnly': 'true',
      if ((cityValue ?? '').isNotEmpty) 'cityValue': cityValue!,
      if ((sourceApp ?? '').isNotEmpty) 'sourceApp': sourceApp!,
      if ((search ?? '').trim().isNotEmpty) 'search': search!.trim(),
    });
    return PagedResult.fromJson(json, ComplaintSummary.fromJson);
  }

  static Future<int> unreadCount() async {
    final json = await ApiClient.get('$_base/unread-count');
    return JsonRead.number(JsonRead.map(json)['unread']);
  }

  static Future<ComplaintDetail> detail(String id) async {
    final json = await ApiClient.get('$_base/$id');
    return ComplaintDetail.fromJson(JsonRead.map(json));
  }

  static Future<ComplaintDetail> markRead(String id) async {
    final json = await ApiClient.put('$_base/$id/read');
    return ComplaintDetail.fromJson(JsonRead.map(json));
  }

  static Future<int> markAllRead() async {
    final json = await ApiClient.put('$_base/read-all');
    return JsonRead.number(JsonRead.map(json)['updated']);
  }
}
