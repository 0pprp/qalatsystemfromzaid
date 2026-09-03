import 'package:sales_employee_application/config/company_branches.dart';
import 'package:sales_employee_application/data/sales_models.dart';
import 'package:sales_employee_application/services/api_client.dart';

class GlobalCustomerSearch {
  static const perBranchTimeout = Duration(seconds: 5);

  static Future<List<SalesCustomer>> search(String query) async {
    final branches = await CompanyBranches.fetchSalesBranches();
    if (branches.isEmpty) return const [];

    final chunks = await Future.wait(
      branches.map((branch) => _searchBranch(branch, query)),
    );

    return dedupe([for (final chunk in chunks) ...chunk]);
  }

  static Future<List<SalesCustomer>> _searchBranch(
    CompanyBranch branch,
    String query,
  ) async {
    try {
      final raw = await ApiClient.getAbsolute(
        '${branch.apiBase}sales/customers/directory',
        query: {'q': query},
        timeout: perBranchTimeout,
      );
      final list = raw is List ? raw : const [];
      return [
        for (final item in list)
          if (item is Map)
            SalesCustomer.fromJson(Map<String, dynamic>.from(item)).copyWithBranch(
              sourceBranchValue: branch.value,
              sourceBranchName: branch.name,
              sourceDatabase: branch.database,
              sourceApiLink: branch.apiBase,
            ),
      ];
    } catch (_) {
      return const [];
    }
  }

  static List<SalesCustomer> dedupe(List<SalesCustomer> rows) {
    final seen = <String>{};
    final result = <SalesCustomer>[];
    for (final row in rows) {
      final keys = <String>{
        '${row.sourceBranchValue ?? ''}|${row.customerId}',
      };
      final phone = (row.phone ?? '').replaceAll(RegExp(r'\s+'), '');
      final name = row.fullName.trim();
      if (phone.isNotEmpty && name.isNotEmpty) {
        keys.add('p:$phone|$name');
      }
      if (keys.any(seen.contains)) {
        continue;
      }
      seen.addAll(keys);
      result.add(row);
    }
    return result;
  }
}
