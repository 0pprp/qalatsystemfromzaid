import 'package:sales_filter_application/models/filter_models.dart';
import 'package:sales_filter_application/services/api_client.dart';

class FilterRepository {
  Future<List<FilterCity>> myCities() async {
    final raw = await ApiClient.get('sales-filter/me/cities');
    final list = raw is List ? raw : const [];
    return list.whereType<Map>().map((e) => FilterCity.fromJson(Map<String, dynamic>.from(e))).toList();
  }

  Future<Map<String, int>> counts({required String cityValue}) async {
    final raw = await ApiClient.get('sales-filter/counts', {'cityValue': cityValue});
    if (raw is! Map) return {};
    final map = <String, int>{};
    raw.forEach((k, v) {
      map['$k'] = int.tryParse('$v') ?? 0;
    });
    return map;
  }

  Future<List<FilterRequest>> list({
    required String status,
    String? cityValue,
    int page = 1,
  }) async {
    final query = <String, String>{
      'status': status,
      'page': '$page',
      'pageSize': '30',
    };
    if (cityValue != null && cityValue.isNotEmpty) {
      query['cityValue'] = cityValue;
    }
    final raw = await ApiClient.get('sales-filter/requests', query);
    final items = raw is Map ? (raw['items'] ?? raw['Items']) : null;
    final list = items is List ? items : const [];
    return list.whereType<Map>().map((e) => FilterRequest.fromJson(Map<String, dynamic>.from(e))).toList();
  }

  Future<FilterRequest> get(String cityValue, int id) async {
    final raw = await ApiClient.get('sales-filter/requests/$cityValue/$id');
    return FilterRequest.fromJson(Map<String, dynamic>.from(raw as Map));
  }

  Future<FilterRequest> hold(String cityValue, int id, {required String note}) async {
    final raw = await ApiClient.post('sales-filter/requests/$cityValue/$id/hold', body: {'note': note});
    return FilterRequest.fromJson(Map<String, dynamic>.from(raw as Map));
  }

  Future<FilterRequest> ready(String cityValue, int id, {String? note}) async {
    final raw = await ApiClient.post('sales-filter/requests/$cityValue/$id/ready', body: {'note': note});
    return FilterRequest.fromJson(Map<String, dynamic>.from(raw as Map));
  }

  Future<FilterRequest> reject(String cityValue, int id, {required String reason, String? note}) async {
    final raw = await ApiClient.post('sales-filter/requests/$cityValue/$id/reject', body: {
      'reason': reason,
      'note': note,
    });
    return FilterRequest.fromJson(Map<String, dynamic>.from(raw as Map));
  }
}
