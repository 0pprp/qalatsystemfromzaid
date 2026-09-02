import 'package:flutter_test/flutter_test.dart';
import 'package:sales_employee_application/config/app_env.dart';

void main() {
  test('local api base is the sales-employee gateway', () {
    expect(AppEnv.localApiBaseUrl.contains('5280'), isTrue);
  });
}
