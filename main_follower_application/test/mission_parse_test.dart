import 'package:flutter_test/flutter_test.dart';
import 'package:main_follower_application/core/config/app_env.dart';
import 'package:main_follower_application/domain/mission.dart';

void main() {
  test('AppEnv demo resolves sales-gw', () {
    final base = AppEnv.resolveApiBase(env: 'demo');
    expect(base.contains('sales-gw'), isTrue);
    expect(base.contains('169.58.236.52'), isTrue);
  });

  test('Mission.fromJson maps cities', () {
    final m = Mission.fromJson({
      'id': 3,
      'committeeId': 1,
      'committeeName': 'لجنة 1',
      'status': 'Active',
      'cityValues': ['DatabaseCompanyBasra', 'DatabaseCompanyNajaf'],
      'note': 'جولة',
    });
    expect(m.id, 3);
    expect(m.cityValues.length, 2);
    expect(m.committeeName, 'لجنة 1');
  });
}
