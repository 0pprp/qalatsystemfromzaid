/// Friendly Arabic labels for mission cityValues (never show DatabaseCompany*).
class ProvinceLabels {
  static const demoBranchValue = 'DatabaseCompanyNajaf_DEMO';

  static String display(String? cityValue) {
    final raw = (cityValue ?? '').trim();
    if (raw.isEmpty) return 'غير متوفر';
    final mapped = _map(raw);
    if (mapped != null) return mapped;
    if (raw.toLowerCase().startsWith('database')) return 'محافظة';
    return raw;
  }

  static bool _isDemoKey(String value) =>
      value.toUpperCase().contains('DEMO') ||
      value.toLowerCase().contains('_demo');

  static String? _map(String value) {
    var key = value.trim();
    final demo = _isDemoKey(key);
    if (RegExp(r'_DEMO$', caseSensitive: false).hasMatch(key)) {
      key = key.substring(0, key.length - 5);
    }
    switch (key.toLowerCase()) {
      case 'databasecompanynajaf':
      case 'najaf-demo':
      case 'najaf':
      case '1':
        return demo ? 'النجف - DEMO' : 'النجف';
      case 'databasecompanybasra':
      case 'basra':
        return 'البصرة';
      case 'databasecompanybaghdadkarak':
      case 'karkh':
        return 'الكرخ';
      case 'databasecompanybaghdadrosafa':
      case 'rosafa':
      case 'rusafa':
        return 'الرصافة';
      case 'databasecompanykarbala':
      case 'karbala':
        return 'كربلاء';
      case 'databasecompanybabil':
      case 'babil':
        return 'بابل';
      case 'databasecompanydewania':
      case 'diwaniya':
      case 'dewania':
        return 'الديوانية';
      case 'databasecompanykot':
      case 'wasit':
      case 'kot':
        return 'واسط';
      case 'databasecompanynasria':
      case 'nasria':
        return 'ذي قار';
      case 'databasecompanymaysan':
      case 'maysan':
        return 'ميسان';
      case 'databasecompanymothana':
      case 'muthanna':
      case 'mothana':
        return 'المثنى';
      case 'databasecompanymusol':
      case 'nineveh':
      case 'musol':
        return 'نينوى';
      case 'databasecompanykirkuk':
      case 'kirkuk':
        return 'كركوك';
      case 'databasecompanydiyala':
      case 'diyala':
        return 'ديالى';
      case 'local':
        return 'محلي';
      default:
        return null;
    }
  }
}
