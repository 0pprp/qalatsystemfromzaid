import 'package:sales_employee_application/services/app_storage.dart';

class LocalStore {
  LocalStore._();
  static final LocalStore instance = LocalStore._();
  late final AppStorage _storage = createPlatformStorage();

  Future<void> init() => _storage.init();

  String? getString(String key) => _storage.getString(key);

  Future<void> setString(String key, String value) =>
      _storage.setString(key, value);

  Future<void> remove(String key) => _storage.remove(key);

  Future<void> writeBytes(String name, List<int> bytes) =>
      _storage.writeBytes(name, bytes);

  Future<void> openNamedFile(String name) => _storage.openNamedFile(name);
}
