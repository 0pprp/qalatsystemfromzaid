/// Outcome of a user-tapped «إرسال التسديدات» action.
enum ManualPaymentSyncOutcome {
  /// No usable network — UI must show the Arabic offline message.
  offline,

  /// Before 16:00 Baghdad — uploads are not allowed yet.
  gateClosed,

  /// Another sync is already running.
  busy,

  /// Online, and no retryable/permanent/unresolved rows remain.
  nothingPending,

  /// At least one pending row was uploaded successfully this run.
  uploaded,

  /// Online and attempted, but zero rows reached Synced (all failed / network).
  failed,

  /// Finished with a mix / remaining unresolved (never show full success).
  partial,
}

class ManualPaymentSyncResult {
  const ManualPaymentSyncResult({
    required this.outcome,
    this.uploadedCount = 0,
    this.remainingRetryable = 0,
    this.permanentFailures = 0,
    this.remainingPending = 0,
  });

  final ManualPaymentSyncOutcome outcome;
  final int uploadedCount;
  final int remainingRetryable;
  final int permanentFailures;

  /// Backward-compatible alias: retryable unresolved count.
  final int remainingPending;

  bool get isCompleteSuccess =>
      outcome == ManualPaymentSyncOutcome.uploaded &&
      remainingRetryable == 0 &&
      permanentFailures == 0;

  String summaryMessage() {
    switch (outcome) {
      case ManualPaymentSyncOutcome.offline:
        return offlineMessage;
      case ManualPaymentSyncOutcome.gateClosed:
        return gateClosedMessage;
      case ManualPaymentSyncOutcome.busy:
        return 'جاري إرسال التسديدات حالياً — انتظر قليلاً';
      case ManualPaymentSyncOutcome.nothingPending:
        if (permanentFailures > 0) {
          return 'لا توجد تسديدات قابلة للإرسال، لكن يوجد $permanentFailures مرفوضاً يحتاج مراجعة';
        }
        return 'لا توجد تسديدات بانتظار الإرسال';
      case ManualPaymentSyncOutcome.uploaded:
      case ManualPaymentSyncOutcome.partial:
      case ManualPaymentSyncOutcome.failed:
        return 'تم التأكيد: $uploadedCount — قابل لإعادة المحاولة: $remainingRetryable — مرفوض نهائياً: $permanentFailures';
    }
  }

  static const offlineMessage = 'يجب الحصول على انترنت لارسال التسديدات';
  static const gateClosedMessage =
      'التسديدات تُرفع أونلاين فوراً — لا حاجة لانتظار الساعة 4 مساءً';
}
