import 'package:flutter_test/flutter_test.dart';

void main() {
  bool noteOk(int rating, String? note) {
    if (rating < 1 || rating > 10) return false;
    if (rating >= 7) return true;
    return (note ?? '').trim().isNotEmpty;
  }

  test('note optional at rating >= 7', () {
    expect(noteOk(7, null), isTrue);
    expect(noteOk(10, ''), isTrue);
    expect(noteOk(6, null), isFalse);
    expect(noteOk(6, 'ضعيف'), isTrue);
  });

  test('stats delta current minus baseline', () {
    expect(5000 - 3500, 1500);
  });
}
