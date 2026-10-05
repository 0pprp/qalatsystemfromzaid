import 'package:delegate_application/utils/safe_num.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('SafeNum.toDouble null and empty → 0', () {
    expect(SafeNum.toDouble(null), 0);
    expect(SafeNum.toDouble(''), 0);
    expect(SafeNum.toDouble('  '), 0);
  });

  test('SafeNum.toDouble parses num and string', () {
    expect(SafeNum.toDouble(12), 12);
    expect(SafeNum.toDouble(12.5), 12.5);
    expect(SafeNum.toDouble('1,000'), 1000);
    expect(SafeNum.toDouble('bad'), 0);
  });

  test('SafeNum.toInt handles sqlite num', () {
    expect(SafeNum.toInt(null), 0);
    expect(SafeNum.toInt(7), 7);
    expect(SafeNum.toInt(7.0), 7);
    expect(SafeNum.toInt('9'), 9);
  });
}
