import 'package:delegate_application/services/payment_local_retention.dart';
import 'package:delegate_application/utils/iraq_date.dart';
import 'package:path/path.dart';
import 'package:sqflite/sqflite.dart';

class DatabaseHelper {
  static final DatabaseHelper _instance = DatabaseHelper._internal();
  static Database? _database;

  factory DatabaseHelper() {
    return _instance;
  }

  DatabaseHelper._internal();

  Future<Database> get database async {
    if (_database != null) return _database!;
    _database = await _initDatabase();
    return _database!;
  }

  Future<Database> _initDatabase() async {
    String path = join(await getDatabasesPath(), 'DelegateData_DB.db');
    return await openDatabase(
      path,
      version: 6,
      onCreate: _onCreate,
      onUpgrade: _onUpgrade,
    );
  }

  Future<void> _onCreate(Database db, int version) async {
    await db.execute(
      'CREATE TABLE IF NOT EXISTS SelectDelegate (id INTEGER PRIMARY KEY AUTOINCREMENT, DelegateId INTEGER, DelegateName TEXT, ReceiptName TEXT, UpdateReceipt BOOLEAN, DeleteReceipt BOOLEAN, DevicePaymentState BOOLEAN);',
    );
    await db.execute(
      'CREATE TABLE IF NOT EXISTS Customer (id INTEGER PRIMARY KEY AUTOINCREMENT, CustomerId INTEGER, CustomerName TEXT, DelegateId INTEGER, PhoneNumber TEXT, AmountTotalSales FLOAT, AmountDaySales FLOAT, ReceiptsTotal FLOAT, AmountRemaining FLOAT, ItemsNames TEXT, CityId INTEGER, Amount1 FLOAT, Amount2 FLOAT, Amount3 FLOAT, Amount4 FLOAT, Amount5 FLOAT, Amount6 FLOAT, Amount7 FLOAT, PhoneNumberCompany TEXT, CountReceiptDevice INTEGER, Address TEXT, ShopName TEXT, NumberOfDayPayment INTEGER, IsLegal TEXT, LastPaymentDate TEXT, DateSaleDevice TEXT);',
    );
    await db.execute('''
CREATE TABLE IF NOT EXISTS CustomerPayment (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  CustomerId INTEGER,
  CustomerName TEXT,
  DelegateId INTEGER,
  DelegateName TEXT,
  Amount FLOAT,
  Location TEXT,
  ClientPaymentId TEXT,
  CreatedAtUtc TEXT,
  SyncStatus TEXT,
  SyncError TEXT,
  ReceiptNumber TEXT,
  PermanentFailure INTEGER DEFAULT 0,
  OwnerAsyncId TEXT
);
''');
    await db.execute(
      'CREATE UNIQUE INDEX IF NOT EXISTS UX_CustomerPayment_ClientPaymentId ON CustomerPayment(ClientPaymentId) WHERE ClientPaymentId IS NOT NULL;',
    );
    await db.execute(
      'CREATE TABLE IF NOT EXISTS DateWeek (id INTEGER PRIMARY KEY AUTOINCREMENT, Date1 TEXT, Date2 TEXT, Date3 TEXT, Date4 TEXT, Date5 TEXT, Date6 TEXT, Date7 TEXT);',
    );
    await _ensureBusinessDayCollectionTable(db);
  }

  Future<void> _onUpgrade(Database db, int oldVersion, int newVersion) async {
    if (oldVersion < 2) {
      try {
        await db
            .execute("ALTER TABLE Customer ADD COLUMN LastPaymentDate TEXT;");
      } catch (_) {}
    }
    if (oldVersion < 3) {
      try {
        await db
            .execute("ALTER TABLE Customer ADD COLUMN DateSaleDevice TEXT;");
      } catch (_) {}
    }
    if (oldVersion < 4) {
      await _ensureCustomerPaymentSyncColumns(db);
    }
    if (oldVersion < 5) {
      await _ensureBusinessDayCollectionTable(db);
    }
    if (oldVersion < 6) {
      await _ensureOwnerAsyncIdColumn(db);
    }
  }

  Future<void> _ensureOwnerAsyncIdColumn(Database db) async {
    try {
      await db.execute(
          'ALTER TABLE CustomerPayment ADD COLUMN OwnerAsyncId TEXT;');
    } catch (_) {}
  }

  Future<void> _ensureBusinessDayCollectionTable(Database db) async {
    await db.execute('''
CREATE TABLE IF NOT EXISTS BusinessDayCollection (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  CustomerId INTEGER NOT NULL,
  DelegateId INTEGER NOT NULL,
  BusinessDateKey TEXT NOT NULL,
  Amount FLOAT,
  ClientPaymentId TEXT,
  CreatedAtUtc TEXT
);
''');
    await db.execute('''
CREATE UNIQUE INDEX IF NOT EXISTS UX_BusinessDayCollection_DayCustomer
ON BusinessDayCollection(BusinessDateKey, CustomerId, DelegateId);
''');
    // Seed today's rows so existing pending/synced payments keep the day lock.
    try {
      final rows = await db.query('CustomerPayment');
      final todayKey = IraqDate.businessDateKey();
      for (final row in rows) {
        final created = row['CreatedAtUtc']?.toString() ?? '';
        if (!IraqDate.isCreatedOnIraqDay(created)) continue;
        final cid = int.tryParse(row['CustomerId']?.toString() ?? '') ?? 0;
        final did = int.tryParse(row['DelegateId']?.toString() ?? '') ?? 0;
        if (cid <= 0 || did <= 0) continue;
        await db.insert(
          'BusinessDayCollection',
          {
            'CustomerId': cid,
            'DelegateId': did,
            'BusinessDateKey': todayKey,
            'Amount': row['Amount'],
            'ClientPaymentId': row['ClientPaymentId'],
            'CreatedAtUtc': created,
          },
          conflictAlgorithm: ConflictAlgorithm.replace,
        );
      }
    } catch (_) {}
  }

