import 'package:delegate_application/utils/iraq_date.dart';

enum TodayPaymentFilter { all, paid, unpaid }

enum TodayPaymentStatus { paid, unpaid }

/// Pure helpers for «تسديدات اليوم».
///
/// Eligible pool (existing product convention — not calendar weekday due):
/// continuous customers (LastPaymentDate or DateSaleDevice within 365 days)
/// with AmountRemaining > 0. DateWeek is receipt-PDF labels only.
class TodayPaymentsLogic {
  static bool isContinuous({
    required String? lastPaymentDate,
    required String? dateSaleDevice,
    DateTime? now,
  }) {
    final reference = now ?? DateTime.now();
    bool isLastPaymentRecent = false;
    bool isDateSaleRecent = false;

    if (lastPaymentDate != null && lastPaymentDate.isNotEmpty) {
      try {
        final date = DateTime.parse(lastPaymentDate);
        isLastPaymentRecent = reference.difference(date).inDays < 365;
      } catch (_) {}
    }

    if (dateSaleDevice != null && dateSaleDevice.isNotEmpty) {
      try {
        final date = DateTime.parse(dateSaleDevice);
        isDateSaleRecent = reference.difference(date).inDays < 365;
      } catch (_) {}
    }

    return isLastPaymentRecent || isDateSaleRecent;
  }

  /// Eligible for «تسديدات اليوم»:
  /// - has a payment today (keeps fully-paid customers visible after refresh), OR
  /// - continuous customer with AmountRemaining > 0.
  static bool isEligibleForTodayCollection(
    Map<String, dynamic> customer, {
    bool hasTodayPayment = false,
  }) {
    if (hasTodayPayment) return true;
    final remaining = _toDouble(customer['AmountRemaining']);
    if (remaining <= 0) return false;
    return isContinuous(
      lastPaymentDate: customer['LastPaymentDate']?.toString(),
      dateSaleDevice: customer['DateSaleDevice']?.toString(),
    );
  }

  /// Latest local payment for customer on the current Iraq **business** day
  /// (03:00 Baghdad boundary — matches [DelegateBusinessDateService] rollover).
  static Map<String, dynamic>? todayPaymentForCustomer({
    required int customerId,
    required List<Map<String, dynamic>> localPayments,
    DateTime? utcNow,
  }) {
    final matches = localPayments.where((p) {
      final id = int.tryParse(p['CustomerId']?.toString() ?? '') ?? 0;
      if (id != customerId) return false;
      return IraqDate.isCreatedOnIraqDay(
          p['CreatedAtUtc']?.toString(), utcNow);
    }).toList();
    if (matches.isEmpty) return null;
    matches.sort((a, b) {
      final aT = a['CreatedAtUtc']?.toString() ?? '';
      final bT = b['CreatedAtUtc']?.toString() ?? '';
      return bT.compareTo(aT);
    });
    return matches.first;
  }

  /// True when this customer already has a local payment on the current
  /// business day for [delegateId] (lifetime history must not block).
  static bool hasPaymentOnBusinessDay({
    required int customerId,
    required int delegateId,
    required List<Map<String, dynamic>> localPayments,
    DateTime? utcNow,
  }) {
    return localPayments.any((p) {
      final cid = int.tryParse(p['CustomerId']?.toString() ?? '') ?? 0;
      final did = int.tryParse(p['DelegateId']?.toString() ?? '') ?? 0;
      if (cid != customerId || did != delegateId) return false;
      return IraqDate.isCreatedOnIraqDay(p['CreatedAtUtc']?.toString(), utcNow);
    });
  }

  /// Unsynced rows (any calendar day) block a second collection until upload
  /// finishes — stops day-23 pending + day-25 re-collect duplicates.
  static bool hasUnsyncedPayment({
    required int customerId,
    required int delegateId,
    required List<Map<String, dynamic>> localPayments,
  }) {
    return localPayments.any((p) {
      final cid = int.tryParse(p['CustomerId']?.toString() ?? '') ?? 0;
      final did = int.tryParse(p['DelegateId']?.toString() ?? '') ?? 0;
      if (cid != customerId || did != delegateId) return false;
      final status = p['SyncStatus']?.toString();
      return status == null ||
          status.isEmpty ||
          status == 'PendingSync' ||
          status == 'SyncFailed' ||
          status == 'Syncing';
    });
  }

  /// Block re-collection when local day payment, durable ledger, or unsynced
  /// leftover exists.
  static bool blocksNewCollection({
    required int customerId,
    required int delegateId,
    required List<Map<String, dynamic>> localPayments,
    bool ledgerHasBusinessDay = false,
    DateTime? utcNow,
  }) {
    if (ledgerHasBusinessDay) return true;
    if (hasPaymentOnBusinessDay(
      customerId: customerId,
      delegateId: delegateId,
      localPayments: localPayments,
      utcNow: utcNow,
    )) {
      return true;
    }
    return hasUnsyncedPayment(
      customerId: customerId,
      delegateId: delegateId,
      localPayments: localPayments,
    );
  }

  static List<Map<String, dynamic>> filterPaymentsOnBusinessDay(
    List<Map<String, dynamic>> localPayments, {
    DateTime? utcNow,
  }) {
    return localPayments
        .where(
          (p) =>
              IraqDate.isCreatedOnIraqDay(p['CreatedAtUtc']?.toString(), utcNow),
        )
        .toList();
  }

