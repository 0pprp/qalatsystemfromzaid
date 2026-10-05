import 'package:main_follower_application/core/network/api_client.dart';
import 'package:main_follower_application/domain/staff_member.dart';

class StaffRepository {
  static String _cityPath(int missionId, String cityValue, String leaf) =>
      'main-follower/missions/$missionId/cities/${Uri.encodeComponent(cityValue)}/$leaf';

  static Future<List<StaffMember>> listDelegates({
    required int missionId,
    required String cityValue,
  }) async {
    final rows = await ApiClient.getList(_cityPath(missionId, cityValue, 'delegates'));
    return rows
        .whereType<Map>()
        .map((e) => StaffMember.delegate(Map<String, dynamic>.from(e)))
        .where((m) => m.id > 0)
        .toList();
  }

  static Future<List<StaffMember>> listFollowers({
    required int missionId,
    required String cityValue,
  }) async {
    final rows = await ApiClient.getList(_cityPath(missionId, cityValue, 'followers'));
    return rows
        .whereType<Map>()
        .map((e) => StaffMember.follower(Map<String, dynamic>.from(e)))
        .where((m) => m.id > 0)
        .toList();
  }

  static Future<List<StaffMember>> listEmployees({
    required int missionId,
    required String cityValue,
  }) async {
    final rows =
        await ApiClient.getList(_cityPath(missionId, cityValue, 'sales-employees'));
    return rows
        .whereType<Map>()
        .map((e) => StaffMember.employee(Map<String, dynamic>.from(e)))
        .where((m) => m.id > 0)
        .toList();
  }

  static Future<StaffReview> submitReview({
    required int missionId,
    required String cityValue,
    required String targetType,
    required int targetId,
    required String targetName,
    int? listId,
    required int score,
    String? note,
  }) async {
    final json = await ApiClient.postJson('main-follower/staff-reviews', {
      'missionId': missionId,
      'cityValue': cityValue,
      'targetType': targetType,
      'targetId': targetId,
      'targetName': targetName,
      'listId': listId,
      'score': score,
      'note': note,
    });
    return StaffReview.fromJson(json);
  }

  static Future<List<StaffReview>> listMissionReviews(int missionId) async {
    final rows = await ApiClient.getList('main-follower/missions/$missionId/staff-reviews');
    return rows
        .whereType<Map>()
        .map((e) => StaffReview.fromJson(Map<String, dynamic>.from(e)))
        .toList();
  }

  static Future<List<StaffReview>> listTargetNotes({
    required String targetType,
    required int targetId,
    String? cityValue,
  }) async {
    final q = (cityValue != null && cityValue.isNotEmpty)
        ? '?cityValue=${Uri.encodeComponent(cityValue)}'
        : '';
    final rows = await ApiClient.getList(
      'main-follower/staff/$targetType/$targetId/notes$q',
    );
    return rows
        .whereType<Map>()
        .map((e) => StaffReview.fromJson(Map<String, dynamic>.from(e)))
        .toList();
  }
}
