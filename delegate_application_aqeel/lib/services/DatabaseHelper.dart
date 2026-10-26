import 'package:sqflite/sqflite.dart';
import 'package:path/path.dart';

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
      version: 4,
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
  }

  Future<void> _onUpgrade(Database db, int oldVersion, int newVersion) async {
    if (oldVersion < 2) {
      try {
        await db.execute('ALTER TABLE Customer ADD COLUMN LastPaymentDate TEXT;');
      } catch (_) {}
    }
    if (oldVersion < 3) {
      try {
        await db.execute('ALTER TABLE Customer ADD COLUMN DateSaleDevice TEXT;');
      } catch (_) {}
    }
    if (oldVersion < 4) {
      for (final col in [
        'ClientPaymentId TEXT',
        'CreatedAtUtc TEXT',
        'SyncStatus TEXT',
        'SyncError TEXT',
        'PermanentFailure INTEGER DEFAULT 0',
        'OwnerAsyncId TEXT',
      ]) {
        try {
          await db.execute('ALTER TABLE CustomerPayment ADD COLUMN $col;');
        } catch (_) {}
      }
      await db.execute(
        'CREATE UNIQUE INDEX IF NOT EXISTS UX_CustomerPayment_ClientPaymentId ON CustomerPayment(ClientPaymentId) WHERE ClientPaymentId IS NOT NULL;',
      );
      // Deterministic legacy ids — never invent random UUIDs for old rows.
      final missing = await db.rawQuery('''
SELECT id FROM CustomerPayment
WHERE ClientPaymentId IS NULL OR TRIM(ClientPaymentId) = '';
''');
      for (final row in missing) {
        final id = row['id'] as int;
        await db.update(
          'CustomerPayment',
          {
            'ClientPaymentId': 'legacy-local-$id',
            'SyncStatus': 'PendingSync',
            'CreatedAtUtc':
                DateTime.now().toUtc().toIso8601String(),
          },
          where: 'id = ?',
          whereArgs: [id],
        );
      }
    }
  }

  /// Any payment not confirmed synced must block destructive wipe.
  Future<int> countUnresolvedPayments() async {
    final db = await database;
    final rows = await db.rawQuery('''
SELECT COUNT(1) AS c FROM CustomerPayment
WHERE SyncStatus IS NULL
   OR SyncStatus = ''
   OR SyncStatus = 'PendingSync'
   OR SyncStatus = 'Syncing'
   OR SyncStatus = 'SyncFailed'
   OR SyncStatus = 'AwaitingSession'
   OR PermanentFailure = 1
''');
    return (rows.first['c'] as num?)?.toInt() ?? 0;
  }

  Future<void> clearAllTables({bool allowWipeUnresolved = false}) async {
    final unresolved = await countUnresolvedPayments();
    if (!allowWipeUnresolved && unresolved > 0) {
      throw StateError('UNRESOLVED_PAYMENTS:$unresolved');
    }
    final db = await database;
    await db.delete('SelectDelegate');
    await db.delete('Customer');
    await db.delete('CustomerPayment');
    await db.delete('DateWeek');
  }

  /// Replace table contents only after a successful in-memory fetch.
  Future<void> replaceTableRows(
    String tableName,
    List<Map<String, dynamic>> rows,
  ) async {
    final db = await database;
    await db.transaction((txn) async {
      await txn.delete(tableName);
      for (final row in rows) {
        await txn.insert(tableName, row,
            conflictAlgorithm: ConflictAlgorithm.replace);
      }
    });
  }
}