  /// Delete local rows immediately after the server accepted them.
  Future<int> deleteCustomerPaymentByIds(List<int> ids) async {
    final db = await database;
    return _deleteCustomerPaymentIds(db, ids);
  }

  Future<void> _ensureCustomerPaymentSyncColumns(Database db) async {
    final columns = <String>[
      'ClientPaymentId TEXT',
      'CreatedAtUtc TEXT',
      'SyncStatus TEXT',
      'SyncError TEXT',
      'ReceiptNumber TEXT',
      'PermanentFailure INTEGER DEFAULT 0',
    ];
    for (final col in columns) {
      try {
        await db.execute('ALTER TABLE CustomerPayment ADD COLUMN $col;');
      } catch (_) {}
    }
    try {
      await db.execute(
        'CREATE UNIQUE INDEX IF NOT EXISTS UX_CustomerPayment_ClientPaymentId ON CustomerPayment(ClientPaymentId) WHERE ClientPaymentId IS NOT NULL;',
      );
    } catch (_) {}
    // Legacy rows without ClientPaymentId stay local until recreated; mark pending.
    try {
      await db.execute('''
UPDATE CustomerPayment
SET SyncStatus = 'PendingSync'
WHERE SyncStatus IS NULL OR SyncStatus = '';
''');
    } catch (_) {}

    // Stable backfill: never invent random UUIDs for previously unknown server rows.
    // Use deterministic legacy-{localId} so retries stay idempotent by ClientPaymentId.
    try {
      final missing = await db.rawQuery('''
SELECT id FROM CustomerPayment
WHERE ClientPaymentId IS NULL OR TRIM(ClientPaymentId) = '';
''');
      for (final row in missing) {
        final id = row['id'];
        if (id == null) continue;
        await db.update(
          'CustomerPayment',
          {
            'ClientPaymentId': 'legacy-local-$id',
            'SyncStatus': 'PendingSync',
            'SyncError': null,
            'PermanentFailure': 0,
          },
          where: 'id = ?',
          whereArgs: [id],
        );
      }
    } catch (_) {}
  }

  /// After login: re-queue session-held payments owned by this AsyncId.
  Future<int> releaseAwaitingSessionPayments({required String ownerAsyncId}) async {
    final db = await database;
    final owner = ownerAsyncId.trim();
    if (owner.isEmpty) return 0;
    return db.rawUpdate('''
UPDATE CustomerPayment
SET SyncStatus = 'PendingSync', SyncError = NULL, PermanentFailure = 0
WHERE SyncStatus = 'AwaitingSession'
  AND (OwnerAsyncId IS NULL OR OwnerAsyncId = '' OR OwnerAsyncId = ?)
''', [owner]);
  }

  /// Unresolved financial rows that must not be wiped on logout.
  Future<int> countUnresolvedPayments() async {
    final db = await database;
    final rows = await db.rawQuery('''
SELECT COUNT(1) AS c FROM CustomerPayment
WHERE SyncStatus IS NULL
   OR SyncStatus = 'PendingSync'
   OR SyncStatus = 'Syncing'
   OR SyncStatus = 'SyncFailed'
   OR PermanentFailure = 1
''');
    return (rows.first['c'] as num?)?.toInt() ?? 0;
  }

  Future<void> clearAllTables({bool allowWipeUnresolved = false}) async {
    final unresolved = await countUnresolvedPayments();
    if (!allowWipeUnresolved && unresolved > 0) {
      throw StateError(
          'UNRESOLVED_PAYMENTS:$unresolved');
    }
    final db = await database;
    await db.delete('SelectDelegate');
    await db.delete('Customer');
    await db.delete('CustomerPayment');
    await db.delete('DateWeek');
    try {
      await db.delete('BusinessDayCollection');
    } catch (_) {}
  }

  /// At 03:00 business-day rollover: delete local rows that are already
  /// [Synced] and outside the current business day. Never touches PendingSync /
  /// SyncFailed / Syncing.
  Future<int> purgeSyncedPaymentsBeforeBusinessDay({DateTime? utcNow}) async {
    final db = await database;
    final rows = await db.query(
      'CustomerPayment',
      columns: ['id', 'CreatedAtUtc', 'SyncStatus'],
    );
    final ids = PaymentLocalRetention.syncedOutsideBusinessDayRowIdsToDelete(
      rows,
      utcNow: utcNow,
    );
    return _deleteCustomerPaymentIds(db, ids);
  }

  /// Exact ClientPaymentId duplicates only (keep smallest id). Safe; does not
  /// group by Amount.
  Future<int> deleteDuplicateClientPaymentIds() async {
    final db = await database;
    final rows = await db.query(
      'CustomerPayment',
      columns: ['id', 'ClientPaymentId'],
    );
    final ids =
        PaymentLocalRetention.duplicateClientPaymentIdRowIdsToDelete(rows);
    return _deleteCustomerPaymentIds(db, ids);
  }

  Future<int> _deleteCustomerPaymentIds(Database db, List<int> ids) async {
    if (ids.isEmpty) return 0;
    var deleted = 0;
    const chunk = 200;
    for (var i = 0; i < ids.length; i += chunk) {
      final slice = ids.sublist(i, i + chunk > ids.length ? ids.length : i + chunk);
      final placeholders = List.filled(slice.length, '?').join(',');
      deleted += await db.rawDelete(
        'DELETE FROM CustomerPayment WHERE id IN ($placeholders)',
        slice,
      );
    }
    return deleted;
  }
}