  /// Local-day memory for print + UI: live [CustomerPayment] rows on the
  /// current business day, plus [BusinessDayCollection] markers that survive
  /// after a successful sync deletes the queue row.
  ///
  /// Does **not** depend on what has arrived on the server — only device memory.
  static List<Map<String, dynamic>> mergeLocalDayPayments({
    required List<Map<String, dynamic>> localPayments,
    required List<Map<String, dynamic>> ledgerRows,
    Map<int, String>? customerNames,
    DateTime? utcNow,
  }) {
    final today = filterPaymentsOnBusinessDay(localPayments, utcNow: utcNow);
    final merged = <Map<String, dynamic>>[];
    final seen = <String>{};

    for (final p in today) {
      final cid = p['CustomerId']?.toString() ?? '';
      final did = p['DelegateId']?.toString() ?? '';
      seen.add('$cid|$did');
      final row = Map<String, dynamic>.from(p);
      final id = int.tryParse(cid) ?? 0;
      if (id > 0) {
        row['CustomerId'] = id;
      }
      final name = row['CustomerName']?.toString() ?? '';
      if (name.isEmpty && customerNames != null && id > 0) {
        row['CustomerName'] = customerNames[id] ?? '';
      }
      merged.add(row);
    }

    for (final p in ledgerRows) {
      final cid = p['CustomerId']?.toString() ?? '';
      final did = p['DelegateId']?.toString() ?? '';
      final key = '$cid|$did';
      if (seen.contains(key)) continue;
      seen.add(key);
      final id = int.tryParse(cid) ?? 0;
      final didInt = int.tryParse(did) ?? 0;
      merged.add({
        'CustomerId': id,
        'DelegateId': didInt > 0 ? didInt : p['DelegateId'],
        'Amount': p['Amount'],
        'CreatedAtUtc': p['CreatedAtUtc'],
        'SyncStatus': 'Synced',
        'CustomerName':
            (customerNames != null && id > 0) ? (customerNames[id] ?? '') : '',
        'ClientPaymentId': p['ClientPaymentId'],
      });
    }

    return merged;
  }

  static double sumAmounts(List<Map<String, dynamic>> payments) {
    var total = 0.0;
    for (final p in payments) {
      total += _toDouble(p['Amount']);
    }
    return total;
  }

  static List<TodayPaymentRow> buildRows({
    required List<Map<String, dynamic>> customers,
    required List<Map<String, dynamic>> localPayments,
    required Map<int, String> delegateNames,
    TodayPaymentFilter filter = TodayPaymentFilter.all,
    String searchQuery = '',
    DateTime? utcNow,
  }) {
    final q = searchQuery.trim();
    final rows = <TodayPaymentRow>[];

    for (final c in customers) {
      final customerId = int.tryParse(c['CustomerId']?.toString() ?? '') ?? 0;
      if (customerId <= 0) continue;

      final pay = todayPaymentForCustomer(
        customerId: customerId,
        localPayments: localPayments,
        utcNow: utcNow,
      );
      if (!isEligibleForTodayCollection(c, hasTodayPayment: pay != null)) {
        continue;
      }

      final status =
          pay != null ? TodayPaymentStatus.paid : TodayPaymentStatus.unpaid;
      if (filter == TodayPaymentFilter.paid && status != TodayPaymentStatus.paid) {
        continue;
      }
      if (filter == TodayPaymentFilter.unpaid &&
          status != TodayPaymentStatus.unpaid) {
        continue;
      }

      final name = c['CustomerName']?.toString() ?? '';
      if (q.isNotEmpty && !name.contains(q)) continue;

      final delegateId =
          int.tryParse(c['DelegateId']?.toString() ?? '') ?? 0;

      rows.add(TodayPaymentRow(
        customerId: customerId,
        customerName: name,
        listName: delegateNames[delegateId] ?? '',
        installment: _toDouble(c['AmountDaySales']),
        status: status,
        paidAmount: pay != null ? _toDouble(pay['Amount']) : null,
        paidAtUtc: pay?['CreatedAtUtc']?.toString(),
      ));
    }

    rows.sort((a, b) {
      if (a.status != b.status) {
        return a.status == TodayPaymentStatus.unpaid ? -1 : 1;
      }
      return a.customerName.compareTo(b.customerName);
    });
    return rows;
  }

  static TodayPaymentsSummary summarize(List<TodayPaymentRow> allEligible) {
    final paid = allEligible.where((r) => r.status == TodayPaymentStatus.paid);
    final unpaid =
        allEligible.where((r) => r.status == TodayPaymentStatus.unpaid);
    final total = paid.fold<double>(0, (s, r) => s + (r.paidAmount ?? 0));
    return TodayPaymentsSummary(
      dueCount: allEligible.length,
      paidCount: paid.length,
      unpaidCount: unpaid.length,
      totalReceivedToday: total,
    );
  }

  static double _toDouble(dynamic v) {
    if (v == null) return 0;
    if (v is num) return v.toDouble();
    return double.tryParse(v.toString().replaceAll(',', '')) ?? 0;
  }
}

class TodayPaymentRow {
  final int customerId;
  final String customerName;
  final String listName;
  final double installment;
  final TodayPaymentStatus status;
  final double? paidAmount;
  final String? paidAtUtc;

  const TodayPaymentRow({
    required this.customerId,
    required this.customerName,
    required this.listName,
    required this.installment,
    required this.status,
    this.paidAmount,
    this.paidAtUtc,
  });
}

class TodayPaymentsSummary {
  final int dueCount;
  final int paidCount;
  final int unpaidCount;
  final double totalReceivedToday;

  const TodayPaymentsSummary({
    required this.dueCount,
    required this.paidCount,
    required this.unpaidCount,
    required this.totalReceivedToday,
  });
}
