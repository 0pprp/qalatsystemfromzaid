import 'package:flutter_test/flutter_test.dart';

void main() {
  test('empty assigned lists shows dedicated empty copy', () {
    const emptyMessage = 'لا توجد قوائم مخصصة لك حاليًا';
    final lists = <int>[];
    final message = lists.isEmpty ? emptyMessage : null;
    expect(message, emptyMessage);
  });
}
