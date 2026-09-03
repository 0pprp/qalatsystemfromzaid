import 'package:flutter_test/flutter_test.dart';
import 'package:sales_employee_application/config/app_env.dart';
import 'package:sales_employee_application/config/company_branches.dart';
import 'package:sales_employee_application/data/sales_models.dart';
import 'package:sales_employee_application/services/global_customer_search.dart';

void main() {
  test('local api base is the sales-employee gateway', () {
    expect(AppEnv.localApiBaseUrl.contains('5280'), isTrue);
  });

  test('production default does not use the demo host', () {
    expect(AppEnv.isDemo, isFalse);
    expect(AppEnv.apiBase().contains('169.58.236.52'), isFalse);
    expect(AppEnv.useMockSalesRepository, isFalse);
  });

  test('GetAdmin sales branches exclude legal/demo/monthly names', () {
    final rows = CompanyBranches.parseSalesBranches([
      {
        'value': 1,
        'name': 'النجف',
        'database': 'DatabaseCompanyNajaf',
        'link': 'http://sharenewnajaf.alsaaeidy.com/api/',
      },
      {
        'value': 2,
        'name': 'قانونية الشركة',
        'database': 'DatabaseLegal',
        'link': 'http://legal.alsaaeidy.com/api/',
      },
      {
        'value': 3,
        'name': 'تجريبي',
        'database': 'DatabaseDemo',
        'link': 'http://demo.alsaaeidy.com/api/',
      },
      {
        'value': 4,
        'name': 'الشهري',
        'database': 'DatabaseMonthly',
        'link': 'http://monthly.alsaaeidy.com/api/',
      },
    ]);
    expect(rows, hasLength(1));
    expect(rows.single.name, 'النجف');
    expect(rows.single.apiBase, 'http://sharenewnajaf.alsaaeidy.com/api/');
  });

  test('global search dedupes by branch+id and by phone+name', () {
    final rows = GlobalCustomerSearch.dedupe([
      SalesCustomer(
        customerId: 1,
        fullName: 'أحمد',
        phone: '0780 111',
        sourceBranchValue: 1,
      ),
      SalesCustomer(
        customerId: 1,
        fullName: 'أحمد',
        phone: '0780111',
        sourceBranchValue: 1,
      ),
      SalesCustomer(
        customerId: 9,
        fullName: 'أحمد',
        phone: '0780111',
        sourceBranchValue: 2,
      ),
    ]);
    expect(rows, hasLength(1));
  });
}
