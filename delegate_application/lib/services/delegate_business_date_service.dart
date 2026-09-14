import 'package:delegate_application/services/payment_sync_service.dart';
import 'package:delegate_application/today_payments_page.dart';
import 'package:delegate_application/utils/iraq_date.dart';
import 'package:flutter/widgets.dart';
import 'package:shared_preferences/shared_preferences.dart';

/// Idempotent daily rollover for «تسديدات اليوم» (Baghdad business day @ 03:00).
///
/// Does **not** delete server history or local payment rows. Only advances
/// [lastBusinessDate] and refreshes the today-list UI after attempting sync.
class DelegateBusinessDateService with WidgetsBindingObserver {
  DelegateBusinessDateService._();
  static final DelegateBusinessDateService instance =
      DelegateBusinessDateService._();

  static const prefsKey = 'lastBusinessDate';

  bool _started = false;
  bool _rolling = false;

  Future<void> start() async {
    if (_started) return;
    _started = true;
    WidgetsBinding.instance.addObserver(this);
    await ensureCurrentBusinessDay();
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed) {
      ensureCurrentBusinessDay();
    }
  }

  Future<void> dispose() async {
    WidgetsBinding.instance.removeObserver(this);
    _started = false;
  }

  Future<String?> readLastBusinessDate() async {
    final prefs = await SharedPreferences.getInstance();
    return prefs.getString(prefsKey);
  }

  /// Returns true when a rollover (or first stamp) occurred.
  Future<bool> ensureCurrentBusinessDay({DateTime? utcNow}) async {
    if (_rolling) return false;
    _rolling = true;
    try {
      final current = IraqDate.businessDateKey(utcNow);
      final prefs = await SharedPreferences.getInstance();
      final last = prefs.getString(prefsKey);
      if (last == current) {
        return false;
      }

      // Flush pending uploads from previous business day(s) before UI switch.
      // CreatedAtUtc on each row preserves the original event time.
      await PaymentSyncService.instance.syncPendingPayments();

      await prefs.setString(prefsKey, current);
      TodayPaymentsRefresh.notify();
      return true;
    } finally {
      _rolling = false;
    }
  }
}
