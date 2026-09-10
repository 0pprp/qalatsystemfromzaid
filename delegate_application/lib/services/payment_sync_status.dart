class PaymentSyncStatus {
  static const pendingSync = 'PendingSync';
  static const syncing = 'Syncing';
  static const synced = 'Synced';
  static const syncFailed = 'SyncFailed';

  static bool needsSync(String? status) =>
      status == null ||
      status.isEmpty ||
      status == pendingSync ||
      status == syncFailed ||
      status == syncing;
}
