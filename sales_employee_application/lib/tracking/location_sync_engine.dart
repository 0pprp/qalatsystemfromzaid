import 'package:sales_employee_application/data/sales_repository.dart';
import 'package:sales_employee_application/tracking/location_store.dart';
import 'package:sales_employee_application/tracking/tracking_config.dart';

class LocationSyncEngine {
  LocationSyncEngine(this._store, this._repo);

  final LocationPointStore _store;
  final SalesRepository _repo;
  bool _busy = false;

  Future<bool> sync(int shiftId) async {
    if (_busy) return true;
    _busy = true;
    try {
      final events = await _store.pendingEvents();
      if (events.isNotEmpty) {
        for (final event in events) {
          await _repo.recordTrackingEvent(event.shiftId ?? shiftId, event.eventType);
        }
        await _store.markEventsSynced([for (final event in events) event.id]);
      }
      var rounds = 0;
      while (rounds < 25) {
        rounds++;
        final batch = await _store.pending(limit: TrackingConfig.syncBatchSize);
        if (batch.isEmpty) {
          return true;
        }
        await _repo.recordTrackingEvent(shiftId, 'SYNC_STARTED');
        final seqs = [for (final p in batch) p.deviceSequence];
        await _store.markStatus(seqs, shiftId, 'Syncing');
        try {
          await _repo.uploadLocationBatch(shiftId, batch);
          await _store.markStatus(seqs, shiftId, 'Synced');
          await _repo.recordTrackingEvent(shiftId, 'SYNC_COMPLETED');
        } catch (_) {
          await _store.markFailed(seqs, shiftId);
          await _repo.recordTrackingEvent(shiftId, 'SYNC_FAILED');
          return false;
        }
      }
      return true;
    } finally {
      _busy = false;
    }
  }
}
