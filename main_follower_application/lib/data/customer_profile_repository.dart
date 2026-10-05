import 'package:main_follower_application/core/network/api_client.dart';
import 'package:main_follower_application/domain/customer_profile.dart';

class CustomerProfileRepository {
  static String _base({
    required int missionId,
    required String cityValue,
  }) =>
      'main-follower/missions/$missionId/cities/${Uri.encodeComponent(cityValue)}';

  static Future<CustomerProfile> load({
    required int missionId,
    required String cityValue,
    int? customerId,
    String? name,
    String? phone,
  }) async {
    final Map<String, dynamic> json;
    if (customerId != null && customerId > 0) {
      json = await ApiClient.getJson(
        '${_base(missionId: missionId, cityValue: cityValue)}/customers/$customerId/profile',
      );
    } else {
      final q = <String, String>{};
      if (name != null && name.trim().isNotEmpty) q['name'] = name.trim();
      if (phone != null && phone.trim().isNotEmpty) q['phone'] = phone.trim();
      final query = q.entries
          .map((e) =>
              '${Uri.encodeQueryComponent(e.key)}=${Uri.encodeQueryComponent(e.value)}')
          .join('&');
      json = await ApiClient.getJson(
        '${_base(missionId: missionId, cityValue: cityValue)}/customers/profile'
        '${query.isEmpty ? '' : '?$query'}',
      );
    }
    return CustomerProfile.fromJson(json);
  }

  static Future<(List<int> bytes, String contentType)> documentBytes({
    required int missionId,
    required String cityValue,
    required int documentId,
  }) {
    return ApiClient.getBytes(
      '${_base(missionId: missionId, cityValue: cityValue)}/customer-documents/$documentId/file',
    );
  }
}
