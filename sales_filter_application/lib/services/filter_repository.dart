import 'package:sales_filter_application/models/filter_models.dart';
import 'package:sales_filter_application/services/api_client.dart';

class FilterRepository {
  Future<List<FilterCity>> myCities() async {
    final raw = await ApiClient.get('sales-filter/me/cities');
    final list = raw is List ? raw : const [];
    return list.whereType<Map>().map((e) => FilterCity.fromJson(Map<String, dynamic>.from(e))).toList();
  }

  Future<List<FilterRequest>> list({
    required String status,
    String? city,
    int page = 1,
  }) async {
    final query = <String, String>{
      'status': status,
      'page': '$page',
      'pageSize': '30',
    };
    if (city != null && city.isNotEmpty) query['city'] = city;
    final raw = await ApiClient.get('sales-filter/requests', query);
    final items = raw is Map ? (raw['items'] ?? raw['Items']) : null;
    final list = items is List ? items : const [];
    return list.whereType<Map>().map((e) => FilterRequest.fromJson(Map<String, dynamic>.from(e))).toList();
  }

  Future<FilterRequest> get(int id) async {
    final raw = await ApiClient.get('sales-filter/requests/$id');
    return FilterRequest.fromJson(Map<String, dynamic>.from(raw as Map));
  }

  Future<FilterRequest> hold(int id, {String? note}) async {
    final raw = await ApiClient.post('sales-filter/requests/$id/hold', body: {'note': note});
    return FilterRequest.fromJson(Map<String, dynamic>.from(raw as Map));
  }

  Future<FilterRequest> ready(int id, {String? note}) async {
    final raw = await ApiClient.post('sales-filter/requests/$id/ready', body: {'note': note});
    return FilterRequest.fromJson(Map<String, dynamic>.from(raw as Map));
  }

  Future<FilterRequest> reject(int id, {required String reason, String? note}) async {
    final raw = await ApiClient.post('sales-filter/requests/$id/reject', body: {
      'reason': reason,
      'note': note,
    });
    return FilterRequest.fromJson(Map<String, dynamic>.from(raw as Map));
  }
}
