import 'package:path/path.dart' as p;
import 'package:sales_employee_application/tracking/location_store.dart';
import 'package:sales_employee_application/tracking/work_shift.dart';
import 'package:sqflite/sqflite.dart';

class SqliteLocationStore implements LocationPointStore {
  Database? _db;

  static const schema = '''
CREATE TABLE IF NOT EXISTS local_location_points (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  shift_id INTEGER NOT NULL,
  latitude REAL NOT NULL,
  longitude REAL NOT NULL,
  accuracy REAL,
  speed REAL,
  heading REAL,
  captured_at_utc TEXT NOT NULL,
  device_sequence INTEGER NOT NULL,
  sync_status TEXT NOT NULL,
  retry_count INTEGER NOT NULL DEFAULT 0,
  UNIQUE(shift_id, device_sequence)
);
CREATE TABLE IF NOT EXISTS tracking_meta (
  key TEXT PRIMARY KEY,
  value TEXT
);
CREATE TABLE IF NOT EXISTS local_tracking_events (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  shift_id INTEGER,
  event_type TEXT NOT NULL,
  occurred_at_utc TEXT NOT NULL,
  sync_status TEXT NOT NULL
);
''';

  @override
  Future<void> init() async {
    if (_db != null) return;
    final dir = await getDatabasesPath();
    _db = await openDatabase(
      p.join(dir, 'sales_tracking.db'),
      version: 1,
      onCreate: (db, _) async {
        for (final stmt in schema.split(';').where((s) => s.trim().isNotEmpty)) {
          await db.execute(stmt);
        }
      },
      onOpen: (db) async {
        for (final stmt in schema.split(';').where((s) => s.trim().isNotEmpty)) {
          await db.execute(stmt);
        }
      },
    );
  }

  Database get db => _db!;

  @override
  Future<int> nextSequence(int shiftId) async {
    await init();
    final key = 'seq_$shiftId';
    final rows = await db.query('tracking_meta', where: 'key = ?', whereArgs: [key]);
    final current = rows.isEmpty ? 0 : int.tryParse('${rows.first['value']}') ?? 0;
    final next = current + 1;
    await db.insert('tracking_meta', {'key': key, 'value': '$next'}, conflictAlgorithm: ConflictAlgorithm.replace);
    return next;
  }

  @override
  Future<void> insert(LocalLocationPoint point) async {
    await init();
    await db.insert(
      'local_location_points',
      {
        'shift_id': point.shiftId,
        'latitude': point.latitude,
        'longitude': point.longitude,
        'accuracy': point.accuracy,
        'speed': point.speed,
        'heading': point.heading,
        'captured_at_utc': point.capturedAtUtc.toUtc().toIso8601String(),
        'device_sequence': point.deviceSequence,
        'sync_status': point.syncStatus,
        'retry_count': point.retryCount,
      },
      conflictAlgorithm: ConflictAlgorithm.ignore,
    );
  }

  @override
  Future<List<LocalLocationPoint>> pending({int limit = 200}) async {
    await init();
    final rows = await db.query(
      'local_location_points',
      where: "sync_status IN ('Pending','Failed')",
      orderBy: 'device_sequence ASC',
      limit: limit,
    );
    return rows.map(_map).toList();
  }

  @override
  Future<void> markStatus(List<int> sequences, int shiftId, String status) async {
    if (sequences.isEmpty) return;
    await init();
    final placeholders = List.filled(sequences.length, '?').join(',');
    await db.rawUpdate(
      'UPDATE local_location_points SET sync_status = ? WHERE shift_id = ? AND device_sequence IN ($placeholders)',
      [status, shiftId, ...sequences],
    );
  }

  @override
  Future<void> markFailed(List<int> sequences, int shiftId) async {
    if (sequences.isEmpty) return;
    await init();
    final placeholders = List.filled(sequences.length, '?').join(',');
    await db.rawUpdate(
      "UPDATE local_location_points SET sync_status = 'Failed', retry_count = retry_count + 1 WHERE shift_id = ? AND device_sequence IN ($placeholders)",
      [shiftId, ...sequences],
    );
  }

  @override
  Future<int> pendingCount() async {
    await init();
    final row = await db.rawQuery("SELECT COUNT(*) AS c FROM local_location_points WHERE sync_status != 'Synced'");
    return int.tryParse('${row.first['c']}') ?? 0;
  }

  @override
  Future<List<LocalTrackingEvent>> pendingEvents() async {
    await init();
    final rows = await db.query(
      'local_tracking_events',
      where: "sync_status IN ('Pending','Failed')",
      orderBy: 'id ASC',
      limit: 50,
    );
    return [
      for (final row in rows)
        LocalTrackingEvent(
          id: row['id'] as int,
          shiftId: row['shift_id'] as int?,
          eventType: '${row['event_type']}',
        )
    ];
  }

  @override
  Future<void> markEventsSynced(List<int> ids) async {
    if (ids.isEmpty) return;
    await init();
    final placeholders = List.filled(ids.length, '?').join(',');
    await db.rawUpdate(
      "UPDATE local_tracking_events SET sync_status = 'Synced' WHERE id IN ($placeholders)",
      ids,
    );
  }

  LocalLocationPoint _map(Map<String, Object?> row) => LocalLocationPoint(
        id: row['id'] as int?,
        shiftId: row['shift_id'] as int,
        latitude: (row['latitude'] as num).toDouble(),
        longitude: (row['longitude'] as num).toDouble(),
        accuracy: (row['accuracy'] as num?)?.toDouble(),
        speed: (row['speed'] as num?)?.toDouble(),
        heading: (row['heading'] as num?)?.toDouble(),
        capturedAtUtc: DateTime.parse('${row['captured_at_utc']}').toUtc(),
        deviceSequence: row['device_sequence'] as int,
        syncStatus: '${row['sync_status']}',
        retryCount: row['retry_count'] as int? ?? 0,
      );
}
