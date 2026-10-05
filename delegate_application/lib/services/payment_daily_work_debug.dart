import 'package:delegate_application/services/today_payments_logic.dart';
import 'package:delegate_application/utils/iraq_date.dart';

/// Pure regression guards for daily delegate work.
///
/// Locks the contracts that caused past-day totals to grow days later:
/// 1. Send window stays open after midnight until 03:00 (same business day)
/// 2. UI day zeros at 03:00 (prior CreatedAtUtc not counted)
/// 3. Print/paid strip uses merged local memory (queue ∪ ledger)
class PaymentDailyWorkDebug {
  /// Send allowed in [16:00 → 02:59] Baghdad only.
  static bool isSendWindowOpen(DateTime utcNow) =>
      IraqDate.isPaymentSyncGateOpen(utcNow);

  /// True when a payment collected on [collectedUtc] would still show in the
  /// «تسديدات اليوم» UI at [nowUtc].
  static bool countsInTodayUi({
    required DateTime collectedUtc,
    required DateTime nowUtc,
  }) {
    return IraqDate.isCreatedOnIraqDay(
      collectedUtc.toUtc().toIso8601String(),
      nowUtc.toUtc(),
    );
  }

  /// After sync wipe, paid print must still include ledger-only rows.
  static bool paidStripSurvivesSyncWipe({
    required List<Map<String, dynamic>> queueAfterWipe,
    required List<Map<String, dynamic>> ledger,
    required DateTime utcNow,
  }) {
    final merged = TodayPaymentsLogic.mergeLocalDayPayments(
      localPayments: queueAfterWipe,
      ledgerRows: ledger,
      utcNow: utcNow,
    );
    return merged.isNotEmpty;
  }

  /// Documented catastrophe: gate closed at 01:00 after collecting until 23:00
  /// caused pending to wait until next afternoon → prior day inflated.
  static bool midnightGapWouldBlockSend(DateTime utcAt0100Baghdad) {
    // Must be FALSE after the fix (send window open overnight until 03:00).
    return !IraqDate.isPaymentSyncGateOpen(utcAt0100Baghdad);
  }
}
