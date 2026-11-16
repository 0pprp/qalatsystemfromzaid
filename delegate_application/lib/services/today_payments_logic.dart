import 'package:delegate_application/utils/iraq_date.dart';

enum TodayPaymentFilter { all, paid, unpaid }

/// Why «تسديد» was refused before a row was written.
enum CollectionBlockKind { none, alreadyCollected, waitingToSend }

class CollectionBlock {
  final CollectionBlockKind kind;
  final String? message;

  const CollectionBlock._(this.kind, this.message);

  static const none = CollectionBlock._(CollectionBlockKind.none, null);

  static const alreadyCollectedMessage = 'لا يمكن التسديد مرة أخرى لهذا العميل';

  static const waitingToSendMessage =
      'يوجد تسديد سابق بانتظار الإرسال لهذا العميل — اضغط إرسال التسديدات أولاً';

  bool get isBlocked => kind != CollectionBlockKind.none;

  static CollectionBlock fromFlags({
    required bool ledgerHasBusinessDay,
    required bool hasPaymentOnBusinessDay,
    required bool hasUnsyncedPayment,
  }) {
    if (ledgerHasBusinessDay || hasPaymentOnBusinessDay) {
      return const CollectionBlock._(
        CollectionBlockKind.alreadyCollected,
        alreadyCollectedMessage,
      );
    }
    if (hasUnsyncedPayment) {
      return const CollectionBlock._(
        CollectionBlockKind.waitingToSend,
        waitingToSendMessage,
      );
    }
    return none;
  }

  /// Headline plus one block of facts per local row, so a photo of the dialog
  /// shows why this customer cannot be collected.
  static CollectionBlock detailed({
    required CollectionBlockKind kind,
    required String headline,
    List<Map<String, dynamic>> rows = const [],
    String? extraLine,
  }) {
    final lines = <String>[headline];
    if (extraLine != null && extraLine.trim().isNotEmpty) {
      lines.add(extraLine.trim());
    }
    if (rows.isEmpty && extraLine == null) {
      return CollectionBlock._(kind, headline);
    }
    for (var i = 0; i < rows.length && i < 3; i++) {
      lines.add('');
      if (rows.length > 1) {
        lines.add('تسديد ${i + 1}:');
      } else {
        lines.add('تفاصيل التسديد العالق:');
      }
      lines.addAll(_factLines(rows[i]));
    }
    if (rows.length > 3) {
      lines.add('');
      lines.add('وتسديدات أخرى: ${rows.length - 3}');
    }
    return CollectionBlock._(kind, lines.join('\n'));
  }

  static List<String> _factLines(Map<String, dynamic> row) {
    final status = row['SyncStatus']?.toString();
    final error = row['SyncError']?.toString().trim() ?? '';
    final created = row['CreatedAtUtc']?.toString() ?? '';
    final clientPaymentId = row['ClientPaymentId']?.toString().trim() ?? '';
    return [
      'المبلغ: ${_formatAmount(row['Amount'])}',
      'تاريخ الحفظ: ${IraqDate.formatIraqDateTime(created).isEmpty ? 'غير معروف' : IraqDate.formatIraqDateTime(created)}',
      'الوقت المخزن: ${created.isEmpty ? 'فارغ' : created}',
      'الحالة: ${_statusLabel(status)}',
      'رمز الحالة: ${status == null || status.isEmpty ? 'فارغ' : status}',
      'مرفوض نهائياً: ${TodayPaymentsLogic.isPermanentFailureFlag(row['PermanentFailure']) ? 'نعم' : 'لا'}',
      'سبب الرفض: ${error.isEmpty ? 'لا يوجد' : error}',
      'رقم العميل: ${row['CustomerId'] ?? 'فارغ'}',
      'رقم القائمة: ${row['DelegateId'] ?? 'فارغ'}',
      'معرّف التسديد: ${clientPaymentId.isEmpty ? 'فارغ' : clientPaymentId}',
    ];
  }

  static String _statusLabel(String? status) {
    switch (status) {
      case null:
      case '':
        return 'بدون حالة';
      case 'PendingSync':
        return 'بانتظار الإرسال';
      case 'Syncing':
        return 'جاري الإرسال ولم يكتمل';
      case 'SyncFailed':
        return 'فشل الإرسال';
      case 'Synced':
        return 'تم الإرسال';
      case 'AwaitingSession':
        return 'بانتظار إعادة تسجيل الدخول';
      default:
        return status;
    }
  }

