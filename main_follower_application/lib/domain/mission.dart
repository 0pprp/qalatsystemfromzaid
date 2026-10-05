/// Domain: mission assigned to a main-follower committee.
class Mission {
  const Mission({
    required this.id,
    required this.committeeId,
    required this.committeeName,
    required this.status,
    required this.cityValues,
    this.note,
    this.createdAtUtc,
  });

  final int id;
  final int committeeId;
  final String committeeName;
  final String status;
  final List<String> cityValues;
  final String? note;
  final DateTime? createdAtUtc;

  factory Mission.fromJson(Map<String, dynamic> json) {
    final cities = <String>[];
    final raw = json['cityValues'] ?? json['CityValues'];
    if (raw is List) {
      for (final c in raw) {
        final s = c?.toString().trim() ?? '';
        if (s.isNotEmpty) cities.add(s);
      }
    }
    return Mission(
      id: _asInt(json['id'] ?? json['Id']),
      committeeId: _asInt(json['committeeId'] ?? json['CommitteeId']),
      committeeName: (json['committeeName'] ?? json['CommitteeName'] ?? '').toString(),
      status: (json['status'] ?? json['Status'] ?? '').toString(),
      cityValues: cities,
      note: (json['note'] ?? json['Note'])?.toString(),
      createdAtUtc: DateTime.tryParse(
          (json['createdAtUtc'] ?? json['CreatedAtUtc'] ?? '').toString()),
    );
  }

  static int _asInt(dynamic v) {
    if (v is int) return v;
    return int.tryParse(v?.toString() ?? '') ?? 0;
  }
}
