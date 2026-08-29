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
      version: 3,
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
    await db.execute(
      'CREATE TABLE IF NOT EXISTS CustomerPayment (id INTEGER PRIMARY KEY AUTOINCREMENT, CustomerId INTEGER, CustomerName TEXT, DelegateId INTEGER, DelegateName TEXT, Amount FLOAT, Location TEXT);',
    );
    await db.execute(
      'CREATE TABLE IF NOT EXISTS DateWeek (id INTEGER PRIMARY KEY AUTOINCREMENT, Date1 TEXT, Date2 TEXT, Date3 TEXT, Date4 TEXT, Date5 TEXT, Date6 TEXT, Date7 TEXT);',
    );
  }

  Future<void> _onUpgrade(Database db, int oldVersion, int newVersion) async {
    if (oldVersion < 2) {
      // Check if column exists before adding it to avoid errors if logic was partially run before
      try {
        await db
            .execute("ALTER TABLE Customer ADD COLUMN LastPaymentDate TEXT;");
      } catch (e) {
        // Log "Column LastPaymentDate might already exist: $e"
      }
    }
    if (oldVersion < 3) {
      try {
        await db
            .execute("ALTER TABLE Customer ADD COLUMN DateSaleDevice TEXT;");
      } catch (e) {
        // Log error
      }
    }
  }

  Future<void> clearAllTables() async {
    final db = await database;
    await db.delete('SelectDelegate');
    await db.delete('Customer');
    await db.delete('CustomerPayment');
    await db.delete('DateWeek');
  }
}