  static String _formatAmount(dynamic value) {
    final raw = value?.toString().replaceAll(',', '') ?? '';
    final amount = double.tryParse(raw);
    if (amount == null) return 'غير معروف';
    final digits = amount.round().abs().toString();
    final buffer = StringBuffer();
    for (var i = 0; i < digits.length; i++) {
      if (i > 0 && (digits.length - i) % 3 == 0) buffer.write(',');
      buffer.write(digits[i]);
    }
    if (amount < 0) return '-$buffer';
    return buffer.toString();
  }
}

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

  /// Server rejects CreatedAtUtc older than this ([CollectionPaymentRules.MaxPastSkew]).
  static const Duration maxUploadAge = Duration(days: 30);

  static const tooOldToUploadMessage = 'وقت إنشاء التسديد قديم جدًا';

  static bool isPermanentFailureFlag(dynamic value) {
    if (value == null) return false;
    if (value is bool) return value;
    if (value is num) return value != 0;
    final text = value.toString().trim().toLowerCase();
    return text == '1' || text == 'true';
  }

  /// True when the server will never accept this historical row.
  static bool isBeyondUploadWindow(String? createdAtUtc, [DateTime? utcNow]) {
    if (createdAtUtc == null || createdAtUtc.trim().isEmpty) return false;
    try {
      final created = DateTime.parse(createdAtUtc.trim()).toUtc();
      final now = (utcNow ?? DateTime.now()).toUtc();
      return now.difference(created) > maxUploadAge;
    } catch (_) {
      return false;
    }
  }

  /// Hard rejects and expired rows. «إرسال التسديدات» must not revive them,
  /// and they must not freeze a later collection.
  static bool isTerminalUnuploadableError(String? error) {
    final text = error?.trim() ?? '';
    if (text.isEmpty) return false;
    if (text == 'OWNER_MISMATCH') return true;
    if (text.contains('2,000') || text.contains('2000')) return true;
    if (text.contains('فارغًا') || text.contains('فارغا')) return true;
    if (text.contains('معرف العميل غير صالح')) return true;
    if (text.contains('قديم')) return true;
    return false;
  }

  /// Unsynced rows still worth uploading block a second collection.
  /// A row the server will never take (older than 30 days, or a terminal
  /// reject) does not — otherwise that customer can never pay again.
  static bool unsyncedRowBlocksCollection(
    Map<String, dynamic> payment, {
    DateTime? utcNow,
  }) {
    if (isBeyondUploadWindow(payment['CreatedAtUtc']?.toString(), utcNow)) {
      return false;
    }
    if (isPermanentFailureFlag(payment['PermanentFailure']) &&
        isTerminalUnuploadableError(payment['SyncError']?.toString())) {
      return false;
    }
    final status = payment['SyncStatus']?.toString();
    return status == null ||
        status.isEmpty ||
        status == 'PendingSync' ||
        status == 'SyncFailed' ||
        status == 'Syncing';
  }

  /// Unsynced rows (any calendar day) block a second collection until upload
  /// finishes — stops day-23 pending + day-25 re-collect duplicates.
  static bool hasUnsyncedPayment({
    required int customerId,
    required int delegateId,
    required List<Map<String, dynamic>> localPayments,
    DateTime? utcNow,
  }) {
    return localPayments.any((p) {
      final cid = int.tryParse(p['CustomerId']?.toString() ?? '') ?? 0;
      final did = int.tryParse(p['DelegateId']?.toString() ?? '') ?? 0;
      if (cid != customerId || did != delegateId) return false;
      return unsyncedRowBlocksCollection(p, utcNow: utcNow);
    });
  }

  /// Why a new collection is refused, so the dialog can leave «جاري حفظ…»
  /// and offer «إرسال التسديدات» instead of spinning forever.
  static List<Map<String, dynamic>> _rowsForCustomerDelegate({
    required int customerId,
    required int delegateId,
    required List<Map<String, dynamic>> localPayments,
  }) {
    return localPayments.where((p) {
      final cid = int.tryParse(p['CustomerId']?.toString() ?? '') ?? 0;
      final did = int.tryParse(p['DelegateId']?.toString() ?? '') ?? 0;
      return cid == customerId && did == delegateId;
    }).toList();
  }

  static CollectionBlock classifyCollection({
    required int customerId,
    required int delegateId,
    required List<Map<String, dynamic>> localPayments,
    bool ledgerHasBusinessDay = false,
    DateTime? utcNow,
  }) {
    final owned = _rowsForCustomerDelegate(
      customerId: customerId,
      delegateId: delegateId,
      localPayments: localPayments,
    );
    final onDay = owned
        .where((p) =>
            IraqDate.isCreatedOnIraqDay(p['CreatedAtUtc']?.toString(), utcNow))
        .toList();
    if (ledgerHasBusinessDay || onDay.isNotEmpty) {
      return CollectionBlock.detailed(
        kind: CollectionBlockKind.alreadyCollected,
        headline: CollectionBlock.alreadyCollectedMessage,
        rows: onDay,
        extraLine: onDay.isEmpty
            ? 'السبب: سجل يوم العمل على الجهاز يقول إن هذا العميل سُدّد اليوم، ولا يوجد صف ظاهر في قائمة الإرسال.'
            : 'السبب: يوجد تسديد محفوظ لهذا العميل في يوم العمل الحالي.',
      );
    }
    final waiting = owned
        .where((p) => unsyncedRowBlocksCollection(p, utcNow: utcNow))
        .toList();
    if (waiting.isNotEmpty) {
      return CollectionBlock.detailed(
        kind: CollectionBlockKind.waitingToSend,
        headline: CollectionBlock.waitingToSendMessage,
        rows: waiting,
        extraLine: 'السبب: هذا التسديد ما زال على الجهاز ولم يُقبل على الخادم.',
      );
    }
    return CollectionBlock.none;
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
    return classifyCollection(
      customerId: customerId,
      delegateId: delegateId,
      localPayments: localPayments,
      ledgerHasBusinessDay: ledgerHasBusinessDay,
      utcNow: utcNow,
    ).isBlocked;
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
