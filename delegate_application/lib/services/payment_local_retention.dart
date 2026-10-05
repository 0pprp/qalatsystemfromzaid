import 'package:delegate_application/services/payment_sync_status.dart';
import 'package:delegate_application/utils/iraq_date.dart';

/// Pure helpers for durable local [CustomerPayment] retention.
///
/// Never group-delete by Amount — that wiped same-day installments when a prior
/// day had the same amount for the same customer.
class PaymentLocalRetention {
  /// Row `id`s safe to purge at 03:00 business-day rollover:
  /// only [PaymentSyncStatus.synced] rows outside the current business day.
  /// Pending / failed / syncing rows are never returned.
  static List<int> syncedOutsideBusinessDayRowIdsToDelete(
    List<Map<String, dynamic>> rows, {
    DateTime? utcNow,
  }) {
    final out = <int>[];
    for (final row in rows) {
      final status = row['SyncStatus']?.toString();
      if (status != PaymentSyncStatus.synced) continue;
      final created = row['CreatedAtUtc']?.toString();
      if (IraqDate.isCreatedOnIraqDay(created, utcNow)) continue;
      final id = int.tryParse(row['id']?.toString() ?? '');
      if (id != null) out.add(id);
    }
    return out;
  }

  /// Exact [ClientPaymentId] duplicates only — keep the smallest `id`.
  /// Rows with null/empty ClientPaymentId are left untouched.
  static List<int> duplicateClientPaymentIdRowIdsToDelete(
    List<Map<String, dynamic>> rows,
  ) {
    final byKey = <String, List<int>>{};
    for (final row in rows) {
      final key = row['ClientPaymentId']?.toString().trim() ?? '';
      if (key.isEmpty) continue;
      final id = int.tryParse(row['id']?.toString() ?? '');
      if (id == null) continue;
      byKey.putIfAbsent(key, () => []).add(id);
    }
    final toDelete = <int>[];
    for (final ids in byKey.values) {
      if (ids.length < 2) continue;
      ids.sort();
      toDelete.addAll(ids.skip(1));
    }
    return toDelete;
  }
}
