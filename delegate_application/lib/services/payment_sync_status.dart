class PaymentSyncStatus {
  static const pendingSync = 'PendingSync';
  static const syncing = 'Syncing';
  static const synced = 'Synced';
  static const syncFailed = 'SyncFailed';

  /// Session expired on server — keep row, retry after login (not PermanentFailure).
  static const awaitingSession = 'AwaitingSession';

  /// Legacy row needs stable ClientPaymentId before upload.
  static const awaitingClientId = 'AwaitingClientId';

  static bool needsSync(String? status) =>
      status == null ||
      status.isEmpty ||
      status == pendingSync ||
      status == syncFailed ||
      status == syncing ||
      status == awaitingSession;

  static bool isSessionHold(String? status) => status == awaitingSession;
}
