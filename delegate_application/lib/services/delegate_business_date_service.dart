import 'dart:async';

import 'package:delegate_application/services/DatabaseHelper.dart';
import 'package:delegate_application/services/business_day_collection_store.dart';
import 'package:delegate_application/services/payment_sync_service.dart';
import 'package:delegate_application/today_payments_page.dart';
import 'package:delegate_application/utils/iraq_date.dart';
import 'package:flutter/widgets.dart';
import 'package:shared_preferences/shared_preferences.dart';

/// Idempotent daily rollover for «تسديدات اليوم» (Baghdad business day @ 03:00).
///
/// Before switching the UI day: last-chance flush of previous-day pending
/// (bypass 16:00 gate once — those rows were already eligible after 16:00).
/// Then purges **Synced** local rows from prior business days. PendingSync /
/// SyncFailed that remain offline are kept until the next send window.
///
/// Schedules an in-process timer for the next 03:00 so a foreground app that
/// never backgrounds still zeroes day counters.
///
/// Payment HTTP upload window: [16:00 → 02:59] Baghdad ([PaymentSyncService]).
class DelegateBusinessDateService with WidgetsBindingObserver {
  DelegateBusinessDateService._();
  static final DelegateBusinessDateService instance =
      DelegateBusinessDateService._();

  static const prefsKey = 'lastBusinessDate';

  bool _started = false;
  bool _rolling = false;
  Timer? _nextRolloverTimer;

  Future<void> start() async {
    if (_started) return;
    _started = true;
    WidgetsBinding.instance.addObserver(this);
    await ensureCurrentBusinessDay();
    _scheduleNextRollover();
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed) {
      unawaited(_onResume());
    }
  }

  Future<void> _onResume() async {
    await ensureCurrentBusinessDay();
    _scheduleNextRollover();
  }

  Future<void> dispose() async {
    WidgetsBinding.instance.removeObserver(this);
    _nextRolloverTimer?.cancel();
    _nextRolloverTimer = null;
    _started = false;
  }

  Future<String?> readLastBusinessDate() async {
    final prefs = await SharedPreferences.getInstance();
    return prefs.getString(prefsKey);
  }

  /// Returns true when a rollover (or first stamp) occurred.
  /// Never throws — sync/purge failures must not break list/rating UI loads.
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

      // Last-chance upload of previous business-day leftovers before UI zeroes.
      // Bypass gate: at 03:00 the new day's send window is not open yet, but
      // these rows already passed yesterday's 16:00 eligibility.
      try {
        await PaymentSyncService.instance
            .flushPendingForBusinessDayRollover(utcNow: utcNow);
      } catch (_) {}

      // Dedupe exact ClientPaymentId copies before purge.
      try {
        await DatabaseHelper().deleteDuplicateClientPaymentIds();
      } catch (_) {}

      // Physical local wipe: Synced rows outside this business day only.
      try {
        await DatabaseHelper()
            .purgeSyncedPaymentsBeforeBusinessDay(utcNow: utcNow);
      } catch (_) {}
      try {
        await BusinessDayCollectionStore.purgeOlderThanDays(utcNow: utcNow);
      } catch (_) {}

      await prefs.setString(prefsKey, current);
      TodayPaymentsRefresh.notify();
      return true;
    } catch (_) {
      return false;
    } finally {
      _rolling = false;
    }
  }

  void _scheduleNextRollover({DateTime? utcNow}) {
    _nextRolloverTimer?.cancel();
    final delay = IraqDate.delayUntilNextBusinessDayStart(utcNow);
    // Clamp tiny delays so we never busy-loop if clock jumps.
    final safe = delay < const Duration(seconds: 1)
        ? const Duration(seconds: 1)
        : delay;
    _nextRolloverTimer = Timer(safe, () async {
      await ensureCurrentBusinessDay();
      _scheduleNextRollover();
    });
  }
}
