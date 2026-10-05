import 'package:main_follower_application/core/network/api_client.dart';
import 'package:main_follower_application/domain/mission.dart';

class MissionRepository {
  static Future<List<Mission>> listMine() async {
    final rows = await ApiClient.getList('main-follower/missions');
    return rows
        .whereType<Map>()
        .map((e) => Mission.fromJson(Map<String, dynamic>.from(e)))
        .toList();
  }
}
