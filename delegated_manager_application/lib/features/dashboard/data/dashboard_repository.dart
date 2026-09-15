import 'package:delegated_manager_application/core/network/api_client.dart';
import 'package:delegated_manager_application/core/theme/app_theme.dart';
import 'package:delegated_manager_application/core/utils/json_read.dart';
import 'package:delegated_manager_application/features/complaints/data/complaints_repository.dart';
import 'package:delegated_manager_application/features/dashboard/domain/dashboard_summary.dart';
import 'package:delegated_manager_application/features/exceptions/data/exceptions_repository.dart';

class DashboardRepository {
  /// Uses `delegated-manager/dashboard`; if that endpoint is missing on an
  /// older gateway it composes the same numbers from the unread count and the
  /// pending exception page.
  static Future<DashboardSummary> load() async {
    try {
      final json = await ApiClient.get('delegated-manager/dashboard');
      return DashboardSummary.fromJson(JsonRead.map(json));
    } on ApiException catch (e) {
      if (e.statusCode != 404) rethrow;
      return _compose();
    }
  }

  static Future<DashboardSummary> _compose() async {
    final unread = await ComplaintsRepository.unreadCount();
    final pending = await ExceptionsRepository.list(
      status: ExceptionStatuses.pending,
      pageSize: 1,
    );
    return DashboardSummary.compose(
      unreadComplaints: unread,
      pendingExceptions: pending.totalCount,
    );
  }
}
