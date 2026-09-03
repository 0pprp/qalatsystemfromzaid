import 'dart:convert';

import 'package:http/http.dart' as http;
import 'package:sales_employee_application/config/app_env.dart';

class CompanyBranch {
  const CompanyBranch({
    required this.value,
    required this.name,
    required this.database,
    required this.link,
  });

  final int value;
  final String name;
  final String database;
  final String link;

  String get apiBase => AppEnv.normalizeBase(link);

  @override
  bool operator ==(Object other) =>
      other is CompanyBranch && other.value == value && other.link == link;

  @override
  int get hashCode => Object.hash(value, link);

  factory CompanyBranch.fromJson(Map<String, dynamic> json) {
    return CompanyBranch(
      value: int.tryParse('${json['value'] ?? json['Value'] ?? 0}') ?? 0,
      name: '${json['name'] ?? json['Name'] ?? ''}'.trim(),
      database: '${json['database'] ?? json['Database'] ?? ''}'.trim(),
      link: '${json['link'] ?? json['Link'] ?? ''}'.trim(),
    );
  }

  Map<String, dynamic> toJson() => {
        'value': value,
        'name': name,
        'database': database,
        'link': apiBase,
      };
}

class CompanyBranches {
  static const getAdminUrl = 'http://defaultdata.alsaaeidy.com/GetAdmin';

  static const excludedNameParts = [
    'قانونية الشركة',
    'تجريبي',
    'الشهري',
  ];

  static bool isSalesBranchName(String name) {
    return !excludedNameParts.any(name.contains);
  }

  static List<CompanyBranch> parseSalesBranches(dynamic raw) {
    final list = raw is List ? raw : const [];
    return [
      for (final item in list)
        if (item is Map)
          CompanyBranch.fromJson(Map<String, dynamic>.from(item)),
    ].where((b) => b.link.isNotEmpty && isSalesBranchName(b.name)).toList();
  }

  static Future<List<CompanyBranch>> fetchSalesBranches() async {
    final response = await http
        .get(
          Uri.parse(getAdminUrl),
          headers: const {'Accept': 'application/json'},
        )
        .timeout(const Duration(seconds: 12));
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw Exception('تعذر جلب قائمة الفروع (${response.statusCode})');
    }
    return parseSalesBranches(jsonDecode(response.body));
  }
}
