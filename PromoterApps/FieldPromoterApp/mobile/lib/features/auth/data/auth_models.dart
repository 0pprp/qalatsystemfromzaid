import '../../../core/network/json.dart';

class Me {
  const Me({
    required this.id,
    required this.phone,
    required this.firstName,
    required this.fatherName,
    required this.grandfatherName,
    this.surname,
    required this.fullName,
    this.createdAt,
    this.username,
  });

  final int id;
  final String phone;
  final String firstName;
  final String fatherName;
  final String grandfatherName;
  final String? surname;
  final String fullName;
  final DateTime? createdAt;
  final String? username;

  String get displayName => fullName.isNotEmpty
      ? fullName
      : [firstName, fatherName, grandfatherName, surname ?? ''].where((s) => s.isNotEmpty).join(' ');

  factory Me.fromJson(Json j) => Me(
        id: asInt(j['id']),
        phone: asString(j['phone']),
        firstName: asString(j['firstName']),
        fatherName: asString(j['fatherName']),
        grandfatherName: asString(j['grandfatherName']),
        surname: asStringOrNull(j['surname']),
        fullName: asString(j['fullName']),
        createdAt: asDate(j['createdAt']),
        username: asStringOrNull(j['username']),
      );

  Json toJson() => {
        'id': id,
        'phone': phone,
        'firstName': firstName,
        'fatherName': fatherName,
        'grandfatherName': grandfatherName,
        'surname': surname,
        'fullName': fullName,
        'createdAt': createdAt?.toIso8601String(),
        'username': username,
      };
}
