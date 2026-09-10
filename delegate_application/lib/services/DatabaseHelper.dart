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
  ReceiptNumber TEXT,
  PermanentFailure INTEGER DEFAULT 0
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
  }

  Future<void> clearAllTables() async {
    final db = await database;
    await db.delete('SelectDelegate');
    await db.delete('Customer');
    await db.delete('CustomerPayment');
    await db.delete('DateWeek');
  }
}
