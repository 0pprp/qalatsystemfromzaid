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

  static bool isEligibleForTodayCollection(Map<String, dynamic> customer) {
    final remaining = _toDouble(customer['AmountRemaining']);
    if (remaining <= 0) return false;
    return isContinuous(
      lastPaymentDate: customer['LastPaymentDate']?.toString(),
      dateSaleDevice: customer['DateSaleDevice']?.toString(),
    );
  }

  /// Latest local payment for customer today (Iraq day via CreatedAtUtc).
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
      if (!isEligibleForTodayCollection(c)) continue;
      final customerId = int.tryParse(c['CustomerId']?.toString() ?? '') ?? 0;
      if (customerId <= 0) continue;

      final pay = todayPaymentForCustomer(
        customerId: customerId,
        localPayments: localPayments,
        utcNow: utcNow,
      );
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
