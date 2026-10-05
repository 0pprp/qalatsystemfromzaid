class StaffMember {
  const StaffMember({
    required this.id,
    required this.name,
    required this.targetType,
    this.subtitle,
    this.listId,
  });

  final int id;
  final String name;
  /// Delegate | SubFollower | SalesEmployee
  final String targetType;
  final String? subtitle;
  final int? listId;

  factory StaffMember.delegate(Map<String, dynamic> json) {
    final id = _asInt(json['delegateId'] ?? json['DelegateID'] ?? json['id']);
    final name = (json['delegateName'] ?? json['DelegateName'] ?? 'مندوب').toString();
    return StaffMember(
      id: id,
      name: name,
      targetType: 'Delegate',
      subtitle: (json['phone'] ?? json['PhoneNumber'])?.toString(),
      listId: id > 0 ? id : null,
    );
  }

  factory StaffMember.follower(Map<String, dynamic> json) {
    final id = _asInt(json['followerId'] ?? json['FollowerId'] ?? json['userId']);
    final name = (json['followerName'] ?? json['FollowerName'] ?? 'متابع').toString();
    final city = (json['cityName'] ?? json['CityName'])?.toString();
    return StaffMember(
      id: id,
      name: name,
      targetType: 'SubFollower',
      subtitle: city,
    );
  }

  factory StaffMember.employee(Map<String, dynamic> json) {
    final id = _asInt(json['employeeId'] ?? json['EmployeeId'] ?? json['id']);
    final name = (json['employeeName'] ?? json['EmployeeName'] ?? 'موظف').toString();
    final shift = (json['shiftStatus'] ?? json['ShiftStatus'])?.toString();
    return StaffMember(
      id: id,
      name: name,
      targetType: 'SalesEmployee',
      subtitle: shift,
    );
  }

  static int _asInt(dynamic v) {
    if (v is int) return v;
    return int.tryParse(v?.toString() ?? '') ?? 0;
  }
}

class StaffReview {
  const StaffReview({
    required this.id,
    required this.missionId,
    required this.cityValue,
    required this.targetType,
    required this.targetId,
    required this.score,
    this.targetName,
    this.note,
    this.feedNote,
  });

  final int id;
  final int missionId;
  final String cityValue;
  final String targetType;
  final int targetId;
  final int score;
  final String? targetName;
  final String? note;
  final String? feedNote;

  factory StaffReview.fromJson(Map<String, dynamic> json) {
    int asInt(dynamic v) {
      if (v is int) return v;
      return int.tryParse(v?.toString() ?? '') ?? 0;
    }

    return StaffReview(
      id: asInt(json['id'] ?? json['Id']),
      missionId: asInt(json['missionId'] ?? json['MissionId']),
      cityValue: (json['cityValue'] ?? json['CityValue'] ?? '').toString(),
      targetType: (json['targetType'] ?? json['TargetType'] ?? '').toString(),
      targetId: asInt(json['targetId'] ?? json['TargetId']),
      score: asInt(json['score'] ?? json['Score']),
      targetName: (json['targetName'] ?? json['TargetName'])?.toString(),
      note: (json['note'] ?? json['Note'] ?? json['userNote'])?.toString(),
      feedNote: (json['feedNote'] ?? json['FeedNote'])?.toString(),
    );
  }

  String get typeLabel => switch (targetType) {
        'Delegate' => 'مندوب',
        'SubFollower' => 'متابع فرعي',
        'SalesEmployee' => 'موظف مبيعات',
        _ => targetType,
      };
}
