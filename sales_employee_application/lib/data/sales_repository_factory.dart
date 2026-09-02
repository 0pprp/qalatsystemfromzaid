import 'package:flutter/foundation.dart';
import 'package:sales_employee_application/config/app_env.dart';
import 'package:sales_employee_application/data/api_sales_repository.dart';
import 'package:sales_employee_application/data/mock_sales_repository.dart';
import 'package:sales_employee_application/data/sales_repository.dart';

class SalesRepositoryFactory {
  static SalesRepository? _instance;

  static SalesRepository get instance => _instance ??= create();

  static SalesRepository create() {
    if (AppEnv.useMockSalesRepository) {
      return MockSalesRepository();
    }
    return ApiSalesRepository();
  }

  @visibleForTesting
  static void reset() => _instance = null;

  @visibleForTesting
  static void setInstance(SalesRepository repository) => _instance = repository;
}
