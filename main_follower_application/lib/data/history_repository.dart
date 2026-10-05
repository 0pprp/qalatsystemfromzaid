import 'package:main_follower_application/core/network/api_client.dart';

class HistoryEntry {
  const HistoryEntry({
    required this.kind,
    required this.atUtc,
    required this.missionId,
    required this.cityValue,
    required this.score,
    this.committeeName,
    this.title,
    this.note,
    this.reviewerUserName,
  });

  final String kind;
  final String atUtc;
  final int missionId;
  final String cityValue;
  final int score;
  final String? committeeName;
  final String? title;
  final String? note;
  final String? reviewerUserName;

  bool get isSale => kind == 'sale';

  factory HistoryEntry.fromJson(Map<String, dynamic> json) {
    int asInt(dynamic v) {
      if (v is int) return v;
      return int.tryParse(v?.toString() ?? '') ?? 0;
    }

    return HistoryEntry(
      kind: (json['kind'] ?? '').toString(),
      atUtc: (json['atUtc'] ?? json['createdAtUtc'] ?? '').toString(),
      missionId: asInt(json['missionId']),
      cityValue: (json['cityValue'] ?? '').toString(),
      score: asInt(json['score']),
      committeeName: (json['committeeName'])?.toString(),
      title: (json['title'])?.toString(),
      note: (json['note'])?.toString(),
      reviewerUserName: (json['reviewerUserName'])?.toString(),
    );
  }
}

class HistoryRepository {
  static Future<List<HistoryEntry>> cityHistory(String cityValue) async {
    final rows = await ApiClient.getList(
      'main-follower/cities/${Uri.encodeComponent(cityValue)}/history',
    );
    return rows
        .whereType<Map>()
        .map((e) => HistoryEntry.fromJson(Map<String, dynamic>.from(e)))
        .toList();
  }
}
