class WorkShift {
  const WorkShift({
    required this.id,
    required this.userName,
    required this.isActive,
    this.startedAtUtc,
    this.endedAtUtc,
  });

  final int id;
  final String userName;
  final bool isActive;
  final DateTime? startedAtUtc;
  final DateTime? endedAtUtc;

  factory WorkShift.fromJson(Map<String, dynamic> json) {
    int asInt(dynamic v) {
      if (v is int) return v;
      return int.tryParse(v?.toString() ?? '') ?? 0;
    }

    DateTime? asDate(dynamic v) {
      if (v == null) return null;
      return DateTime.tryParse(v.toString())?.toUtc();
    }

    return WorkShift(
      id: asInt(json['id'] ?? json['Id']),
      userName: (json['userName'] ?? json['UserName'] ?? '').toString(),
      isActive: json['isActive'] == true ||
          json['IsActive'] == true ||
          (json['endedAtUtc'] == null && json['EndedAtUtc'] == null),
      startedAtUtc: asDate(json['startedAtUtc'] ?? json['StartedAtUtc']),
      endedAtUtc: asDate(json['endedAtUtc'] ?? json['EndedAtUtc']),
    );
  }
}
