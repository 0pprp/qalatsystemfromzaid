/// Province options for mission assignment (cityValues).
///
/// Demo never calls production GetAdmin — only النجف DEMO.
/// Production maps GetAdmin rows to DatabaseCompany* keys when present.
class ProvinceOption {
  const ProvinceOption({required this.value, required this.name});

  /// Stored in mission `cityValues` (matches employee / gateway keys).
  final String value;

  /// Arabic label shown in the multi-select list.
  final String name;
}

class ProvinceCatalog {
  static const String demoBranchValue = 'DatabaseCompanyNajaf_DEMO';
  static const String getAdminUrl = 'http://defaultdata.alsaaeidy.com/GetAdmin';

  static bool isDemoEnv(String env) => env.trim().toLowerCase() == 'demo';
  static bool isLocalEnv(String env) => env.trim().toLowerCase() == 'local';

  /// Sync catalog for demo/local. Production starts empty and loads GetAdmin.
  static List<ProvinceOption> forEnv(String env) {
    if (isDemoEnv(env)) {
      return const [
        ProvinceOption(value: demoBranchValue, name: 'النجف - DEMO'),
      ];
    }
    if (isLocalEnv(env)) {
      return const [
        ProvinceOption(value: 'local', name: 'محلي'),
      ];
    }
    return const [];
  }

  static List<ProvinceOption> fromGetAdmin(Iterable<dynamic> rows) {
    final result = <ProvinceOption>[];
    final seen = <String>{};

    for (final raw in rows) {
      if (raw is! Map) continue;
      final map = Map<String, dynamic>.from(raw);
      final name = _text(map['name']);
      final link = _text(map['link']);
      if (name.isEmpty || link.isEmpty) continue;
      if (_isExcludedSalesBranch(name)) continue;

      final database = _text(map['database']);
      final valueRaw = map['value'];
      final value = database.toLowerCase().startsWith('databasecompany')
          ? database
          : (valueRaw == null ? '' : valueRaw.toString().trim());
      if (value.isEmpty) continue;
      if (!seen.add(value.toLowerCase())) continue;

      result.add(ProvinceOption(value: value, name: name));
    }

    return result;
  }

  static bool _isExcludedSalesBranch(String name) {
    return name.contains('قانونية الشركة') ||
        name.contains('تجريبي') ||
        name.contains('الشهري');
  }

  static String _text(dynamic value) =>
      value == null ? '' : value.toString().trim();
}
