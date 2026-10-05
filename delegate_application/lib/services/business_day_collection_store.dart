import 'package:delegate_application/services/DatabaseHelper.dart';
import 'package:delegate_application/utils/iraq_date.dart';
import 'package:sqflite/sqflite.dart';

/// Durable "collected on this business day" markers that survive Synced-row deletes.
///
/// Prevents re-collection after a successful upload clears [CustomerPayment],
/// and keeps «تسديدات اليوم» totals accurate until the next 03:00 rollover.
class BusinessDayCollectionStore {
  static Future<void> record({
    required int customerId,
    required int delegateId,
    required double amount,
    required String clientPaymentId,
    required String createdAtUtc,
    DateTime? utcNow,
  }) async {
    if (customerId <= 0 || delegateId <= 0) return;
    final parsed = DateTime.tryParse(createdAtUtc)?.toUtc();
    final key = IraqDate.businessDateKey(parsed ?? utcNow);
    final db = await DatabaseHelper().database;
    await db.insert(
      'BusinessDayCollection',
      {
        'CustomerId': customerId,
        'DelegateId': delegateId,
        'BusinessDateKey': key,
        'Amount': amount,
        'ClientPaymentId':
            clientPaymentId.isEmpty ? '${customerId}_$key' : clientPaymentId,
        'CreatedAtUtc': createdAtUtc,
      },
      conflictAlgorithm: ConflictAlgorithm.replace,
    );
  }

  static Future<List<Map<String, dynamic>>> forBusinessDay({
    int? delegateId,
    DateTime? utcNow,
  }) async {
    final key = IraqDate.businessDateKey(utcNow);
    final db = await DatabaseHelper().database;
    if (delegateId != null && delegateId > 0) {
      return db.query(
        'BusinessDayCollection',
        where: 'BusinessDateKey = ? AND DelegateId = ?',
        whereArgs: [key, delegateId],
      );
    }
    return db.query(
      'BusinessDayCollection',
      where: 'BusinessDateKey = ?',
      whereArgs: [key],
    );
  }

  static Future<bool> hasCustomerOnBusinessDay({
    required int customerId,
    required int delegateId,
    DateTime? utcNow,
  }) async {
    final key = IraqDate.businessDateKey(utcNow);
    final db = await DatabaseHelper().database;
    final rows = await db.query(
      'BusinessDayCollection',
      columns: ['id'],
      where: 'BusinessDateKey = ? AND CustomerId = ? AND DelegateId = ?',
      whereArgs: [key, customerId, delegateId],
      limit: 1,
    );
    return rows.isNotEmpty;
  }

  /// Drop markers older than a few business days (safety net).
  static Future<int> purgeOlderThanDays({
    int keepDays = 14,
    DateTime? utcNow,
  }) async {
    final today = IraqDate.businessDate(utcNow);
    final cutoff = today.subtract(Duration(days: keepDays));
    final y = cutoff.year.toString().padLeft(4, '0');
    final m = cutoff.month.toString().padLeft(2, '0');
    final d = cutoff.day.toString().padLeft(2, '0');
    final cutoffKey = '$y-$m-$d';
    final db = await DatabaseHelper().database;
    return db.delete(
      'BusinessDayCollection',
      where: 'BusinessDateKey < ?',
      whereArgs: [cutoffKey],
    );
  }
}
