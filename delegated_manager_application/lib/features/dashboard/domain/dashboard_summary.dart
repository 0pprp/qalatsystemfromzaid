import 'package:delegated_manager_application/core/utils/json_read.dart';

/// Response of `GET delegated-manager/dashboard`.
class DashboardSummary {
  const DashboardSummary({
    required this.unreadComplaints,
    required this.pendingExceptions,
    required this.approvedExceptions,
    required this.rejectedExceptions,
    required this.cancelledExceptions,
    required this.totalExceptions,
  });

  final int unreadComplaints;
  final int pendingExceptions;
  final int approvedExceptions;
  final int rejectedExceptions;
  final int cancelledExceptions;
  final int totalExceptions;

  bool get hasWork => unreadComplaints > 0 || pendingExceptions > 0;

  static const DashboardSummary empty = DashboardSummary(
    unreadComplaints: 0,
    pendingExceptions: 0,
    approvedExceptions: 0,
    rejectedExceptions: 0,
    cancelledExceptions: 0,
    totalExceptions: 0,
  );

  factory DashboardSummary.fromJson(Map<String, dynamic> json) {
    final exceptions = JsonRead.map(json['exceptions']);
    return DashboardSummary(
      unreadComplaints: JsonRead.number(json['unreadComplaints']),
      pendingExceptions: JsonRead.number(exceptions['pending']),
      approvedExceptions: JsonRead.number(exceptions['approved']),
      rejectedExceptions: JsonRead.number(exceptions['rejected']),
      cancelledExceptions: JsonRead.number(exceptions['cancelled']),
      totalExceptions: JsonRead.number(exceptions['total']),
    );
  }

  /// Fallback when the dashboard endpoint is unavailable: compose from the
  /// unread-count endpoint plus a pending exception page total.
  factory DashboardSummary.compose({
    required int unreadComplaints,
    required int pendingExceptions,
  }) =>
      DashboardSummary(
        unreadComplaints: unreadComplaints,
        pendingExceptions: pendingExceptions,
        approvedExceptions: 0,
        rejectedExceptions: 0,
        cancelledExceptions: 0,
        totalExceptions: pendingExceptions,
      );
}
