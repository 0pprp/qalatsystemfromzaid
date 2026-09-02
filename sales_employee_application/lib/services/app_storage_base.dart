abstract class AppStorage {
  Future<void> init();
  String? getString(String key);
  Future<void> setString(String key, String value);
  Future<void> remove(String key);
  Future<void> writeBytes(String name, List<int> bytes);
  Future<void> openNamedFile(String name);
}
