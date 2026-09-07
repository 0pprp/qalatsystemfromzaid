import 'package:sales_employee_application/tracking/shift_start_debug.dart';

class WorkShift {
  WorkShift({
    required this.shiftId,
    required this.status,
    required this.startedAtUtc,
    required this.cutoffAtUtc,
    this.isNew = false,
    this.hasActiveShift = true,
    this.closeReason,
  });

  final int shiftId;
  final String status;
  final DateTime startedAtUtc;
  final DateTime cutoffAtUtc;
  final bool isNew;
  final bool hasActiveShift;
  final String? closeReason;

  bool get isActive =>
      status.toLowerCase() == 'active' && hasActiveShift && shiftId > 0;

  bool isPastCutoff([DateTime? utcNow]) =>
      (utcNow ?? DateTime.now().toUtc()).isAfter(cutoffAtUtc) ||
      (utcNow ?? DateTime.now().toUtc()).isAtSameMomentAs(cutoffAtUtc);

  Map<String, dynamic> toJson() => {
        'shiftId': shiftId,
        'status': status,
        'startedAtUtc': startedAtUtc.toIso8601String(),
        'cutoffAtUtc': cutoffAtUtc.toIso8601String(),
        'isNew': isNew,
        'hasActiveShift': hasActiveShift,
        'closeReason': closeReason,
      };

  factory WorkShift.fromJson(Map<String, dynamic> json) {
    DateTime parseUtc(String field, dynamic v) {
      var raw = '${v ?? ''}'.trim();
      ShiftStartDebug.log('parsing $field raw=$raw');
      if (raw.isEmpty) {
        ShiftStartDebug.log('parsing $field empty -> utcNow fallback');
        return DateTime.now().toUtc();
      }
      raw = raw.replaceFirst(' ', 'T');
      // Backend DateTime (SQL Unspecified) is often serialized without Z.
      // Treat timezone-less values as UTC, not device local (Iraq UTC+3).
      final hasZone = RegExp(r'(Z|[+-]\d{2}:?\d{2})$', caseSensitive: false).hasMatch(raw);
      if (!hasZone) {
        raw = '${raw}Z';
        ShiftStartDebug.log('parsing $field appended Z -> $raw');
      }
      final parsed = DateTime.tryParse(raw)?.toUtc();
      if (parsed == null) {
        ShiftStartDebug.log('parsing $field failed tryParse -> utcNow fallback');
        return DateTime.now().toUtc();
      }
      ShiftStartDebug.log('parsing $field result=${parsed.toIso8601String()}');
      return parsed;
    }

    final has = json['hasActiveShift'] ?? json['HasActiveShift'];
    return WorkShift(
      shiftId: int.tryParse('${json['shiftId'] ?? json['ShiftId'] ?? json['id'] ?? json['Id'] ?? 0}') ?? 0,
      status: '${json['status'] ?? json['Status'] ?? 'Active'}',
      startedAtUtc: parseUtc(
        'startedAt',
        json['startedAtUtc'] ??
            json['StartedAtUtc'] ??
            json['startedAt'] ??
            json['StartedAt'],
      ),
      cutoffAtUtc: parseUtc(
        'cutoffAt',
        json['cutoffAtUtc'] ??
            json['CutoffAtUtc'] ??
            json['cutoffAt'] ??
            json['CutoffAt'],
      ),
      isNew: json['isNew'] == true || json['IsNew'] == true,
      hasActiveShift: has == null ? true : has == true,
      closeReason: json['closeReason']?.toString() ?? json['CloseReason']?.toString(),
    );
  }
}

class LocalLocationPoint {
  LocalLocationPoint({
    this.id,
    required this.shiftId,
    required this.latitude,
    required this.longitude,
    this.accuracy,
    this.speed,
    this.heading,
    required this.capturedAtUtc,
    required this.deviceSequence,
    this.syncStatus = 'Pending',
    this.retryCount = 0,
  });

  final int? id;
  final int shiftId;
  final double latitude;
  final double longitude;
  final double? accuracy;
  final double? speed;
  final double? heading;
  final DateTime capturedAtUtc;
  final int deviceSequence;
  String syncStatus;
  int retryCount;

  Map<String, dynamic> toBatchJson() => {
        'latitude': latitude,
        'longitude': longitude,
        'accuracy': accuracy,
        'speed': speed,
        'heading': heading,
        'capturedAtUtc': capturedAtUtc.toUtc().toIso8601String(),
        'deviceSequence': deviceSequence,
        'isOfficial': true,
      };
}

class LocalTrackingEvent {
  LocalTrackingEvent({required this.id, this.shiftId, required this.eventType});
  final int id;
  final int? shiftId;
  final String eventType;
}

class LocationBatchResult {
  LocationBatchResult({this.accepted = 0, this.duplicates = 0, this.rejected = 0});
  final int accepted;
  final int duplicates;
  final int rejected;

  bool get allRejected => accepted == 0 && duplicates == 0 && rejected > 0;

  factory LocationBatchResult.fromJson(Map<String, dynamic> json) => LocationBatchResult(
        accepted: int.tryParse('${json['accepted'] ?? json['Accepted'] ?? 0}') ?? 0,
        duplicates: int.tryParse('${json['duplicates'] ?? json['Duplicates'] ?? 0}') ?? 0,
        rejected: int.tryParse('${json['rejected'] ?? json['Rejected'] ?? 0}') ?? 0,
      );
}

class TrackingShiftPolicy {
  static WorkShift? parseLocal(Map<String, dynamic>? json) {
    if (json == null || json.isEmpty) return null;
    try {
      final shift = WorkShift.fromJson(json);
      if (shift.shiftId <= 0) return null;
      return shift;
    } catch (_) {
      return null;
    }
  }

  /// Native FGS must keep running if the remote lookup failed (offline / 5xx).
  /// Stop only after the user ended the shift, or the server says there is no active shift.
  static bool shouldStopNative({
    required bool gpsStoppedByUser,
    required bool remoteLookupFailed,
    WorkShift? remoteShift,
  }) {
    if (gpsStoppedByUser) return true;
    if (remoteLookupFailed) return false;
    return remoteShift == null || !remoteShift.isActive;
  }
}
